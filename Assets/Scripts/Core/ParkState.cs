using System;
using System.Collections.Generic;

namespace FocusPark.Core
{
    [Serializable] public sealed class FocusSession
    {
        public string id;
        public string task;
        public long startUnix;
        public long endUnix;
        public int plannedMinutes;
        public bool practice;
    }

    [Serializable] public sealed class SessionReceipt
    {
        public string id;
        public string task;
        public long endedUnix;
        public int minutes;
        public bool completed;
        public bool practice;
    }

    [Serializable] public sealed class PlacedAttraction
    {
        public string id;
        public string kind;
        public int x;
        public int z;
        public int rotation;
    }

    [Serializable] public sealed class ParkState
    {
        public int version = 1;
        public int tickets;
        public int lifetimeMinutes;
        public FocusSession session;
        public List<SessionReceipt> receipts = new List<SessionReceipt>();
        public List<PlacedAttraction> attractions = new List<PlacedAttraction>();
    }

    public sealed class AttractionSpec
    {
        public readonly string Id, Name, Description;
        public readonly int Cost, Width, Depth;
        public AttractionSpec(string id, string name, string description, int cost, int width, int depth)
        { Id = id; Name = name; Description = description; Cost = cost; Width = width; Depth = depth; }
    }

    public static class ParkRules
    {
        public const int Size = 12;
        public static readonly AttractionSpec[] Catalog = {
            new AttractionSpec("carousel", "Daydream Carousel", "Your first 25 minutes, made into a little magic.", 25, 2, 2),
            new AttractionSpec("tree", "Cloud Tree", "A soft green corner for your growing park.", 5, 1, 1),
            new AttractionSpec("stall", "Peach Soda Stand", "A tiny treat for a big effort.", 15, 1, 1),
            new AttractionSpec("wheel", "Sunrise Wheel", "A new view of everything you have built.", 75, 3, 2)
        };
        public static AttractionSpec Find(string id) { return Array.Find(Catalog, x => x.Id == id); }

        // Reject malformed persisted data before it can change rewards or rendering.
        public static void Validate(ParkState state)
        {
            if (state == null || state.version != 1 || state.tickets < 0 || state.lifetimeMinutes < 0 ||
                state.receipts == null || state.attractions == null)
                throw new InvalidOperationException("Unsupported or damaged park save.");
            if (state.session != null) ValidateSession(state.session);
            var ids = new HashSet<string>();
            foreach (var receipt in state.receipts)
                if (receipt == null || string.IsNullOrEmpty(receipt.id) || !ids.Add(receipt.id) ||
                    receipt.minutes < 0 || receipt.minutes > 120)
                    throw new InvalidOperationException("Damaged session history.");
            ids.Clear();
            var occupied = new bool[Size, Size];
            foreach (var attraction in state.attractions)
            {
                if (attraction == null || string.IsNullOrEmpty(attraction.id) || !ids.Add(attraction.id) ||
                    attraction.rotation < 0 || attraction.rotation > 3)
                    throw new InvalidOperationException("Damaged attraction data.");
                var spec = Find(attraction.kind);
                if (spec == null) throw new InvalidOperationException("Unknown attraction in park save.");
                int w = attraction.rotation % 2 == 0 ? spec.Width : spec.Depth;
                int d = attraction.rotation % 2 == 0 ? spec.Depth : spec.Width;
                if (attraction.x < 0 || attraction.z < 0 || attraction.x > Size - w || attraction.z > Size - d)
                    throw new InvalidOperationException("An attraction is outside the park.");
                for (int x = attraction.x; x < attraction.x + w; x++)
                    for (int z = attraction.z; z < attraction.z + d; z++)
                    {
                        if (x == 5 || z == 5 || occupied[x, z])
                            throw new InvalidOperationException("Attractions overlap or obstruct a path.");
                        occupied[x, z] = true;
                    }
            }
        }
        static void ValidateSession(FocusSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.id) || string.IsNullOrWhiteSpace(session.task) ||
                session.plannedMinutes < 15 || session.plannedMinutes > 120 || session.startUnix < 0 ||
                session.endUnix <= session.startUnix ||
                session.endUnix - session.startUnix != (session.practice ? 10 : session.plannedMinutes * 60))
                throw new ArgumentException("Invalid focus session.");
        }

        public static FocusSession Begin(ParkState state, string task, long now, int minutes, bool practice)
        {
            if (state.session != null) throw new InvalidOperationException("A session is already running.");
            if (string.IsNullOrWhiteSpace(task)) throw new ArgumentException("Give your task a name.");
            if (minutes < 15 || minutes > 120) throw new ArgumentOutOfRangeException(nameof(minutes));
            if (now < 0 || now > long.MaxValue - minutes * 60L) throw new ArgumentOutOfRangeException(nameof(now));
            return new FocusSession { id = Guid.NewGuid().ToString("N"), task = task.Trim(), startUnix = now,
                endUnix = now + (practice ? 10 : minutes * 60), plannedMinutes = minutes, practice = practice };
        }

        // Native blockers acknowledge the ID before this pending session is committed.
        public static void Activate(ParkState state, FocusSession session)
        {
            if (state.session != null) throw new InvalidOperationException("A session is already running.");
            ValidateSession(session);
            state.session = session;
        }

        public static SessionReceipt Settle(ParkState state, long now, bool endEarly)
        {
            var session = state.session;
            if (session == null || (!endEarly && now < session.endUnix)) return null;
            // Idempotency survives a resumed app and repeated native callbacks.
            var existing = state.receipts.Find(x => x.id == session.id);
            if (existing != null) { state.session = null; return null; }
            bool completed = now >= session.endUnix;
            int elapsed = (int)Math.Max(0, Math.Min(now - session.startUnix, session.endUnix - session.startUnix));
            int earned = completed ? session.plannedMinutes : (session.practice ? 0 : elapsed / 60);
            var receipt = new SessionReceipt { id = session.id, task = session.task, endedUnix = Math.Min(now, session.endUnix),
                minutes = earned, completed = completed, practice = session.practice };
            state.receipts.Add(receipt);
            state.tickets = checked(state.tickets + earned);
            if (!session.practice) state.lifetimeMinutes = checked(state.lifetimeMinutes + earned);
            state.session = null;
            return receipt;
        }

        public static bool CanPlace(ParkState state, string kind, int x, int z, int rotation, out string reason)
        {
            var spec = Find(kind);
            if (state.session != null) { reason = "Build between focus sessions."; return false; }
            if (spec == null) { reason = "Choose an attraction."; return false; }
            if (rotation < 0 || rotation > 3) { reason = "Choose a valid rotation."; return false; }
            if (state.tickets < spec.Cost) { reason = "Earn " + (spec.Cost - state.tickets) + " more tickets."; return false; }
            int w = rotation % 2 == 0 ? spec.Width : spec.Depth;
            int d = rotation % 2 == 0 ? spec.Depth : spec.Width;
            if (x < 0 || z < 0 || x > Size - w || z > Size - d) { reason = "Keep the attraction inside the park."; return false; }
            // Two free pedestrian paths cross the park.
            for (int a = x; a < x + w; a++) for (int b = z; b < z + d; b++)
                if (a == 5 || b == 5) { reason = "Leave the paths clear."; return false; }
            foreach (var placed in state.attractions)
            {
                var other = Find(placed.kind);
                if (other == null) continue;
                int ow = placed.rotation % 2 == 0 ? other.Width : other.Depth;
                int od = placed.rotation % 2 == 0 ? other.Depth : other.Width;
                if (x < placed.x + ow && x + w > placed.x && z < placed.z + od && z + d > placed.z)
                { reason = "That space already has an attraction."; return false; }
            }
            reason = "Ready to build."; return true;
        }

        public static PlacedAttraction Place(ParkState state, string kind, int x, int z, int rotation)
        {
            if (!CanPlace(state, kind, x, z, rotation, out var reason)) throw new InvalidOperationException(reason);
            var placed = new PlacedAttraction { id = Guid.NewGuid().ToString("N"), kind = kind, x = x, z = z, rotation = rotation % 4 };
            state.attractions.Add(placed);
            state.tickets -= Find(kind).Cost;
            return placed;
        }
    }
}

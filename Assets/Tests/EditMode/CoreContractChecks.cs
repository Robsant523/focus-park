using System;
using FocusPark.Core;

namespace FocusPark.Tests
{
    // Shared contract suite: runs in Unity's EditMode runner and in the standalone .NET check.
    public static class CoreContractChecks
    {
        public static readonly string[] Names = {
            "TwentyFiveMinutesEarnExactlyTwentyFiveTickets", "RepeatedSettlementAndReloadDoNotDuplicateRewards",
            "EarlyFinishKeepsOnlyWholeMinutes", "ClockBeforeStartCannotMintTickets", "SessionInputIsValidated",
            "OnlyOneSessionCanRun", "AttractionPurchasePersistsAndCostsTickets", "InsufficientTicketsDoNotMutateState",
            "PathsAndOccupiedCellsAreProtected", "RotatedFootprintRespectsParkBounds", "BuildingIsDisabledDuringFocus",
            "PracticeDoesNotIncreaseLifetimeFocus", "InterruptedPracticeHasNoReward",
            "DamagedSessionsCannotChangeRewards", "DamagedParkSavesAreRejected"
        };
        const long Start = 1700000000;
        public static void Run(string name, Func<ParkState, ParkState> reload)
        {
            var state = new ParkState();
            switch (name)
            {
                case "TwentyFiveMinutesEarnExactlyTwentyFiveTickets":
                    var session = ParkRules.Begin(state, "  Read chapter four  ", Start, 25, false);
                    Equal(1500L, session.endUnix - session.startUnix);
                    Equal("Read chapter four", session.task);
                    ParkRules.Activate(state, session);
                    Equal(null, ParkRules.Settle(state, Start + 1499, false));
                    Equal(0, state.tickets);
                    state = reload(state); // App closes mid-session, then returns after expiry.
                    var receipt = ParkRules.Settle(state, Start + 2000, false);
                    Equal(25, receipt.minutes); Equal(true, receipt.completed);
                    Equal(25, state.tickets); Equal(25, state.lifetimeMinutes);
                    Equal(null, state.session); Equal(Start + 1500, receipt.endedUnix);
                    break;
                case "RepeatedSettlementAndReloadDoNotDuplicateRewards":
                    var pending = ParkRules.Begin(state, "Study", Start, 25, false);
                    ParkRules.Activate(state, pending);
                    ParkRules.Settle(state, Start + 1500, false);
                    Equal(null, ParkRules.Settle(state, Start + 1501, false));
                    state = reload(state);
                    // Simulate a stale persisted/native callback carrying an already credited ID.
                    state.session = pending;
                    Equal(null, ParkRules.Settle(state, Start + 1800, false));
                    Equal(25, state.tickets); Equal(25, state.lifetimeMinutes); Equal(1, state.receipts.Count);
                    Equal(null, state.session);
                    break;
                case "EarlyFinishKeepsOnlyWholeMinutes":
                    ParkRules.Activate(state, ParkRules.Begin(state, "Write", Start, 25, false));
                    var early = ParkRules.Settle(state, Start + 539, true);
                    Equal(8, early.minutes); Equal(false, early.completed); Equal(8, state.tickets);
                    Equal(8, state.lifetimeMinutes); Equal(null, ParkRules.Settle(state, Start + 1500, false));
                    break;
                case "ClockBeforeStartCannotMintTickets":
                    ParkRules.Activate(state, ParkRules.Begin(state, "Write", Start, 25, false));
                    Equal(0, ParkRules.Settle(state, Start - 120, true).minutes);
                    Equal(0, state.tickets);
                    break;
                case "SessionInputIsValidated":
                    Throws<ArgumentException>(() => ParkRules.Begin(state, "  ", Start, 25, false));
                    Throws<ArgumentOutOfRangeException>(() => ParkRules.Begin(state, "Task", Start, 14, false));
                    Throws<ArgumentOutOfRangeException>(() => ParkRules.Begin(state, "Task", Start, 121, false));
                    Throws<ArgumentException>(() => ParkRules.Activate(state, null));
                    Equal(null, state.session); Equal(0, state.receipts.Count);
                    break;
                case "OnlyOneSessionCanRun":
                    var first = ParkRules.Begin(state, "Task", Start, 25, false);
                    ParkRules.Activate(state, first);
                    Throws<InvalidOperationException>(() => ParkRules.Begin(state, "Another task", Start, 25, false));
                    Throws<InvalidOperationException>(() => ParkRules.Activate(state, first));
                    Equal(first.id, state.session.id);
                    break;
                case "AttractionPurchasePersistsAndCostsTickets":
                    state.tickets = 25;
                    var placed = ParkRules.Place(state, "carousel", 1, 1, 0);
                    state = reload(state);
                    Equal(0, state.tickets); Equal(1, state.attractions.Count);
                    Equal(placed.id, state.attractions[0].id); Equal("carousel", state.attractions[0].kind);
                    Equal(1, state.attractions[0].x); Equal(1, state.attractions[0].z);
                    break;
                case "InsufficientTicketsDoNotMutateState":
                    state.tickets = 24;
                    Throws<InvalidOperationException>(() => ParkRules.Place(state, "carousel", 1, 1, 0));
                    Equal(24, state.tickets); Equal(0, state.attractions.Count);
                    Equal(false, ParkRules.CanPlace(state, "unknown", 1, 1, 0, out _));
                    break;
                case "PathsAndOccupiedCellsAreProtected":
                    state.tickets = 100;
                    Equal(false, ParkRules.CanPlace(state, "carousel", 4, 1, 0, out _));
                    Equal(false, ParkRules.CanPlace(state, "tree", 1, 5, 0, out _));
                    ParkRules.Place(state, "carousel", 1, 1, 0);
                    Equal(false, ParkRules.CanPlace(state, "tree", 2, 2, 0, out _));
                    Equal(true, ParkRules.CanPlace(state, "tree", 3, 2, 0, out _));
                    break;
                case "RotatedFootprintRespectsParkBounds":
                    state.tickets = 100;
                    Equal(false, ParkRules.CanPlace(state, "wheel", 10, 10, 0, out _));
                    Equal(false, ParkRules.CanPlace(state, "wheel", 10, 10, 1, out _));
                    Equal(true, ParkRules.CanPlace(state, "wheel", 10, 9, 1, out _));
                    Equal(false, ParkRules.CanPlace(state, "tree", -1, 1, 0, out _));
                    Equal(false, ParkRules.CanPlace(state, "tree", 1, -1, 0, out _));
                    break;
                case "BuildingIsDisabledDuringFocus":
                    state.tickets = 100;
                    ParkRules.Activate(state, ParkRules.Begin(state, "Task", Start, 25, false));
                    Throws<InvalidOperationException>(() => ParkRules.Place(state, "tree", 1, 1, 0));
                    Equal(100, state.tickets); Equal(0, state.attractions.Count);
                    break;
                case "PracticeDoesNotIncreaseLifetimeFocus":
                    var practice = ParkRules.Begin(state, "Try it", Start, 25, true);
                    Equal(10L, practice.endUnix - practice.startUnix);
                    ParkRules.Activate(state, practice);
                    var practiceReceipt = ParkRules.Settle(state, Start + 10, false);
                    Equal(25, state.tickets); Equal(0, state.lifetimeMinutes); Equal(true, practiceReceipt.practice);
                    break;
                case "InterruptedPracticeHasNoReward":
                    ParkRules.Activate(state, ParkRules.Begin(state, "Try it", Start, 25, true));
                    var interrupted = ParkRules.Settle(state, Start + 9, true);
                    Equal(0, interrupted.minutes); Equal(0, state.tickets); Equal(0, state.lifetimeMinutes);
                    break;
                case "DamagedSessionsCannotChangeRewards":
                    var damaged = ParkRules.Begin(state, "Task", Start, 25, false);
                    damaged.plannedMinutes = -100;
                    Throws<ArgumentException>(() => ParkRules.Activate(state, damaged));
                    damaged.plannedMinutes = 25; damaged.endUnix++;
                    Throws<ArgumentException>(() => ParkRules.Activate(state, damaged));
                    Equal(0, state.tickets); Equal(null, state.session);
                    break;
                case "DamagedParkSavesAreRejected":
                    ParkRules.Validate(reload(state));
                    state.lifetimeMinutes = -1;
                    Throws<InvalidOperationException>(() => ParkRules.Validate(state));
                    state.lifetimeMinutes = 0;
                    state.attractions.Add(new PlacedAttraction { id = "a", kind = "tree", x = 5, z = 1 });
                    Throws<InvalidOperationException>(() => ParkRules.Validate(state));
                    state.attractions[0].x = 1;
                    ParkRules.Validate(state);
                    state.attractions.Add(new PlacedAttraction { id = "b", kind = "tree", x = 1, z = 1 });
                    Throws<InvalidOperationException>(() => ParkRules.Validate(state));
                    state.attractions.Clear();
                    var duplicate = new SessionReceipt { id = "receipt", task = "Task", minutes = 25 };
                    state.receipts.Add(duplicate); state.receipts.Add(duplicate);
                    Throws<InvalidOperationException>(() => ParkRules.Validate(state));
                    break;
                default: throw new ArgumentException("Unknown contract: " + name);
            }
        }
        static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + "; actual " + actual);
        }
        static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new Exception("Expected " + typeof(T).Name);
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using FocusPark.Core;

namespace FocusPark.Game
{
    public sealed class ParkSave
    {
        readonly string path;
        public ParkSave(bool practice) { path = Path.Combine(Application.persistentDataPath, practice ? "practice-park.json" : "park.json"); }
        public string PathName => path;
        public ParkState Load()
        {
            if (!File.Exists(path)) return new ParkState();
            try { return Read(path); }
            catch (Exception primary)
            {
                if (File.Exists(path + ".bak"))
                {
                    try { return Read(path + ".bak"); } catch { /* Preserve both files for recovery. */ }
                }
                throw new IOException("Your park save could not be read. The original files have been preserved at " + path, primary);
            }
        }
        static ParkState Read(string file)
        {
            var value = JsonUtility.FromJson<ParkState>(File.ReadAllText(file));
            if (value == null || value.version != 1 || value.attractions == null || value.receipts == null || value.tickets < 0)
                throw new IOException("Unsupported or damaged park save.");
            ParkRules.Validate(value);
            return value;
        }
        public void Write(ParkState value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var writer = new StreamWriter(temp, false)) { writer.Write(JsonUtility.ToJson(value, true)); writer.Flush(); writer.BaseStream.Flush(); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}

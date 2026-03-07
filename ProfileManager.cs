// ProfileManager.cs
// Mirrors Python class ProfileManager — reads / writes profiles.json
// next to the running assembly using Newtonsoft.Json.
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ToolsByGimhan.RebarGenerator
{
    public sealed class ProfileManager
    {
        private readonly string _filePath;
        public Dictionary<string, JObject> Profiles { get; private set; } = new();

        public ProfileManager(string assemblyDirectory)
        {
            _filePath = Path.Combine(assemblyDirectory, "profiles.json");
            LoadProfiles();
        }

        // ── Load ─────────────────────────────────────────────────────────
        public void LoadProfiles()
        {
            Profiles = new Dictionary<string, JObject>(StringComparer.Ordinal);
            if (File.Exists(_filePath))
            {
                try
                {
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(
                                  File.ReadAllText(_filePath));
                    if (raw != null) Profiles = raw;
                }
                catch { /* ignore corrupt file */ }
            }
            if (!Profiles.ContainsKey("Default"))
                Profiles["Default"] = DefaultBeamProfile();
        }

        // ── Save ─────────────────────────────────────────────────────────
        public bool SaveProfile(string name, JObject data)
        {
            Profiles[name] = data;
            try { File.WriteAllText(_filePath, JsonConvert.SerializeObject(Profiles, Formatting.Indented)); return true; }
            catch { return false; }
        }

        // ── Delete ────────────────────────────────────────────────────────
        public bool DeleteProfile(string name)
        {
            if (name == "Default" || !Profiles.ContainsKey(name)) return false;
            Profiles.Remove(name);
            try { File.WriteAllText(_filePath, JsonConvert.SerializeObject(Profiles, Formatting.Indented)); return true; }
            catch { return false; }
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private static JObject DefaultBeamProfile() => JObject.Parse(
            @"{""End Sections"":{""T1"":2,""T2"":0,""B1"":2,""B2"":0},
               ""Middle Section"":{""T1"":2,""T2"":0,""B1"":2,""B2"":0},
               ""Global"":{""SideCover"":25,""EndOffset"":40,""EndSpacing"":150,
                            ""MidSpacing"":250,""BeamWidth"":300,""BeamHeight"":600}}");

        /// <summary>Returns value of a JObject token as T, or the defaultValue if missing.</summary>
        public static T GetVal<T>(JObject? obj, string key, T defaultValue)
        {
            if (obj == null || !obj.ContainsKey(key)) return defaultValue;
            try { return obj[key]!.Value<T>() ?? defaultValue; }
            catch { return defaultValue; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Remembers which saved profile belongs to which VRChat world. The current world is read
    /// by reflection from RoomManager so a game update that renames it only disables this
    /// feature (logged once) instead of breaking the build or the game.
    /// </summary>
    internal sealed class WorldProfiles
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly MelonLogger.Instance _log;
        private readonly string _path;
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        private MemberInfo _worldMember;
        private bool _searched;
        private bool _unavailable;
        private bool _loggedError;
        private float _nextRefresh;

        public string CurrentId { get; private set; } = string.Empty;
        public string CurrentName { get; private set; } = string.Empty;
        public bool Available { get { return !_unavailable; } }

        public WorldProfiles(MelonLogger.Instance log)
        {
            _log = log;
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "VividFrame");
            _path = Path.Combine(dir, "worlds.txt");
            Load();
        }

        /// <summary>Re-reads the current world at most once a second. Returns true if the world id changed.</summary>
        public bool Refresh()
        {
            if (_unavailable || Time.unscaledTime < _nextRefresh)
                return false;
            _nextRefresh = Time.unscaledTime + 1f;
            try
            {
                if (!_searched)
                    FindField();
                if (_worldMember == null)
                    return false;

                object world = _worldMember is PropertyInfo wp ? wp.GetValue(null) : ((FieldInfo)_worldMember).GetValue(null);
                string id = string.Empty;
                string name = string.Empty;
                if (world != null)
                {
                    id = ReadString(world, "id");
                    name = ReadString(world, "name");
                }

                if (id == CurrentId)
                {
                    if (!string.IsNullOrEmpty(name))
                        CurrentName = name;
                    return false;
                }
                CurrentId = id;
                CurrentName = name;
                return !string.IsNullOrEmpty(id);
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    _log?.Warning("World detection failed; world profiles are disabled this session: {0}", ex.Message);
                }
                _unavailable = true;
                return false;
            }
        }

        public string Lookup(string worldId)
        {
            if (string.IsNullOrEmpty(worldId))
                return null;
            return _map.TryGetValue(worldId, out string profile) ? profile : null;
        }

        public string PinnedForCurrent()
        {
            return Lookup(CurrentId);
        }

        public bool Pin(string profile)
        {
            if (string.IsNullOrEmpty(CurrentId) || string.IsNullOrEmpty(profile))
                return false;
            _map[CurrentId] = profile;
            Persist();
            return true;
        }

        public bool Unpin()
        {
            if (string.IsNullOrEmpty(CurrentId) || !_map.Remove(CurrentId))
                return false;
            Persist();
            return true;
        }

        private void FindField()
        {
            _searched = true;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string asmName = asm.GetName().Name;
                if (asmName == null || !asmName.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
                    continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch { continue; }

                foreach (Type t in types)
                {
                    if (t == null || t.Name != "RoomManager")
                        continue;
                    // Il2CppInterop exposes the game's fields as properties, so check those first.
                    foreach (PropertyInfo p in t.GetProperties(AnyStatic))
                    {
                        if (p.GetIndexParameters().Length == 0
                            && (p.Name.StartsWith("field_Internal_Static_ApiWorld_", StringComparison.Ordinal)
                                || p.Name.StartsWith("prop_ApiWorld_", StringComparison.Ordinal)))
                        {
                            _worldMember = p;
                            return;
                        }
                    }
                    foreach (FieldInfo f in t.GetFields(AnyStatic))
                    {
                        if (f.Name.StartsWith("field_Internal_Static_ApiWorld_", StringComparison.Ordinal))
                        {
                            _worldMember = f;
                            return;
                        }
                    }
                }
            }

            _unavailable = true;
            _log?.Warning("Could not find the current-world field; world profiles are unavailable.");
        }

        private static string ReadString(object target, string member)
        {
            Type t = target.GetType();
            PropertyInfo p = t.GetProperty(member, AnyInstance);
            if (p != null && p.PropertyType == typeof(string))
                return (string)p.GetValue(target) ?? string.Empty;
            FieldInfo f = t.GetField(member, AnyInstance);
            if (f != null && f.FieldType == typeof(string))
                return (string)f.GetValue(target) ?? string.Empty;
            return string.Empty;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;
                foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
                        _map[parts[0]] = parts[1];
                }
            }
            catch (Exception ex)
            {
                _log?.Warning("Could not read worlds.txt: {0}", ex.Message);
            }
        }

        private void Persist()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var sb = new StringBuilder();
                foreach (var pair in _map)
                    sb.Append(pair.Key).Append('\t').Append(pair.Value).Append('\n');
                File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _log?.Warning("Could not write worlds.txt: {0}", ex.Message);
            }
        }
    }
}

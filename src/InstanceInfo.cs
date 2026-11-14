using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace VividFrame
{
    /// <summary>
    /// Reads how many players are in the current instance. VRChat's types are reached by reflection
    /// so an obfuscated name changing in a game update only turns this feature off (logged once)
    /// instead of breaking the build or the game.
    /// </summary>
    internal sealed class InstanceInfo
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly MelonLogger.Instance _log;
        private MemberInfo _managerMember;   // static PlayerManager instance
        private MemberInfo _listMember;      // List<Player> on the manager
        private bool _searched;
        private bool _unavailable;
        private bool _loggedError;
        private float _nextRefresh;
        private int _count = -1;

        public InstanceInfo(MelonLogger.Instance log)
        {
            _log = log;
        }

        public bool Available { get { return !_unavailable; } }

        /// <summary>Players in the instance, or -1 when unknown (not in a world, or unavailable).</summary>
        public int PlayerCount
        {
            get
            {
                Refresh();
                return _count;
            }
        }

        private void Refresh()
        {
            if (_unavailable || Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f;
            try
            {
                if (!_searched)
                    Find();
                if (_managerMember == null || _listMember == null)
                    return;

                object manager = ReadMember(_managerMember, null);
                if (manager == null)
                {
                    _count = -1;
                    return;
                }
                object list = ReadMember(_listMember, manager);
                if (list == null)
                {
                    _count = -1;
                    return;
                }
                PropertyInfo countProp = list.GetType().GetProperty("Count", AnyInstance);
                _count = countProp != null ? Convert.ToInt32(countProp.GetValue(list)) : -1;
            }
            catch (Exception ex)
            {
                _count = -1;
                if (!_loggedError)
                {
                    _loggedError = true;
                    _log?.Warning("Player count failed; the overlay is disabled this session: {0}", ex.Message);
                }
                _unavailable = true;
            }
        }

        private void Find()
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
                    if (t == null || t.Name != "PlayerManager")
                        continue;
                    MemberInfo manager = FindStatic(t, "prop_PlayerManager_");
                    MemberInfo list = FindInstance(t, "field_Private_List_1_Player_");
                    if (manager != null && list != null)
                    {
                        _managerMember = manager;
                        _listMember = list;
                        return;
                    }
                }
            }

            _unavailable = true;
            _log?.Warning("Could not find the player list; the player count overlay is unavailable.");
        }

        // Il2CppInterop exposes the game's fields as properties, so both kinds are checked.
        private static MemberInfo FindStatic(Type type, string prefix)
        {
            foreach (PropertyInfo p in type.GetProperties(AnyStatic))
            {
                if (p.Name.StartsWith(prefix, StringComparison.Ordinal) && p.GetIndexParameters().Length == 0)
                    return p;
            }
            foreach (FieldInfo f in type.GetFields(AnyStatic))
            {
                if (f.Name.StartsWith(prefix, StringComparison.Ordinal))
                    return f;
            }
            return null;
        }

        private static MemberInfo FindInstance(Type type, string prefix)
        {
            foreach (PropertyInfo p in type.GetProperties(AnyInstance))
            {
                if (p.Name.StartsWith(prefix, StringComparison.Ordinal) && p.GetIndexParameters().Length == 0)
                    return p;
            }
            foreach (FieldInfo f in type.GetFields(AnyInstance))
            {
                if (f.Name.StartsWith(prefix, StringComparison.Ordinal))
                    return f;
            }
            return null;
        }

        private static object ReadMember(MemberInfo member, object target)
        {
            PropertyInfo p = member as PropertyInfo;
            if (p != null)
                return p.GetValue(target);
            FieldInfo f = member as FieldInfo;
            return f != null ? f.GetValue(target) : null;
        }
    }
}

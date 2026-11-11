using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Reflection lookups into VHVR and Valheim, by <see cref="VhvrApi"/> entry. The plain versions fail loudly: a missing
    /// member throws MissingMemberException naming it, which disables only the module that needed it. The Try versions
    /// return null instead, for things a module can live without.
    ///
    /// Note: Harmony can't patch VHVR methods that read Valheim's private fields (VHVR is compiled against a publicized
    /// Valheim, so the patched copy fails to compile). Only patch methods checked to be free of that
    /// (scripts/Check-VhvrCompat.ps1 checks), and reach anything else through plain delegates (no IL generation).
    /// </summary>
    internal static class Vhvr
    {
        private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>();

        public static Type TryType(ApiEntry entry)
        {
            return TypeByName(entry.Type);
        }

        public static Type Type(ApiEntry entry)
        {
            var t = TryType(entry);
            if (t == null) throw new MissingMemberException($"{entry.Type} not found ({Blame(entry)})");
            return t;
        }

        /// <summary>A method, property getter or property setter, depending on the entry.</summary>
        public static MethodInfo TryMethod(ApiEntry entry)
        {
            var t = TryType(entry);
            if (t == null) return null;
            switch (entry.Shape)
            {
                case ApiShape.Getter: return AccessTools.PropertyGetter(t, entry.Member);
                case ApiShape.Setter: return AccessTools.PropertySetter(t, entry.Member);
                default:
                    return entry.Parameters == null
                        ? AccessTools.Method(t, entry.Member)
                        : AccessTools.Method(t, entry.Member, entry.Parameters.Select(TypeByName).ToArray());
            }
        }

        public static MethodInfo Method(ApiEntry entry)
        {
            Type(entry);
            var m = TryMethod(entry);
            if (m == null) throw new MissingMemberException($"{entry} not found ({Blame(entry)})");
            return m;
        }

        public static FieldInfo TryField(ApiEntry entry)
        {
            var t = TryType(entry);
            return t == null ? null : AccessTools.Field(t, entry.Member);
        }

        public static FieldInfo Field(ApiEntry entry)
        {
            Type(entry);
            var f = TryField(entry);
            if (f == null) throw new MissingMemberException($"{entry} not found ({Blame(entry)})");
            return f;
        }

        public static T Delegate<T>(MethodInfo method) where T : System.Delegate
        {
            return (T)System.Delegate.CreateDelegate(typeof(T), method);
        }

        private static string Blame(ApiEntry entry)
        {
            return entry.Assembly == ApiAssembly.Vhvr ? "VHVR changed?" : "game update?";
        }

        private static Type TypeByName(string fullName)
        {
            if (types.TryGetValue(fullName, out var cached)) return cached;
            var t = AccessTools.TypeByName(fullName);
            if (t != null) types[fullName] = t; // misses aren't cached: a late-loading plugin could still bring the type
            return t;
        }
    }
}

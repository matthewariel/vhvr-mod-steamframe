using System;
using System.Reflection;
using HarmonyLib;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Reflection lookups into VHVR (and Valheim) that fail loudly. A missing member throws MissingMemberException naming it,
    /// which disables only the module that needed it.
    ///
    /// Note: Harmony can't patch VHVR methods that read Valheim's private fields (VHVR is compiled against a publicized
    /// Valheim, so the patched copy fails to compile). Only patch methods checked to be free of that, and reach anything
    /// else through plain delegates (no IL generation).
    /// </summary>
    internal static class Vhvr
    {
        public static Type Type(string fullName)
        {
            var t = AccessTools.TypeByName(fullName);
            if (t == null) throw new MissingMemberException($"{fullName} not found (VHVR changed?)");
            return t;
        }

        public static MethodInfo Method(Type type, string name, params Type[] args)
        {
            var m = args == null || args.Length == 0 ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
            if (m == null) throw new MissingMemberException($"{type.Name}.{name}() not found (VHVR changed?)");
            return m;
        }

        public static MethodInfo Getter(Type type, string property)
        {
            var m = AccessTools.PropertyGetter(type, property);
            if (m == null) throw new MissingMemberException($"{type.Name}.{property} not found (VHVR changed?)");
            return m;
        }

        public static FieldInfo Field(Type type, string name)
        {
            var f = AccessTools.Field(type, name);
            if (f == null) throw new MissingMemberException($"{type.Name}.{name} not found (VHVR changed?)");
            return f;
        }

        public static T Delegate<T>(MethodInfo method) where T : System.Delegate
        {
            return (T)System.Delegate.CreateDelegate(typeof(T), method);
        }
    }
}

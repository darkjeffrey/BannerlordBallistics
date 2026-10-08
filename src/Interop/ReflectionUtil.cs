using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProjectileLandingTracker.Interop
{
    /// <summary>
    /// Reflection helpers for reading game internals (private fields, UI types, engine classes) that are
    /// not part of the public API. Lookups are cached, and every read is defensive: a missing member or a
    /// throwing getter yields <c>null</c> instead of an exception, so a game update can only ever disable
    /// a feature rather than crash the mission. Not thread-safe; use from the game's main thread only.
    /// </summary>
    internal static class ReflectionUtil
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const BindingFlags AnyStatic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> MemberCache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();

        /// <summary>Finds a loaded type by its full name, searching every loaded assembly.</summary>
        public static Type FindType(string fullName)
        {
            if (TypeCache.TryGetValue(fullName, out Type cached))
                return cached;

            Type found = null;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    found = assembly.GetType(fullName, false);
                    if (found != null)
                        break;
                }
            }
            catch (Exception)
            {
                found = null;
            }

            TypeCache[fullName] = found;
            return found;
        }

        /// <summary>Reads a static property or field (public or not); null if it can't be read.</summary>
        public static object GetStatic(Type type, string name)
        {
            if (type == null)
                return null;

            try
            {
                PropertyInfo property = type.GetProperty(name, AnyStatic);
                if (property != null)
                    return property.GetValue(null, null);

                FieldInfo field = type.GetField(name, AnyStatic);
                return field?.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads an instance property or field (public or not) by name, searching base classes too.
        /// Returns null if the member doesn't exist or can't be read.
        /// </summary>
        public static object GetMember(object instance, string name)
        {
            if (instance == null)
                return null;

            MemberInfo member = FindMember(instance.GetType(), name);
            try
            {
                if (member is PropertyInfo property)
                    return property.GetValue(instance, null);
                if (member is FieldInfo field)
                    return field.GetValue(instance);
            }
            catch (Exception)
            {
                // fall through: unreadable member
            }
            return null;
        }

        /// <summary>Finds an instance property by name, searching base classes; null if absent.</summary>
        public static PropertyInfo FindProperty(Type type, string name)
        {
            try
            {
                for (Type t = type; t != null; t = t.BaseType)
                {
                    PropertyInfo property = t.GetProperty(name, DeclaredInstance);
                    if (property != null && property.GetIndexParameters().Length == 0)
                        return property;
                }
            }
            catch (Exception)
            {
                // ambiguous or inaccessible: treat as not found
            }
            return null;
        }

        /// <summary>Enumerates every instance field of a type and its base classes.</summary>
        public static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(DeclaredInstance))
                    yield return field;
            }
        }

        /// <summary>True if the type, or any of its base classes, has the given simple name.</summary>
        public static bool InheritsFromName(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (t.Name == name)
                    return true;
            }
            return false;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            if (!MemberCache.TryGetValue(type, out Dictionary<string, MemberInfo> members))
            {
                members = new Dictionary<string, MemberInfo>();
                MemberCache[type] = members;
            }

            if (members.TryGetValue(name, out MemberInfo cached))
                return cached;

            MemberInfo found = null;
            try
            {
                for (Type t = type; t != null && found == null; t = t.BaseType)
                {
                    PropertyInfo property = t.GetProperty(name, DeclaredInstance);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                        found = property;
                    else
                        found = t.GetField(name, DeclaredInstance);
                }
            }
            catch (Exception)
            {
                found = null;
            }

            members[name] = found;
            return found;
        }
    }
}

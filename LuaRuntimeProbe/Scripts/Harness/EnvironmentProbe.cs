using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace LuaRuntimeProbe.Harness
{
    /// <summary>
    /// Reports the runtime environment a benchmark result must be interpreted
    /// against.  Everything is best-effort and reflective where the symbol name
    /// is not guaranteed, so a missing probe degrades into an explicit
    /// "unavailable" string rather than an exception.
    /// </summary>
    internal static class EnvironmentProbe
    {
        public static List<string> Lines()
        {
            var lines = new List<string>();

            lines.Add("unity=" + Safe(() => Application.unityVersion)
                      + " platform=" + Safe(() => Application.platform.ToString())
                      + " isPlaying=" + Safe(() => Application.isPlaying.ToString())
                      + " frame=" + Safe(() => Time.frameCount.ToString(CultureInfo.InvariantCulture)));

            lines.Add("clr=" + Safe(() => Environment.Version.ToString())
                      + " ptrSize=" + (IntPtr.Size * 8) + "-bit"
                      + " os=" + Safe(() => Environment.OSVersion.ToString()));

            lines.Add("mono=" + MonoDisplayName());
            lines.Add("assembly=Assembly-CSharp " + AssemblyVersion(typeof(GameManager).Assembly));

            foreach (string line in ConstantsFields())
            {
                lines.Add(line);
            }

            string gameVersionPref = TryGamePref("GameVersion");
            if (gameVersionPref != null)
            {
                lines.Add("GamePrefs.GameVersion=" + gameVersionPref);
            }

            lines.Add("gc.totalAllocatedBytes=" + (GcProbe.Available ? "available" : "unavailable on this runtime"));
            return lines;
        }

        private static string MonoDisplayName()
        {
            try
            {
                Type mono = Type.GetType("Mono.Runtime");
                if (mono == null)
                {
                    return "<not Mono>";
                }

                MethodInfo getDisplayName = mono.GetMethod(
                    "GetDisplayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (getDisplayName == null)
                {
                    return "<Mono, no GetDisplayName>";
                }

                object value = getDisplayName.Invoke(null, null);
                return value as string ?? "<unreadable>";
            }
            catch (Exception e)
            {
                return "<probe failed: " + e.GetType().Name + ">";
            }
        }

        private static string AssemblyVersion(Assembly assembly)
        {
            try
            {
                string version = assembly.GetName().Version != null
                    ? assembly.GetName().Version.ToString()
                    : "<null>";

                object[] informational = assembly.GetCustomAttributes(
                    typeof(AssemblyInformationalVersionAttribute), false);

                if (informational != null && informational.Length > 0)
                {
                    var attribute = informational[0] as AssemblyInformationalVersionAttribute;
                    if (attribute != null && !string.IsNullOrEmpty(attribute.InformationalVersion))
                    {
                        return version + " (informational=" + attribute.InformationalVersion + ")";
                    }
                }

                return version;
            }
            catch (Exception e)
            {
                return "<unreadable: " + e.GetType().Name + ">";
            }
        }

        /// <summary>
        /// Reports public static string fields of the game's Constants class whose
        /// name looks like a version/build marker.  Reflection is used on purpose:
        /// the field name is not part of a stable public contract.
        /// </summary>
        private static List<string> ConstantsFields()
        {
            var results = new List<string>();

            Type constants = null;
            try
            {
                constants = typeof(GameManager).Assembly.GetType("Constants", false);
            }
            catch (Exception)
            {
                constants = null;
            }

            if (constants == null)
            {
                results.Add("Constants=<type not found>");
                return results;
            }

            FieldInfo[] fields;
            try
            {
                fields = constants.GetFields(BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception e)
            {
                results.Add("Constants=<fields unreadable: " + e.GetType().Name + ">");
                return results;
            }

            int found = 0;
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                string lower = field.Name.ToLowerInvariant();
                if (lower.IndexOf("version", StringComparison.Ordinal) < 0
                    && lower.IndexOf("build", StringComparison.Ordinal) < 0
                    && lower.IndexOf("commit", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                string value;
                try
                {
                    value = field.GetValue(null) as string;
                }
                catch (Exception)
                {
                    value = "<unreadable>";
                }

                found++;
                results.Add("Constants." + field.Name + "=" + value);
            }

            if (found == 0)
            {
                results.Add("Constants=<no version/build/commit string fields found>");
            }

            return results;
        }

        /// <summary>
        /// Reads one EnumGamePrefs value by NAME so that a renamed enum member
        /// cannot break compilation.
        /// </summary>
        private static string TryGamePref(string prefName)
        {
            try
            {
                Type enumType = typeof(GamePrefs).Assembly.GetType("EnumGamePrefs", false);
                if (enumType == null)
                {
                    return null;
                }

                object value = Enum.Parse(enumType, prefName, false);
                MethodInfo getString = typeof(GamePrefs).GetMethod("GetString", new[] { enumType });
                if (getString == null)
                {
                    return null;
                }

                return getString.Invoke(null, new[] { value }) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Safe(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                return "<" + e.GetType().Name + ">";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace LuaRuntimeProbe.Backends
{
    /// <summary>
    /// Loads native DLLs from an explicit path before the first P/Invoke, keyed by
    /// library file name.
    ///
    /// Why this is needed: KeraLua declares [DllImport("lua54")] and xLua declares
    /// [DllImport("xlua")], and the mod's native\ folder is NOT on the process DLL
    /// search path, so the first Lua call would fail with DllNotFoundException.
    /// Pre-loading by full path makes the later implicit lookup resolve to the
    /// already loaded module.
    ///
    /// IMPORTANT: the bookkeeping is per library.  A single global handle would make
    /// the second backend's preload a no-op and produce
    /// "DllNotFoundException: xlua" even though the file exists - which is exactly
    /// what happened the first time this ran with more than one native runtime.
    ///
    /// SCOPE: LOAD_WITH_ALTERED_SEARCH_PATH makes the module's OWN directory the first
    /// place its dependencies are looked up.  SetDllDirectoryW is deliberately NOT
    /// used, because it would permanently change the DLL search policy of the whole
    /// game process and affect every other mod.
    ///
    /// LIFETIME: modules are intentionally never freed.  Unloading a native library
    /// that managed code may still P/Invoke into would crash the process; the handles
    /// therefore live as long as the game process does.
    /// </summary>
    internal static class NativeLibraryLoader
    {
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameW(IntPtr hModule, [Out] char[] lpFilename, uint nSize);

        private static readonly Dictionary<string, IntPtr> Loaded = new(StringComparer.OrdinalIgnoreCase);

        private static readonly object SyncRoot = new();

        /// <summary>True when the named library has already been loaded by this process.</summary>
        public static bool IsLoaded(string libraryFileName)
        {
            lock (SyncRoot)
            {
                return !string.IsNullOrEmpty(libraryFileName) && Loaded.ContainsKey(libraryFileName);
            }
        }

        public static bool TryGetHandle(string libraryFileName, out IntPtr handle)
        {
            lock (SyncRoot)
            {
                return Loaded.TryGetValue(libraryFileName ?? string.Empty, out handle);
            }
        }

        public static bool TryLoad(string nativeDirectory, string libraryFileName, out string detail)
        {
            detail = null;

            if (string.IsNullOrEmpty(libraryFileName))
            {
                detail = "no library name given";
                return false;
            }

            lock (SyncRoot)
            {
                IntPtr existing;
                if (Loaded.TryGetValue(libraryFileName, out existing))
                {
                    detail = libraryFileName + " already loaded (handle=0x" + existing.ToInt64().ToString("X") + ")";
                    return true;
                }

                if (string.IsNullOrEmpty(nativeDirectory))
                {
                    detail = "native directory could not be resolved";
                    return false;
                }

                string full = Path.Combine(nativeDirectory, libraryFileName);
                if (!File.Exists(full))
                {
                    detail = "native library not found: " + full;
                    return false;
                }

                IntPtr handle = LoadLibraryExW(full, IntPtr.Zero, LoadWithAlteredSearchPath);
                if (handle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    detail = "LoadLibraryExW failed for " + full + " (Win32 error " + err + ")";
                    return false;
                }

                Loaded[libraryFileName] = handle;
                detail = "loaded " + full + " -> " + ResolveModulePath(handle)
                         + " (LOAD_WITH_ALTERED_SEARCH_PATH, no process-wide DLL directory change)";
                return true;
            }
        }

        /// <summary>One line per loaded native library, for the status report.</summary>
        public static string Describe()
        {
            lock (SyncRoot)
            {
                if (Loaded.Count == 0)
                {
                    return "<none>";
                }

                var parts = new List<string>();
                foreach (KeyValuePair<string, IntPtr> pair in Loaded)
                {
                    parts.Add(pair.Key + "=0x" + pair.Value.ToInt64().ToString("X"));
                }

                return string.Join(", ", parts.ToArray());
            }
        }

        private static string ResolveModulePath(IntPtr module)
        {
            try
            {
                var buffer = new char[1024];
                uint len = GetModuleFileNameW(module, buffer, (uint)buffer.Length);
                if (len == 0)
                {
                    return "<unresolved: GetModuleFileNameW returned 0, Win32 error " + Marshal.GetLastWin32Error() + ">";
                }

                if (len >= buffer.Length)
                {
                    return "<unresolved: path longer than " + buffer.Length + " characters>";
                }

                return new string(buffer, 0, (int)len);
            }
            catch (Exception e)
            {
                return "<unresolved: " + e.GetType().Name + ">";
            }
        }
    }
}

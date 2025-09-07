using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;

namespace KFCommonUtilityLib
{
    public static class DelayLoadModuleManager
    {
        private static readonly List<(string mod, List<string> dlls)> list_delay_load = new List<(string mod, List<string> dlls)>();

        private static readonly List<Assembly> loaded = new List<Assembly>();

        public static void RegisterDelayloadDll(string modName, string dllNameWithoutExtension)
        {
            List<string> dlls;
            int index = list_delay_load.FindIndex(p => p.mod == modName);
            if (index < 0)
            {
                dlls = new List<string>() { dllNameWithoutExtension };
                list_delay_load.Add((modName, dlls));
                return;
            }
            dlls = list_delay_load[index].dlls;
            dlls.Add(dllNameWithoutExtension);
        }

        public static void DelayLoad(ref ModEvents.SGameAwakeData _)
        {
            Assembly assembly = Assembly.GetAssembly(typeof(DelayLoadModuleManager));
            Mod mod = ModManager.GetModForAssembly(assembly);
            string delayLoadFolder = mod.Path + "/DelayLoad";
            ModuleManagers.AddAssemblySearchPath(delayLoadFolder);
            foreach (var pair in list_delay_load)
            {
                if (ModManager.GetLoadedAssemblies().Any(a => a.GetName().Name == pair.mod))
                {
                    foreach (var dll in pair.dlls)
                    {
                        try
                        {
                            string assPath = Path.GetFullPath(delayLoadFolder + $"/{dll}.dll");
                            Assembly patch = Assembly.LoadFrom(assPath);
                            if (Path.GetFullPath(patch.Location).Equals(assPath, StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var type in patch.GetTypes())
                                {
                                    if (typeof(IModApi).IsAssignableFrom(type))
                                    {
                                        IModApi modApi = (IModApi)Activator.CreateInstance(type);
                                        modApi.InitMod(mod);
                                        Log.Out(string.Concat($"[DELAYLOAD] Initialized code in {dll}.dll"));
                                    }
                                }
                                mod.allAssemblies.Add(patch);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[DELAYLOAD] Failed loading DLL {dll}.dll");
                            Log.Exception(ex);
                        }
                    }
                }
            }
            //if (ModManager.GetLoadedAssemblies().FirstOrDefault(a => a.GetName().Name == "FullautoLauncher") != null)
            //{
            //    try
            //    {
            //        string assPath = Path.GetFullPath(delayLoadFolder + "/FullautoLauncherAnimationRiggingCompatibilityPatch.dll");
            //        Assembly patch = Assembly.LoadFrom(assPath);
            //        if (Path.GetFullPath(patch.Location).Equals(assPath, StringComparison.OrdinalIgnoreCase))
            //        {
            //            Type apiType = typeof(IModApi);
            //            foreach (var type in patch.GetTypes())
            //            {
            //                if (apiType.IsAssignableFrom(type))
            //                {
            //                    IModApi modApi = (IModApi)Activator.CreateInstance(type);
            //                    modApi.InitMod(mod);
            //                    Log.Out(string.Concat("[DELAYLOAD] Initialized code in FullautoLauncherAnimationRiggingCompatibilityPatch.dll"));
            //                }
            //            }
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Log.Error("[DELAYLOAD] Failed loading DLL FullautoLauncherAnimationRiggingCompatibilityPatch.dll");
            //        Log.Exception(ex);
            //    }
            //}
        }
    }
}

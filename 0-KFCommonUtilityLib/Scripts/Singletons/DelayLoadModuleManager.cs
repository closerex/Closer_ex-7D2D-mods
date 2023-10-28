using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public static class DelayLoadModuleManager
    {
        private static readonly List<Assembly> loaded = new List<Assembly>();

        public static void DelayLoad()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(DelayLoadModuleManager));
            Mod mod = ModManager.GetModForAssembly(assembly);
            string delayLoadFolder = mod.Path + "/DelayLoad";

            if (ModManager.GetMod("FullautoLauncher", true) != null)
            {
                try
                {
                    Assembly patch = Assembly.LoadFrom(delayLoadFolder + "/FullautoLauncherAnimationRiggingCompatibilityPatch.dll");
                    Type apiType = typeof(IModApi);
                    foreach (var type in patch.GetTypes())
                    {
                        if (apiType.IsAssignableFrom(type))
                        {
                            IModApi modApi = (IModApi)Activator.CreateInstance(type);
                            modApi.InitMod(mod);
                            Log.Out(string.Concat("[DELAYLOAD] Initialized code in FullautoLauncherAnimationRiggingCompatibilityPatch.dll"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[DELAYLOAD] Failed loading DLL FullautoLauncherAnimationRiggingCompatibilityPatch.dll");
                    Log.Exception(ex);
                }
            }
        }
    }
}

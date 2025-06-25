using HarmonyLib;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;

namespace KeepReloading
{
    public class KeepReloadingInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
            {
                return;
            }
            inited = true;

            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class KeepReloadingPatches
    {
        //[HarmonyPatch(typeof(XUiC_CameraWindow), nameof(XUiC_CameraWindow.OnOpen))]
        [HarmonyPatch(typeof(XUiC_BackpackWindow), nameof(XUiC_BackpackWindow.OnOpen))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_CameraWindow_OnOpen(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_cancel = AccessTools.Method(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.CancelInventoryActions));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_cancel))
                {
                    var fld_gm = AccessTools.Field(typeof(GameManager), nameof(GameManager.Instance));
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].LoadsField(fld_gm))
                        {
                            codes[i + 3].WithLabels(codes[j].ExtractLabels());
                            codes.RemoveRange(j, i - j + 3);
                            break;
                        }
                    }
                    break;
                }
            }

            return codes;
        }
    }
}

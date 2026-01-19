using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class EventHookPatches
    {
        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.handleReceivedConfigs), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_handleReceivedConfigs_WorldStaticData(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_getstring = AccessTools.Method(typeof(GamePrefs), nameof(GamePrefs.GetString));
            var fld_co = AccessTools.Field(typeof(WorldStaticData), nameof(WorldStaticData.receivedConfigsHandlerCoroutine));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_getstring))
                {
                    for (int j = i + 1; j < codes.Count - 2; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldarg_0 && codes[j + 1].LoadsConstant(0) && codes[j + 2].opcode == OpCodes.Stfld)
                        {
                            codes.InsertRange(j, new[]
                            {
                            CodeInstruction.Call(typeof(KFLibEvents), nameof(KFLibEvents.XmlLoadingStart)).WithLabels(codes[j].ExtractLabels()),
                        });
                            break;
                        }
                    }
                    break;
                }
            }

            for (int i = codes.Count - 1; i >= 0; i--)
            {
                if (codes[i].StoresField(fld_co))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                    CodeInstruction.Call(typeof(KFLibEvents), nameof(KFLibEvents.XmlLoadingFinish)).WithLabels(codes[i + 1].ExtractLabels()),
                });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.LoadAllXmlsCo), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_LoadAllXmlsCo_WorldStaticData(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_co = AccessTools.Field(typeof(WorldStaticData), nameof(WorldStaticData.LoadAllXmlsCoComplete));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].StoresField(fld_co))
                {
                    if (codes[i - 1].LoadsConstant(0))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                        CodeInstruction.Call(typeof(KFLibEvents), nameof(KFLibEvents.XmlLoadingStart)).WithLabels(codes[i + 1].ExtractLabels()),
                    });
                        i++;
                    }
                    else if (codes[i - 1].LoadsConstant(1))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                        CodeInstruction.Call(typeof(KFLibEvents), nameof(KFLibEvents.XmlLoadingFinish)).WithLabels(codes[i + 1].ExtractLabels()),
                    });
                        i++;
                    }
                }
            }
            return codes;
        }
    }
}

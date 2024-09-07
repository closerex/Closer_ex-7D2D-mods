using Audio;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterModCompatibility.Harmony
{
    [HarmonyPatch]
    static class AudioDictionaryPatches
    {
        //override existing sound node instead of throwing exception
        [HarmonyPatch(typeof(Manager), nameof(Manager.AddAudioData))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_AddAudioData_Manager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_add = AccessTools.Method(typeof(Dictionary<string, XmlData>), nameof(Dictionary<string, XmlData>.Add));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_add))
                {
                    codes[i].operand = AccessTools.Method(typeof(Dictionary<string, XmlData>), "set_Item");
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(AIDirectorData), nameof(AIDirectorData.AddNoisySound))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_AddNoisySound_AIDirectorData(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_add = AccessTools.Method(typeof(Dictionary<string, AIDirectorData.Noise>), nameof(Dictionary<string, AIDirectorData.Noise>.Add));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_add))
                {
                    codes[i].operand = AccessTools.Method(typeof(Dictionary<string, AIDirectorData.Noise>), "set_Item");
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(AIDirectorData), nameof(AIDirectorData.AddSmell))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_AddSmell_AIDirectorData(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_add = AccessTools.Method(typeof(Dictionary<string, AIDirectorData.Smell>), nameof(Dictionary<string, AIDirectorData.Smell>.Add));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_add))
                {
                    codes[i].operand = AccessTools.Method(typeof(Dictionary<string, AIDirectorData.Smell>), "set_Item");
                    break;
                }
            }
            return codes;
        }
    }
}

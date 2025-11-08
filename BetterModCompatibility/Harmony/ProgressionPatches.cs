using HarmonyLib;
using System.Collections.Generic;

namespace BetterModCompatibility.Harmony
{
    [HarmonyPatch]
    public static class ProgressionPatches
    {
        [HarmonyPatch(typeof(ProgressionFromXml), nameof(ProgressionFromXml.parseProgressionItem))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ProgressionFromXml_parseProgressionItem(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(Dictionary<string, ProgressionClass>), nameof(Dictionary<string, ProgressionClass>.Add)),
                                               AccessTools.IndexerSetter(typeof(Dictionary<string, ProgressionClass>), new[] { typeof(string) }));
        }
    }
}

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UniLinq;

[HarmonyPatch]
static class LogAndContinuePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return new MethodInfo[]
        {
            AccessTools.Method(typeof(LootFromXml), nameof(LootFromXml.LoadLootContainer)),
            AccessTools.Method(typeof(LootFromXml), nameof(LootFromXml.LoadLootGroup)),
            AccessTools.Method(typeof(LootFromXml), nameof(LootFromXml.LoadLootProbabilityTemplate)),
            AccessTools.Method(typeof(LootFromXml), nameof(LootFromXml.LoadLootQualityTemplate)),
            AccessTools.Method(typeof(LootFromXml), nameof(LootFromXml.ParseItemList)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.ParseTierItems)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.parseItemList)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.ParseNode)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.ParseTraderInfo)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.ParseTraderItemGroup)),
            AccessTools.Method(typeof(TradersFromXml), nameof(TradersFromXml.ParseTraderStageTemplate)),

        };
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_enumerator = AccessTools.Method(typeof(IEnumerable<XElement>), nameof(IEnumerable<XElement>.GetEnumerator), Array.Empty<Type>());
        object lbl_loop = null;
        for (int i = 2; i < codes.Count; i++)
        {
            if ((codes[i].opcode == OpCodes.Br || codes[i].opcode == OpCodes.Br_S) && codes[i - 2].Calls(mtd_enumerator))
            {
                lbl_loop = codes[i].operand;
            }
            else if (codes[i].opcode == OpCodes.Throw)
            {
                codes.RemoveRange(i - 1, 2);
                codes.InsertRange(i - 1, new[]
                {
                    CodeInstruction.Call(typeof(XmlPatchHelpers), nameof(XmlPatchHelpers.LogInsteadOfThrow)),
                    lbl_loop == null ? new CodeInstruction(OpCodes.Ret) : new CodeInstruction(OpCodes.Br, lbl_loop)
                });
            }
        }
        return codes;
    }
}
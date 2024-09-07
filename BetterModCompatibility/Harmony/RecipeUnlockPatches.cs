using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

namespace BetterModCompatibility.Harmony
{
    [HarmonyPatch]
    static class RecipeUnlockPatches
    {
        private static HashSet<int> forceUnlockedRecipes = new HashSet<int>();

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.UnlockedBy), MethodType.Getter)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemClass_UnlockedBy(IEnumerable<CodeInstruction> instructions)
        {
            using (var enumerator = instructions.GetEnumerator())
            {
                while(enumerator.MoveNext())
                {
                    var instr = enumerator.Current;
                    if (instr.opcode == OpCodes.Stloc_0)
                    {
                        yield return CodeInstruction.Call(typeof(RecipeUnlockPatches), nameof(RecipeUnlockPatches.FindValidEntries));
                        yield return instr;
                        enumerator.MoveNext();
                        enumerator.MoveNext();
                        enumerator.MoveNext();
                        continue;
                    }
                    yield return instr;
                }
            }
        }

        [HarmonyPatch(typeof(Block), nameof(Block.UnlockedBy), MethodType.Getter)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Block_UnlockedBy(IEnumerable<CodeInstruction> instructions)
        {
            using (var enumerator = instructions.GetEnumerator())
            {
                while(enumerator.MoveNext())
                {
                    var instr = enumerator.Current;
                    if (instr.opcode == OpCodes.Stloc_0)
                    {
                        yield return CodeInstruction.Call(typeof(RecipeUnlockPatches), nameof(RecipeUnlockPatches.FindValidEntries));
                        yield return instr;
                        enumerator.MoveNext();
                        enumerator.MoveNext();
                        enumerator.MoveNext();
                        continue;
                    }
                    yield return instr;
                }
            }
        }

        private static string[] FindValidEntries(string[] unlockItem)
        {
            if (unlockItem.Length == 0)
                return unlockItem;

            List<string> validItems = new List<string>();
            foreach (var item in unlockItem)
            {
                if (ItemClass.GetItemClass(item) != null || Progression.ProgressionClasses.ContainsKey(item))
                {
                    validItems.Add(item);
                }
                else
                {
                    Log.Warning($"Unlock item {item} is not found!");
                }
            }
            return validItems.ToArray();
        }

        [HarmonyPatch(typeof(Recipe), nameof(Recipe.Init))]
        [HarmonyPostfix]
        private static void Postfix_Recipe_Init(Recipe __instance)
        {
            var item = ItemClass.GetForId(__instance.itemValueType);

            if (((!item.IsBlock() && (item.UnlockedBy == null || item.UnlockedBy.Length == 0))
                 || (item.IsBlock() && (item.GetBlock().UnlockedBy == null || item.GetBlock().UnlockedBy.Length == 0)))
                && __instance.IsLearnable)
            {
                Log.Out($"All unlock conditions for {item.Name} are missing, it's now unlocked!");
                __instance.IsLearnable = false;
                forceUnlockedRecipes.Add(__instance.itemValueType);
            }
        }

        [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetCraftingTier))]
        [HarmonyPostfix]
        private static void Postfix_Recipe_GetCraftingTier(ref int __result, Recipe __instance)
        {
            if (!__instance.IsLearnable && forceUnlockedRecipes.Contains(__instance.itemValueType))
            {
                __result = 6;
            }
        }

        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.GetBindingValue))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_CraftingInfoWindow_GetBindingValue(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_getvalue))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.recipe)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(XUiController), "get_xui"),
                        CodeInstruction.Call(typeof(XUi), "get_playerUI"),
                        CodeInstruction.Call(typeof(LocalPlayerUI), "get_entityPlayer"),
                        CodeInstruction.Call(typeof(Recipe), nameof(Recipe.GetCraftingTier))
                    });
                    codes.RemoveRange(i - 20, 22);
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInitAll))]
        [HarmonyPostfix]
        private static void Postfix_ItemClass_LateInitAll()
        {
            forceUnlockedRecipes.Clear();
        }
    }
}

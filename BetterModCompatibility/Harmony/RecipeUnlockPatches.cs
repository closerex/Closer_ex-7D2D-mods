using Challenges;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
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
                if (ItemClass.GetItemClass(item) != null || Progression.ProgressionClasses.ContainsKey(item) || ChallengeGroup.s_ChallengeGroups.ContainsKey(item.ToLower()) || ChallengeClass.s_Challenges.ContainsKey(item.ToLower()))
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

        private static bool CheckProgressionUnlock(ItemClass item)
        {
            if (item == null)
                return false;
            var itemNameTag = FastTags<TagGroup.Global>.Parse(item.Name);
            foreach (var progression in Progression.ProgressionClasses.Values)
            {
                if (progression != null && progression.Effects != null && progression.Effects.PassivesIndex.Contains(PassiveEffects.RecipeTagUnlocked))
                {
                    foreach (var controller in progression.Effects.EffectGroups)
                    {
                        if (controller.PassiveEffects.Count > 0)
                        {
                            foreach (var passive in controller.PassiveEffects)
                            {
                                if (passive.Type == PassiveEffects.RecipeTagUnlocked && passive.Tags.Test_AnySet(itemNameTag))
                                {
                                    return true;
                                }
                            }
                        }

                    }
                }
            }
            return false;
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

        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.handleReceivedConfigs), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_WorldStaticData_handleReceivedConfigs(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_co = AccessTools.Field(typeof(WorldStaticData), nameof(WorldStaticData.receivedConfigsHandlerCoroutine));

            for (int i = codes.Count; i < 0; i--)
            {
                if (codes[i].StoresField(fld_co))
                {
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(RecipeUnlockPatches), nameof(CheckRecipeUnlockConditions)));
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.LoadAllXmlsCo), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_WorldStaticData_LoadAllXmlsCo(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_complete = AccessTools.Field(typeof(WorldStaticData), nameof(WorldStaticData.LoadAllXmlsCoComplete));

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].StoresField(fld_complete) && codes[i - 1].opcode == OpCodes.Ldc_I4_1)
                {
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(RecipeUnlockPatches), nameof(CheckRecipeUnlockConditions)));
                    break;
                }
            }

            return codes;
        }

        private static void CheckRecipeUnlockConditions()
        {
            foreach (var recipe in CraftingManager.GetAllRecipes())
            {
                var item = ItemClass.GetForId(recipe.itemValueType);

                if (((!item.IsBlock() && (item.UnlockedBy == null || item.UnlockedBy.Length == 0))
                     || (item.IsBlock() && (item.GetBlock().UnlockedBy == null || item.GetBlock().UnlockedBy.Length == 0)))
                    && recipe.IsLearnable && !CheckProgressionUnlock(item))
                {
                    Log.Out($"All unlock conditions for {item.Name} are missing, it's now unlocked!");
                    recipe.IsLearnable = false;
                    forceUnlockedRecipes.Add(recipe.itemValueType);
                }
            }
        }

        [HarmonyPatch]
        public static class GetBindingValuePatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.CompareTo(new VersionInformation(VersionInformation.EGameReleaseType.V, 2, 3, 0)) < 0)
                {
                    Log.Out($"Choosing old GetBindingValue for XUiC_CraftingInfoWindow for game version {Constants.cVersionInformation.Major}.{Constants.cVersionInformation.Minor}");
                    yield return AccessTools.Method(typeof(XUiC_CraftingInfoWindow), "GetBindingValue");
                }
                else
                {
                    Log.Out($"Choosing new GetBindingValueInternal for XUiC_CraftingInfoWindow for game version {Constants.cVersionInformation.Major}.{Constants.cVersionInformation.Minor}");
                    yield return AccessTools.Method(typeof(XUiC_CraftingInfoWindow), "GetBindingValueInternal");
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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
        }

        [HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.RefreshEnabled))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionEntryCraft_RefreshEnabled(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_crafting_tier = AccessTools.Field(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.craftingTier));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_crafting_tier))
                {
                    codes.RemoveRange(i + 1, 21);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(BaseItemActionEntry), "get_ItemController"),
                        CodeInstruction.Call(typeof(XUiController), "get_xui"),
                        CodeInstruction.Call(typeof(XUi), "get_playerUI"),
                        CodeInstruction.Call(typeof(LocalPlayerUI), "get_entityPlayer"),
                        CodeInstruction.Call(typeof(Recipe), nameof(Recipe.GetCraftingTier))
                    });
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

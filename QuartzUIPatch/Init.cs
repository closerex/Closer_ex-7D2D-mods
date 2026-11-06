using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace QuartzUIPatch
{
    public class Init : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            Log.Out("Loading KFLib QuartzUI Patch");
            // Register the patch
            Harmony harmony = new Harmony("com.example.quartzui.patch");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(UIDisplayInfoFromXmlPatch), nameof(UIDisplayInfoFromXmlPatch.ParseDisplayInfoEntry))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_UIDisplayInfoFromXmlPatch_ParseDisplayInfoEntry(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }),
                                               AccessTools.Method(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.GetEnumOrThrow), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) }));
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "HasChanged")]
        [HarmonyPostfix]
        private static void Postfix_XUiC_HUDActiveItem_HasChanged(EntityPlayer ___localPlayer, ref bool __result)
        {
            if (!__result)
            {
                __result |= ___localPlayer.inventory.holdingItem?.Actions?[0]?.IsStatChanged() ?? false;
            }
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "SetupActiveItemEntry")]
        [HarmonyPrefix]
        private static void Prefix_SetupActiveItemEntry_XUiC_HUDStatBar(EntityPlayer ___localPlayer, out ItemActionData __state)
        {
            if (___localPlayer == null)
            {
                __state = null;
                return;
            }
            __state = ___localPlayer.MinEventContext.ItemActionData;
            MultiActionUtils.SetMinEventParamsByEntityInventory(___localPlayer);
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "SetupActiveItemEntry")]
        [HarmonyPostfix]
        private static void Postfix_SetupActiveItemEntry_XUiC_HUDStatBar(EntityPlayer ___localPlayer, ItemActionData __state)
        {
            if (___localPlayer != null)
            {
                ___localPlayer.MinEventContext.ItemActionData = __state;
            }
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "SetupActiveItemEntry")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_HUDActiveItem_SetupActiveItemEntry(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_index = generator.DeclareLocal(typeof(int));
            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            MethodInfo mtd_ammoindex = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_action) && codes[i + 1].LoadsConstant(0))
                {
                    codes[i + 1].opcode = OpCodes.Ldarg_0;
                    codes[i + 1].operand = null;
                    codes.InsertRange(i + 2, new[]
                    {
                        CodeInstruction.LoadField(typeof(Quartz.XUiC_HUDActiveItem), "itemValue"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 5;
                }
                else if (codes[i].Calls(mtd_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index).MoveLabelsFrom(codes[i]));
                    i++;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), nameof(Quartz.XUiC_HUDActiveItem.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_HUDStatBar_Update(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_getfocus = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetFocusedItemIdx));
            MethodInfo mtd_getholding = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemIdx));

            foreach (var ins in instructions)
            {
                if (ins.Calls(mtd_getfocus))
                {
                    ins.operand = mtd_getholding;
                }
                yield return ins;
            }
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), nameof(Quartz.XUiC_HUDActiveItem.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_HUDActiveItem_Update(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(Inventory), nameof(Inventory.GetFocusedItemIdx)),
                                               AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemIdx)));
        }

        [HarmonyPatch(typeof(Quartz.XUiC_ItemInfoWindow), "GetAmmoName")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_ItemInfoWindow_GetAmmoName(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_index = generator.DeclareLocal(typeof(int));
            var fld_actions = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            var mtd_ammoindex = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_actions) && codes[i + 1].LoadsConstant(0))
                {
                    codes[i + 1].opcode = OpCodes.Ldarg_0;
                    codes[i + 1].operand = null;
                    codes.InsertRange(i + 2, new[]
                    {
                        CodeInstruction.LoadField(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.itemStack)),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ItemStack), nameof(ItemStack.itemValue))),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 6;
                }
                else if (codes[i].Calls(mtd_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index).MoveLabelsFrom(codes[i]));
                    i++;
                }
            }

            return codes;
        }

        #region action stat display
        [HarmonyPatch]
        private static class StatValueBindingsPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Quartz.XUiC_HUDActiveItem), "GetLoadedAmmo");
                yield return AccessTools.PropertyGetter(typeof(Quartz.XUiC_HUDActiveItem), nameof(Quartz.XUiC_HUDActiveItem.EntityDamage));
                yield return AccessTools.PropertyGetter(typeof(Quartz.XUiC_HUDActiveItem), nameof(Quartz.XUiC_HUDActiveItem.BlockDamage));
            }

            [HarmonyPostfix]
            private static void Postfix(Quartz.XUiC_HUDActiveItem __instance, EntityPlayer ___localPlayer, ref string __result)
            {
                var displayAsHUDStat = GetDisplayAsHUDStatInterface(__instance, ___localPlayer);
                if (displayAsHUDStat != null)
                {
                    __result = displayAsHUDStat.GetHUDStatValue(___localPlayer.inventory.holdingItemData);
                }
            }
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "GetStatIcon")]
        [HarmonyPostfix]
        private static void Postfix_XUiC_HUDActiveItem_GetStatIcon(Quartz.XUiC_HUDActiveItem __instance, EntityPlayer ___localPlayer, ref string __result)
        {
            var displayAsHUDStat = GetDisplayAsHUDStatInterface(__instance, ___localPlayer);
            displayAsHUDStat?.GetIconOverride(___localPlayer.inventory.holdingItemData, ref __result);
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "GetStatIconColor")]
        [HarmonyPostfix]
        private static void Postfix_XUiC_HUDActiveItem_GetStatIconColor(Quartz.XUiC_HUDActiveItem __instance, EntityPlayer ___localPlayer, ref UnityEngine.Color32 __result)
        {
            var displayAsHUDStat = GetDisplayAsHUDStatInterface(__instance, ___localPlayer);
            displayAsHUDStat?.GetIconTintOverride(___localPlayer.inventory.holdingItemData, ref __result);
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "updateActiveItemAmmo")]
        [HarmonyPrefix]
        private static bool Prefix_updateActiveItemAmmo_XUiC_HUDStatBar(Quartz.XUiC_HUDActiveItem __instance, EntityPlayer ___localPlayer, ref int ___currentAmmoCount)
        {
            var displayAsHUDStat = GetDisplayAsHUDStatInterface(__instance, ___localPlayer);
            if (displayAsHUDStat != null)
            {
                int currentAmmoCount = ___currentAmmoCount;
                if (displayAsHUDStat.UpdateActiveItemAmmo(___localPlayer.inventory.holdingItemData, ref currentAmmoCount))
                {
                    ___currentAmmoCount = currentAmmoCount;
                    return false;
                }
            }
            return true;
        }

        private static IDisplayAsHUDStat GetDisplayAsHUDStatInterface(Quartz.XUiC_HUDActiveItem statBar, EntityPlayer localPlayer)
        {
            if (statBar == null || localPlayer == null)
            {
                return null;
            }

            int actionIndex = MultiActionManager.GetActionIndexForEntity(localPlayer);
            if (actionIndex >= 0 && actionIndex < localPlayer.inventory.holdingItem.Actions.Length)
            {
                return localPlayer.inventory.holdingItem.Actions[actionIndex].LoadModule<IDisplayAsHUDStat>();
            }
            return null;
        }
        #endregion
    }
}

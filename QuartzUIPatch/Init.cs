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
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_action))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(Quartz.XUiC_HUDActiveItem), "itemValue"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 4;
                }
                else if (codes[i].LoadsField(fld_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    break;
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
        private static IEnumerable<CodeInstruction> Transpiler_XUiC_ItemInfoWindow_GetAmmoName(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_actions = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_actions) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.itemStack)),
                        CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData))
                    });
                    i += 3;
                }
            }

            return codes;
        }

        #region action stat display
        [HarmonyPatch]
        private static class GetBindingValueInternalPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 2))
                {
                    yield return AccessTools.Method(typeof(Quartz.XUiC_HUDActiveItem), "GetBindingValue");
                }
                else
                {
                    yield return AccessTools.Method(typeof(Quartz.XUiC_HUDActiveItem), "GetBindingValueInternal");
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var codes = instructions.ToList();

                var mtd_gettotal = AccessTools.Method(typeof(Quartz.XUiC_HUDActiveItem), "GetTotalAmmo");
                var mtd_getloaded = AccessTools.Method(typeof(Quartz.XUiC_HUDActiveItem), "GetLoadedAmmo");
                var mtd_geticon = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.GetIconName));
                var mtd_geticontint = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.GetIconTint));
                var mtd_getstat = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatValue));
                var mtd_getstatwithmax = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatValueWithMax));
                var mtd_getstatfill = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatFillFraction));
                var mtd_geticonoverride = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetIconOverride));
                var mtd_gettintoverride = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetIconTintOverride));
                var mtd_holdingitemdata = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemData));
                var fld_blockdmg = AccessTools.Field(typeof(Quartz.XUiC_HUDActiveItem), "blockDamage");
                var fld_entitydmg = AccessTools.Field(typeof(Quartz.XUiC_HUDActiveItem), "entityDamage");
                var fld_localplayer = AccessTools.Field(typeof(Quartz.XUiC_HUDActiveItem), "localPlayer");
                var fld_inventory = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.inventory));

                var lbd_stat = generator.DeclareLocal(typeof(IDisplayAsHUDStat));

                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(mtd_getloaded) || codes[i].LoadsField(fld_blockdmg) || codes[i].LoadsField(fld_entitydmg))
                    {
                        Label lbl = generator.DefineLabel(), lbl_store = generator.DefineLabel();
                        codes[i - 1].WithLabels(lbl);
                        codes[i + 1].WithLabels(lbl_store);
                        codes.InsertRange(i - 1, new[]
                        {
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            new CodeInstruction(OpCodes.Brfalse, lbl),
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            CodeInstruction.LoadArgument(0),
                            new CodeInstruction(OpCodes.Ldfld, fld_localplayer),
                            new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                            new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                            new CodeInstruction(OpCodes.Callvirt, mtd_getstat),
                            new CodeInstruction(OpCodes.Br, lbl_store)
                        });
                        i += 9;
                        Log.Out("quartz stat value patched!");
                    }
                    else if (codes[i].Calls(mtd_geticon))
                    {
                        Label lbl = generator.DefineLabel();
                        codes[i + 2].WithLabels(lbl);
                        codes.InsertRange(i + 2, new[]
                        {
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            new CodeInstruction(OpCodes.Brfalse, lbl),
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            CodeInstruction.LoadArgument(0),
                            new CodeInstruction(OpCodes.Ldfld, fld_localplayer),
                            new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                            new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                            CodeInstruction.LoadArgument(1),
                            new CodeInstruction(OpCodes.Callvirt, mtd_geticonoverride),
                        });
                        i += 9;
                        Log.Out("quartz icon override patched!");
                    }
                    else if (codes[i].Calls(mtd_geticontint))
                    {
                        Label lbl = generator.DefineLabel();
                        codes[i + 3].WithLabels(lbl);
                        codes.InsertRange(i + 3, new[]
                        {
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            new CodeInstruction(OpCodes.Brfalse, lbl),
                            CodeInstruction.LoadLocal(lbd_stat.LocalIndex),
                            CodeInstruction.LoadArgument(0),
                            new CodeInstruction(OpCodes.Ldfld, fld_localplayer),
                            new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                            new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                            CodeInstruction.LoadLocal(0, true),
                            new CodeInstruction(OpCodes.Callvirt, mtd_gettintoverride),
                        });
                        i += 9;
                        Log.Out("quartz tint override patched!");
                    }
                }

                codes.InsertRange(2, new[]
                {
                    CodeInstruction.LoadArgument(0),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Ldfld, fld_localplayer),
                    CodeInstruction.Call(typeof(Patches), nameof(GetDisplayAsHUDStatInterface)),
                    CodeInstruction.StoreLocal(lbd_stat.LocalIndex)
                });

                return codes;
            }
        }

        [HarmonyPatch(typeof(Quartz.XUiC_HUDActiveItem), "updateActiveItemAmmo")]
        [HarmonyPrefix]
        private static bool Prefix_updateActiveItemAmmo_XUiC_HUDStatBar(Quartz.XUiC_HUDActiveItem __instance, EntityPlayerLocal ___localPlayer, ref int ___currentAmmoCount)
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

        private static IDisplayAsHUDStat GetDisplayAsHUDStatInterface(Quartz.XUiC_HUDActiveItem statBar, EntityPlayerLocal localPlayer)
        {
            if (statBar == null || localPlayer == null)
            {
                return null;
            }

            int actionIndex = MultiActionManager.GetActionIndexForEntity(localPlayer);
            if (actionIndex >= 0 && actionIndex < localPlayer.inventory.holdingItem.Actions.Length)
            {
                return (localPlayer.inventory.holdingItem.Actions[actionIndex] as IModuleContainerFor<IDisplayAsHUDStat>)?.Instance;
            }
            return null;
        }
        #endregion
    }
}

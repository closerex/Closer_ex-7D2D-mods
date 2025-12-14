using HarmonyLib;
using KFCommonUtilityLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public interface IDisplayAsHUDStat
    {
        string GetHUDStatValue(ItemInventoryData invData);
        string GetHUDStatValueWithMax(ItemInventoryData invData, int currentAmmoCount);
        float GetHUDStatFillFraction(ItemInventoryData invData, int currentAmmoCount);
        bool UpdateActiveItemAmmo(ItemInventoryData invData, ref int currentAmmoCount);
        void GetIconOverride(ItemInventoryData invData, ref string originalIcon);
        void GetIconTintOverride(ItemInventoryData invData, ref Color32 originalTint);
    }

    [HarmonyPatch]
    public static class DisplayAsHUDPatches
    {
        [HarmonyPatch]
        public static class GetBindingValuePatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 2))
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "GetBindingValue");
                }
                else
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "GetBindingValueInternal");
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var codes = instructions.ToList();

                var fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));
                var fld_inventory = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.inventory));
                var fld_attackaction = AccessTools.Field(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.attackAction));
                var fld_currentammo = AccessTools.Field(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.currentAmmoCount));
                var mtd_localplayer = AccessTools.Method(typeof(VersionPatchManager), nameof(VersionPatchManager.GetLocalPlayer));
                var mtd_format = AccessTools.Method(typeof(CachedStringFormatter<int>), nameof(CachedStringFormatter<int>.Format), new[] { typeof(int) });
                var mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
                var mtd_holdingitemdata = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemData));
                var mtd_getstat = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatValue));
                var mtd_getstatwithmax = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatValueWithMax));
                var mtd_getstatfill = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatFillFraction));
                var mtd_geticonoverride = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetIconOverride));
                var mtd_gettintoverride = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetIconTintOverride));
                var mtd_iseditingtool = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.IsEditingTool));
                var mtd_geticon = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.GetIconName));
                var mtd_geticontint = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.GetIconTint));

                var lbd_interface = generator.DeclareLocal(typeof(IDisplayAsHUDStat));

                for (int i = 0; i < codes.Count - 1; i++)
                {
                    if (codes[i].LoadsField(fld_meta))
                    {
                        if (codes[i + 1].Calls(mtd_format))
                        {
                            //statcurrent
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (codes[j].opcode == OpCodes.Ldarg_1)
                                {
                                    Label lbl = generator.DefineLabel();
                                    var lbls_original = codes[j].ExtractLabels();
                                    codes[j].WithLabels(lbl);
                                    codes.InsertRange(j, new[]
                                    {
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex).WithLabels(lbls_original),
                                        new CodeInstruction(OpCodes.Brfalse, lbl),
                                        CodeInstruction.LoadArgument(1),
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                                        CodeInstruction.LoadArgument(0),
                                        new CodeInstruction(OpCodes.Call, mtd_localplayer),
                                        new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_getstat),
                                        new CodeInstruction(OpCodes.Stind_Ref),
                                        new CodeInstruction(OpCodes.Br, codes[j - 1].operand)
                                    });
                                    i += 11;
                                    Log.Out("statcurrent display patched!");
                                    break;
                                }
                            }
                        }
                        else if (codes[i + 1].opcode == OpCodes.Ldarg_0)
                        {
                            //statcurrentwithmax
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (codes[j].opcode == OpCodes.Ldarg_1)
                                {
                                    Label lbl = generator.DefineLabel();
                                    var lbls_original = codes[j].ExtractLabels();
                                    codes[j].WithLabels(lbl);
                                    codes.InsertRange(j, new[]
                                    {
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex).WithLabels(lbls_original),
                                        new CodeInstruction(OpCodes.Brfalse, lbl),
                                        CodeInstruction.LoadArgument(1),
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                                        CodeInstruction.LoadArgument(0),
                                        new CodeInstruction(OpCodes.Call, mtd_localplayer),
                                        new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                                        CodeInstruction.LoadArgument(0),
                                        new CodeInstruction(OpCodes.Ldfld, fld_currentammo),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_getstatwithmax),
                                        new CodeInstruction(OpCodes.Stind_Ref),
                                        new CodeInstruction(OpCodes.Br, codes[j - 1].operand)
                                    });
                                    i += 13;
                                    Log.Out("statcurrentwithmax display patched!");
                                    break;
                                }
                            }
                        }
                        else if (codes[i + 1].opcode == OpCodes.Conv_R4)
                        {
                            //statfill, ignored

                        }
                        else
                        {
                            Log.Error($"[DisplayAsHUDPatches] Transpiler_GetBindingValueInternal_XUiC_HUDStatBar unexpected opcode after loading ItemValue.Meta: {codes[i + 1]}");
                        }
                    }
                    else if (codes[i].Calls(mtd_getvalue))
                    {
                        if (codes[i + 1].opcode == OpCodes.Conv_I4)
                        {
                            //statvisible
                            object lbl_or = null;
                            for (int j = i + 2; j < codes.Count; j++)
                            {
                                if (codes[j].opcode == OpCodes.Ldstr && codes[j].OperandIs("true"))
                                {
                                    lbl_or = codes[j - 1].labels[0];
                                    break;
                                }
                            }
                            if (lbl_or == null)
                            {
                                Log.Error($"[DisplayAsHUDPatches] Transpiler_GetBindingValueInternal_XUiC_HUDStatBar unexpected second branch in statvisible patch");
                            }
                            else
                            {
                                for (int j = i - 1; j >= 0; j--)
                                {
                                    if (codes[j].LoadsField(fld_attackaction) && codes[j + 1].Branches(out _))
                                    {
                                        codes.InsertRange(j - 1, new[]
                                        {
                                            CodeInstruction.LoadLocal(lbd_interface.LocalIndex).WithLabels(codes[j - 1].ExtractLabels()),
                                            new CodeInstruction(OpCodes.Brtrue, lbl_or)
                                        });
                                        i += 2;
                                        Log.Out("statvisible display patched!");
                                        break;
                                    }
                                }
                            }
                        }
                        else if (codes[i + 1].opcode == OpCodes.Div)
                        {
                            //statfill
                            for (int j = i - 1; j >= 0; j--)
                            {
                                if (codes[j].opcode == OpCodes.Br || codes[j].opcode == OpCodes.Br_S)
                                {
                                    Label lbl = generator.DefineLabel(), lbl_else = generator.DefineLabel();
                                    var lbls_original = codes[j + 1].ExtractLabels();
                                    codes[j + 1].WithLabels(lbl_else);
                                    codes[i + 2].WithLabels(lbl);
                                    codes.InsertRange(j + 1, new[]
                                    {
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex).WithLabels(lbls_original),
                                        new CodeInstruction(OpCodes.Brfalse, lbl_else),
                                        CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                                        CodeInstruction.LoadArgument(0),
                                        new CodeInstruction(OpCodes.Call, mtd_localplayer),
                                        new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                                        CodeInstruction.LoadArgument(0),
                                        new CodeInstruction(OpCodes.Ldfld, fld_currentammo),
                                        new CodeInstruction(OpCodes.Callvirt, mtd_getstatfill),
                                        new CodeInstruction(OpCodes.Br, lbl)
                                    });
                                    i += 11;
                                    Log.Out("statfill display patched!");
                                    break;
                                }
                            }
                        }
                    }
                    else if (codes[i].Calls(mtd_geticon))
                    {
                        Label lbl = generator.DefineLabel();
                        codes[i + 2].WithLabels(lbl);
                        CodeInstruction ins;
                        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
                        {
                            ins = CodeInstruction.LoadArgument(1);
                        }
                        else
                        {
                            ins = CodeInstruction.LoadLocal(3, true);
                        }
                        codes.InsertRange(i + 2, new[]
                        {
                            CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                            new CodeInstruction(OpCodes.Brfalse, lbl),
                            CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                            CodeInstruction.LoadArgument(0),
                            new CodeInstruction(OpCodes.Call, mtd_localplayer),
                            new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                            new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                            ins,
                            new CodeInstruction(OpCodes.Callvirt, mtd_geticonoverride),
                        });
                        i += 9;
                        Log.Out("icon override display patched!");
                    }
                    else if (codes[i].Calls(mtd_geticontint))
                    {
                        int localIndex;
                        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
                        {
                            localIndex = 3;
                        }
                        else
                        {
                            localIndex = 1;
                        }
                        codes.InsertRange(i + 3, new[]
                        {
                            CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                            new CodeInstruction(OpCodes.Brfalse, codes[i + 3].labels[0]),
                            CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                            CodeInstruction.LoadArgument(0),
                            new CodeInstruction(OpCodes.Call, mtd_localplayer),
                            new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                            new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                            CodeInstruction.LoadLocal(localIndex, true),
                            new CodeInstruction(OpCodes.Callvirt, mtd_gettintoverride),
                        });
                        i += 9;
                        Log.Out("icon tint override display patched!");
                    }
                }

                codes.InsertRange(0, new[]
                {
                    CodeInstruction.LoadArgument(0),
                    CodeInstruction.Call(typeof(DisplayAsHUDPatches), nameof(GetDisplayAsHUDStatInterface)),
                    new CodeInstruction(OpCodes.Stloc, lbd_interface)
                });

                return codes;
            }
        }

        [HarmonyPatch]
        private static class V2_5VersionPatch1
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "RefreshFill");
                }
                else
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "refreshFill");
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var codes = instructions.ToList();
                var fld_inventory = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.inventory));
                var fld_currentammo = AccessTools.Field(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.currentAmmoCount));
                var mtd_localplayer = AccessTools.Method(typeof(VersionPatchManager), nameof(VersionPatchManager.GetLocalPlayer));
                var mtd_holdingitemdata = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemData));
                var mtd_getstatfill = AccessTools.Method(typeof(IDisplayAsHUDStat), nameof(IDisplayAsHUDStat.GetHUDStatFillFraction));
                var mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));

                var lbd_interface = generator.DeclareLocal(typeof(IDisplayAsHUDStat));

                for (int i = 0; i < codes.Count - 1; i++)
                {
                    if (codes[i].opcode == OpCodes.Stloc_0)
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            CodeInstruction.LoadArgument(0),
                            CodeInstruction.Call(typeof(DisplayAsHUDPatches), nameof(GetDisplayAsHUDStatInterface)),
                            new CodeInstruction(OpCodes.Stloc, lbd_interface)
                        });
                        i += 3;
                    }
                    else if (codes[i].Calls(mtd_getvalue))
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (codes[j].opcode == OpCodes.Br || codes[j].opcode == OpCodes.Br_S)
                            {
                                Label lbl = generator.DefineLabel(), lbl_else = generator.DefineLabel();
                                var lbls_original = codes[j + 1].ExtractLabels();
                                codes[j + 1].WithLabels(lbl_else);
                                codes[i + 2].WithLabels(lbl);
                                codes.InsertRange(j + 1, new[]
                                {
                                    CodeInstruction.LoadLocal(lbd_interface.LocalIndex).WithLabels(lbls_original),
                                    new CodeInstruction(OpCodes.Brfalse, lbl_else),
                                    CodeInstruction.LoadLocal(lbd_interface.LocalIndex),
                                    CodeInstruction.LoadArgument(0),
                                    new CodeInstruction(OpCodes.Call, mtd_localplayer),
                                    new CodeInstruction(OpCodes.Ldfld, fld_inventory),
                                    new CodeInstruction(OpCodes.Callvirt, mtd_holdingitemdata),
                                    CodeInstruction.LoadArgument(0),
                                    new CodeInstruction(OpCodes.Ldfld, fld_currentammo),
                                    new CodeInstruction(OpCodes.Callvirt, mtd_getstatfill),
                                    new CodeInstruction(OpCodes.Br, lbl)
                                });
                                Log.Out("refreshfill patched!");
                                break;
                            }
                        }
                        break;
                    }
                }

                return codes;
            }
        }

        [HarmonyPatch(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.updateActiveItemAmmo))]
        [HarmonyPrefix]
        private static bool Prefix_updateActiveItemAmmo_XUiC_HUDStatBar(XUiC_HUDStatBar __instance)
        {
            var displayAsHUDStat = GetDisplayAsHUDStatInterface(__instance);
            if (displayAsHUDStat != null)
            {
                int currentAmmoCount = __instance.currentAmmoCount;
                if (displayAsHUDStat.UpdateActiveItemAmmo(__instance.GetLocalPlayer().inventory.holdingItemData, ref currentAmmoCount))
                {
                    __instance.currentAmmoCount = currentAmmoCount;
                    return false;
                }
            }
            return true;
        }

        private static IDisplayAsHUDStat GetDisplayAsHUDStatInterface(XUiC_HUDStatBar statBar)
        {
            EntityPlayerLocal localPlayer = statBar.GetLocalPlayer();
            if (statBar == null || statBar.StatType != HUDStatTypes.ActiveItem || localPlayer == null || statBar.GetVehicle() != null)
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

        [HarmonyPatch]
        private static class V2_5VersionPatch2
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "SetupActiveItemEntry");
                }
                else
                {
                    yield return AccessTools.Method(typeof(XUiC_HUDStatBar), "setupActiveItemEntry");
                }
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                for (int i = 0; i < codes.Count - 1; i++)
                {
                    if (codes[i].LoadsConstant(0) && (codes[i + 1].opcode == OpCodes.Br_S || codes[i + 1].opcode == OpCodes.Br))
                    {
                        codes[i].opcode = OpCodes.Ldc_I4_S;
                        codes[i].operand = 100;
                    }
                }
                return codes;
            }
        }
    }
}

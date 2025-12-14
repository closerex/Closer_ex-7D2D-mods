using HarmonyLib;
using InControl;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;

[TypeTarget(typeof(ItemClass))]
public class ItemModuleInversedAction
{
}

[HarmonyPatch]
public static class InversedActionPatches
{
    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var prop_wasreleased = AccessTools.PropertyGetter(typeof(OneAxisInputControl), nameof(OneAxisInputControl.WasReleased));
        var fld_primary = AccessTools.Field(typeof(PlayerActionsLocal), nameof(PlayerActionsLocal.Primary));
        var fld_secondary = AccessTools.Field(typeof(PlayerActionsLocal), nameof(PlayerActionsLocal.Secondary));

        int li_holding_item, li_is_primary_pressed, li_is_secondary_pressed, li_can_run_primary, li_can_run_secondary;
        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 1))
        {
            li_holding_item = 35;
            li_is_primary_pressed = 22;
            li_is_secondary_pressed = 23;
            li_can_run_primary = 21;
            li_can_run_secondary = 20;
        }
        else if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
        {
            li_holding_item = 37;
            li_is_primary_pressed = 24;
            li_is_secondary_pressed = 25;
            li_can_run_primary = 23;
            li_can_run_secondary = 22;
        }
        else
        {
            li_holding_item = 40;
            li_is_primary_pressed = 27;
            li_is_secondary_pressed = 28;
            li_can_run_primary = 26;
            li_can_run_secondary = 25;
        }

        for (int i = 1; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == li_is_primary_pressed && codes[i - 1].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i - 1].operand).LocalIndex == li_can_run_primary && codes[i + 1].opcode == OpCodes.And && codes[i + 2].Branches(out _))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, li_is_secondary_pressed),
                    new CodeInstruction(OpCodes.Ldloc_S, li_holding_item),
                    CodeInstruction.CallClosure<Func<bool, bool, ItemClass, bool>>(static (isPrimaryPressed, isSecondaryPressed, holdingItem) =>
                    {
                        return (holdingItem is IModuleContainerFor<ItemModuleInversedAction>) ? isSecondaryPressed : isPrimaryPressed;
                    })
                });
                Log.Out("primary press patched!");
                i += 3;
            }
            else if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == li_is_secondary_pressed && codes[i - 1].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i - 1].operand).LocalIndex == li_can_run_secondary && codes[i + 1].opcode == OpCodes.And && codes[i + 2].Branches(out _))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, li_is_primary_pressed),
                    new CodeInstruction(OpCodes.Ldloc_S, li_holding_item),
                    CodeInstruction.CallClosure<Func<bool, bool, ItemClass, bool>>(static (isSecondaryPressed, isPrimaryPressed, holdingItem) =>
                    {
                        return (holdingItem is IModuleContainerFor<ItemModuleInversedAction>) ? isPrimaryPressed : isSecondaryPressed;
                    })
                });
                Log.Out("secondary press patched!");
                i += 3;
            }
            else if (codes[i].Calls(prop_wasreleased) && codes[i - 1].LoadsField(fld_primary) && codes[i + 1].Branches(out _))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(PlayerMoveController), nameof(PlayerMoveController.playerInput))),
                    new CodeInstruction(OpCodes.Ldfld, fld_secondary),
                    new CodeInstruction(OpCodes.Callvirt, prop_wasreleased),
                    new CodeInstruction(OpCodes.Ldloc_S, li_holding_item),
                    CodeInstruction.CallClosure<Func<bool, bool,ItemClass, bool>>(static (isPrimaryPressed, isSecondaryPressed, holdingItem) =>
                    {
                        return (holdingItem is IModuleContainerFor<ItemModuleInversedAction>) ? isSecondaryPressed : isPrimaryPressed;
                    })
                });
                Log.Out("primary release patched!");
                i += 6;
            }
            else if (codes[i].Calls(prop_wasreleased) && codes[i - 1].LoadsField(fld_secondary) && codes[i + 1].Branches(out _))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(PlayerMoveController), nameof(PlayerMoveController.playerInput))),
                    new CodeInstruction(OpCodes.Ldfld, fld_primary),
                    new CodeInstruction(OpCodes.Callvirt, prop_wasreleased),
                    new CodeInstruction(OpCodes.Ldloc_S, li_holding_item),
                    CodeInstruction.CallClosure<Func<bool, bool, ItemClass, bool>>(static (isSecondaryPressed, isPrimaryPressed, holdingItem) =>
                    {
                        return (holdingItem is IModuleContainerFor<ItemModuleInversedAction>) ? isPrimaryPressed : isSecondaryPressed;
                    })
                });
                Log.Out("secondary release patched!");
                i += 6;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetExecuteActionGrazeTarget))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_GetExecuteActionGrazeTarget_ItemActionDynamic(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_index = AccessTools.Field(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction));
        var mtd_fireevent = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_fireevent))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].opcode == OpCodes.Brfalse || codes[j].opcode == OpCodes.Brfalse_S)
                    {
                        codes[i - 1].ExtractLabels();
                        codes.RemoveRange(j, i - 1 - j);
                        codes.InsertRange(j, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            CodeInstruction.CallClosure<Func<bool, ItemActionDynamic, MinEventTypes>>(static (isSecondary, action) =>
                            {
                                bool isInversedAction = action is IModuleContainerFor<ActionModuleInversedAction>;
                                if ((isSecondary && isInversedAction) || (!isSecondary && !isInversedAction))
                                {
                                    //Log.Out($"firing primary graze miss event is secondary {isSecondary} is inversed action {isInversedAction}");
                                    return MinEventTypes.onSelfPrimaryActionGrazeMiss;
                                }
                                //Log.Out($"firing secondary graze miss event is secondary {isSecondary} is inversed action {isInversedAction}");
                                return MinEventTypes.onSelfSecondaryActionGrazeMiss;
                            })
                        });
                        break;
                    }
                }
                break;
            }
        }

        return codes;
    }
}
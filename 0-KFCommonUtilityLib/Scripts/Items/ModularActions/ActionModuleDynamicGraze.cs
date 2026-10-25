using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

[TypeTarget(typeof(ItemActionDynamic)), TypeDataTarget(typeof(WindowDamageData))]
public class ActionModuleDynamicGraze
{
    // Per-action state shared by the animation coroutine and injected hit code.
    public class WindowDamageData
    {
        // Normalized progress within the graze window.
        public float Phase = 1f;

        // Armed only during a window-mode cast.
        public bool Active;

        // Only the extra sight ray at the sweep midpoint may hit solids.
        public bool AllowBlockHits;

        public float PeakOffset;
        public float Sigma = 0.2f;

        // Temporary scale for casts issued by AnimationCustomMeleeAttackState.
        public float GlobalDamageMultiplier = 1f;
    }

    private string dynamicSoundStart = null;

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    private void Prefix_ExecuteAction(ItemActionDynamic __instance, ItemActionData _actionData, bool _bReleased, bool __runOriginal, out (bool executed, string originalSound) __state)
    {
        if (__runOriginal && !_bReleased && !string.IsNullOrEmpty(dynamicSoundStart) && _actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.soundStart);
                __instance.soundStart = dynamicSoundStart;
                return;
            }
        }
        __state = (false, null);
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    private void Postfix_ExecuteAction(ItemActionDynamic __instance, (bool executed, string originalSound) __state)
    {
        if (__state.executed)
        {
            __instance.soundStart = __state.originalSound;
        }
    }

    [HarmonyPatch(nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    private void Postfix_CancelAction(ItemActionData _actionData)
    {
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool("IsMeleeRunning", false);
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPrefix]
    private void Prefix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, bool __runOriginal, out (bool executed, bool useGrazeCast, float lastUseTime) __state)
    {
        if (__runOriginal && _actionData.invData.holdingEntity is EntityPlayerLocal player)
        {
            var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
            {
                __state = (true, __instance.UseGrazingHits, _actionData.lastUseTime);
                _actionData.lastUseTime = Time.time;
                __instance.UseGrazingHits = false;
                return;
            }
        }
        __state = (false, false, -1);
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    private void Postfix_OnHoldingUpdate(ItemActionDynamic __instance, ItemActionData _actionData, (bool executed, bool useGrazeCast, float lastUseTime) __state)
    {
        if (__state.executed)
        {
            __instance.UseGrazingHits = __state.useGrazeCast;
            _actionData.lastUseTime = __state.lastUseTime;
        }
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        _props.ParseString("DynamicSoundStart", ref dynamicSoundStart);
    }

    // Module transpilers rewrite generated actions, not the shared vanilla method.
    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget))]
    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamic_hitTarget(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var getValue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));

        // Match the graze GetValue by the multiplication that consumes its result.
        int replaced = 0;
        for (int i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].Calls(getValue) && codes[i + 1].opcode == OpCodes.Mul)
            {
                // Inject module/data accessors here so the module builder can rewrite them.
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(ItemActionModuleProcessor), nameof(ItemActionModuleProcessor.LoadDataModule), null, new[] { typeof(WindowDamageData) }),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new[] { typeof(ActionModuleDynamicGraze) }),
                    new CodeInstruction(OpCodes.Ldarg_3),
                    CodeInstruction.Call(typeof(ActionModuleDynamicGraze), nameof(WindowDamageMultiplier)),
                });
                replaced++;
                i += 6;
            }
        }
        if (replaced != 1)
            throw new InvalidOperationException($"DynamicGraze hitTarget anchor changed: replaced={replaced}");

        // Scale damage inputs while retaining vanilla graze and downstream rules.
        var getBlockDamage = AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageBlock));
        var getEntityDamage = AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageEntity));
        int blockDamageScales = 0;
        int entityDamageScales = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            bool blockDamage = codes[i].Calls(getBlockDamage);
            if (!blockDamage && !codes[i].Calls(getEntityDamage)) continue;
            codes.InsertRange(i + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                CodeInstruction.Call(typeof(ItemActionModuleProcessor), nameof(ItemActionModuleProcessor.LoadDataModule), null, new[] { typeof(WindowDamageData) }),
                CodeInstruction.LoadField(typeof(WindowDamageData), nameof(WindowDamageData.GlobalDamageMultiplier)),
                new CodeInstruction(OpCodes.Mul),
            });
            if (blockDamage) blockDamageScales++;
            else entityDamageScales++;
            i += 4;
        }
        if (blockDamageScales != 1 || entityDamageScales != 1)
            throw new InvalidOperationException(
                $"DynamicGraze hitTarget damage anchor changed: block={blockDamageScales} entity={entityDamageScales}");

        // Grazes must not reserve props or blocks for the direct-hit path.
        var addProp = AccessTools.Method(typeof(List<PropRef>), nameof(List<PropRef>.Add));
        var addBlock = AccessTools.Method(typeof(List<Vector3i>), nameof(List<Vector3i>.Add));
        int directPropAdds = 0;
        int directBlockAdds = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(addProp))
            {
                codes[i].opcode = OpCodes.Ldarg_3;
                codes[i].operand = null;
                codes.Insert(i + 1, CodeInstruction.Call(typeof(ActionModuleDynamicGraze), nameof(AddAlreadyHitPropIfDirect)));
                directPropAdds++;
                i++;
            }
            else if (codes[i].Calls(addBlock))
            {
                codes[i].opcode = OpCodes.Ldarg_3;
                codes[i].operand = null;
                codes.Insert(i + 1, CodeInstruction.Call(typeof(ActionModuleDynamicGraze), nameof(AddAlreadyHitBlockIfDirect)));
                directBlockAdds++;
                i++;
            }
        }
        if (directPropAdds != 1 || directBlockAdds != 1)
            throw new InvalidOperationException(
                $"DynamicGraze hitTarget already-hit anchor changed: props={directPropAdds} blocks={directBlockAdds}");

        return codes;
    }

    public static void AddAlreadyHitPropIfDirect(List<PropRef> alreadyHitProps, PropRef propRef, bool isGrazingHit)
    {
        if (!isGrazingHit)
        {
            alreadyHitProps.Add(propRef);
        }
    }

    public static void AddAlreadyHitBlockIfDirect(List<Vector3i> alreadyHitBlocks, Vector3i blockPos, bool isGrazingHit)
    {
        if (!isGrazingHit)
        {
            alreadyHitBlocks.Add(blockPos);
        }
    }

    // Vanilla Raycast ignores hit history and retains only the last penetrated hit.
    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast))]
    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_Raycast(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var hitTarget = AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget));
        var isHitValid = AccessTools.Method(typeof(ItemActionDynamic), "isHitValid");

        int replaced = 0;
        for (int i = 3; i < codes.Count; i++)
        {
            if (codes[i].Calls(hitTarget)
                && codes[i - 1].opcode == OpCodes.Ldc_I4_0
                && (codes[i - 2].opcode == OpCodes.Ldloc_S || codes[i - 2].opcode == OpCodes.Ldloc)
                && codes[i - 3].opcode == OpCodes.Ldarg_1)
            {
                // Reuse isHitValid's out local instead of depending on local numbering.
                int validCall = -1;
                for (int j = i - 4; j >= 0; j--)
                {
                    if (codes[j].Calls(isHitValid))
                    {
                        validCall = j;
                        break;
                    }
                    if (codes[j].opcode == OpCodes.Ret)
                    {
                        break;
                    }
                }
                if (validCall < 1)
                {
                    throw new InvalidOperationException(
                        "DynamicGraze Raycast anchor changed: the hitTarget call has no preceding isHitValid");
                }
                var entityLoad = codes[validCall - 1];
                if (entityLoad.opcode != OpCodes.Ldloca_S && entityLoad.opcode != OpCodes.Ldloca)
                {
                    throw new InvalidOperationException(
                        "DynamicGraze Raycast anchor changed: isHitValid's out argument is not an ldloca");
                }
                var hitInfoLoad = codes[i - 2];

                // Skip damage, not the hit counter: historical hits must not become misses.
                var afterCall = generator.DefineLabel();
                codes[i + 1].WithLabels(afterCall);

                // The receiver is already on the stack; only the skip branch must pop it.
                var resume = generator.DefineLabel();
                codes[i - 3].WithLabels(resume);

                codes.InsertRange(i - 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc, entityLoad.operand),
                    new CodeInstruction(OpCodes.Ldloc, hitInfoLoad.operand),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(ItemActionModuleProcessor), nameof(ItemActionModuleProcessor.LoadDataModule), null, new[] { typeof(WindowDamageData) }),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(ModuleExtensions), nameof(ModuleExtensions.LoadModule), null, new[] { typeof(ActionModuleDynamicGraze) }),
                    CodeInstruction.Call(typeof(ActionModuleDynamicGraze), nameof(ShouldSkipRaycastHit)),
                    new CodeInstruction(OpCodes.Brfalse, resume),
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Br, afterCall)
                });
                replaced++;
                i += 11;
            }
        }
        if (replaced != 1)
            throw new InvalidOperationException($"DynamicGraze Raycast anchor changed: replaced={replaced}");

        // Stop at disallowed solids before isHitValid resolves an entity; keep vanilla's result exit.
        int blockStops = 0;
        for (int i = 4; i < codes.Count - 1; i++)
        {
            if (!codes[i].Calls(isHitValid)) continue;
            var hitInfoLoad = codes[i - 3];
            var exitBranch = codes[i + 1];
            if (codes[i - 4].opcode != OpCodes.Ldarg_0
                || (hitInfoLoad.opcode != OpCodes.Ldloc_S && hitInfoLoad.opcode != OpCodes.Ldloc)
                || codes[i - 2].opcode != OpCodes.Ldarg_1
                || (codes[i - 1].opcode != OpCodes.Ldloca_S && codes[i - 1].opcode != OpCodes.Ldloca)
                || (exitBranch.opcode != OpCodes.Brfalse && exitBranch.opcode != OpCodes.Brfalse_S))
            {
                throw new InvalidOperationException("DynamicGraze Raycast block anchor changed: isHitValid arguments or exit branch");
            }
            var entry = codes[i - 4];
            codes.InsertRange(i - 4, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1).WithLabels(entry.ExtractLabels()),
                CodeInstruction.Call(typeof(ItemActionModuleProcessor), nameof(ItemActionModuleProcessor.LoadDataModule), null, new[] { typeof(WindowDamageData) }),
                new CodeInstruction(OpCodes.Ldloc, hitInfoLoad.operand),
                CodeInstruction.Call(typeof(ActionModuleDynamicGraze), nameof(ShouldStopRaycastOnBlock)),
                new CodeInstruction(OpCodes.Brtrue, exitBranch.operand)
            });
            blockStops++;
            i += 5;
        }
        if (blockStops != 1)
            throw new InvalidOperationException($"DynamicGraze Raycast block anchor changed: replaced={blockStops}");
        return codes;
    }

    public static bool ShouldStopRaycastOnBlock(WindowDamageData data, WorldRayHitInfo hitInfo)
    {
        return data.Active && !data.AllowBlockHits
            && hitInfo.tag != null && GameUtils.IsBlockOrTerrain(hitInfo.tag);
    }

    // Share entity history with GetExecuteActionGrazeTarget.
    public static bool ShouldSkipRaycastHit(ItemActionData _actionData, EntityAlive hitEntity, WorldRayHitInfo hitInfo, WindowDamageData data, ActionModuleDynamicGraze module)
    {
        if (!data.Active)
        {
            return false;
        }
        var dynamicData = (ItemActionDynamic.ItemActionDynamicData)_actionData;
        if (hitEntity && dynamicData.alreadyHitEnts.Contains(hitEntity.entityId))
        {
            return true;
        }
        if (hitInfo.tag != null && GameUtils.IsBlockOrTerrain(hitInfo.tag))
        {
            // Use the same prop/block classification as hitTarget.
            var propValue = hitInfo.hit.propValue;
            if (!propValue.IsAir)
            {
                if (dynamicData.alreadyHitProps.Contains(hitInfo.hit.propData.PropRef))
                {
                    return true;
                }
            }
            else if (!hitInfo.fmcHit.blockValue.isair && dynamicData.alreadyHitBlocks.Contains(hitInfo.fmcHit.blockPos))
            {
                return true;
            }
        }
        if (hitEntity && !dynamicData.alreadyHitEnts.Contains(hitEntity.entityId))
        {
            dynamicData.alreadyHitEnts.Add(hitEntity.entityId);
        }
        return false;
    }

    public static float WindowDamageMultiplier(float baseValue, WindowDamageData data, ActionModuleDynamicGraze module, bool isGrazingHit)
    {
        if (!isGrazingHit || !data.Active)
        {
            return baseValue;
        }
        return MeleeWindowDamage.GrazeMultiplier(data.Phase, baseValue, data.Sigma, data.PeakOffset);
    }
}

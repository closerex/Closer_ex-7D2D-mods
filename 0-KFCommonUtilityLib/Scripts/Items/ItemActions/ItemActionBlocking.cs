using HarmonyLib;
using KFCommonUtilityLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

//done: disable running, disable stamina regen, disable jump, manual animation interruption on main item, block hit animation
//todo: cancel multi item action on switching item
public class ItemActionBlocking : ItemAction
{
    public static int BlockingHash = Animator.StringToHash("IsBlocking");
    public static int BlockingHitHash = Animator.StringToHash("BlockingHit");
    public static int ParryingHitHash = Animator.StringToHash("ParryingHit");
    public FastTags<TagGroup.Global> tagsStaminaOnBlocking;
    public FastTags<TagGroup.Global> tagsStaminaOnParrying;
    public FastTags<TagGroup.Global> tagsDamageBlockingPrec;
    public FastTags<TagGroup.Global> tagsDamageParryingPrec;
    public FastTags<TagGroup.Global> tagDegradationBlocking;
    public FastTags<TagGroup.Global> tagDegradationParrying;
    public FastTags<TagGroup.Global> tagsParryDuration;
    public FastTags<TagGroup.Global> tagsBlockingAngleHor;
    public FastTags<TagGroup.Global> tagsBlockingAngleVer;
    public FastTags<TagGroup.Global> tagsBlockingAngleOffsetHor;
    public FastTags<TagGroup.Global> tagsBlockingAngleOffsetVer;

    public override void ReadFrom(DynamicProperties _props)
    {
        base.ReadFrom(_props);

        _props.Values.TryGetValue("CommonBlockingTags", out string tags);
        FastTags<TagGroup.Global> commonTags = string.IsNullOrEmpty(tags) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(tags);
        tagsStaminaOnBlocking = FastTags<TagGroup.Global>.Parse("StaminaOnBlocking") | commonTags;
        tagsStaminaOnParrying = FastTags<TagGroup.Global>.Parse("StaminaOnParrying") | commonTags;
        tagsDamageBlockingPrec = FastTags<TagGroup.Global>.Parse("DamageBlockingPrec") | commonTags;
        tagsDamageParryingPrec = FastTags<TagGroup.Global>.Parse("DamageParryingPrec") | commonTags;
        tagDegradationBlocking = FastTags<TagGroup.Global>.Parse("DegradationBlocking") | commonTags;
        tagDegradationParrying = FastTags<TagGroup.Global>.Parse("DegradationParrying") | commonTags;
        tagsParryDuration = FastTags<TagGroup.Global>.Parse("ParryDuration") | commonTags;
        tagsBlockingAngleHor = FastTags<TagGroup.Global>.Parse("BlockingRangeHor") | commonTags;
        tagsBlockingAngleVer = FastTags<TagGroup.Global>.Parse("BlockingRangeVer") | commonTags;
        tagsBlockingAngleOffsetHor = FastTags<TagGroup.Global>.Parse("BlockingAngleOffsetHor") | commonTags;
        tagsBlockingAngleOffsetVer = FastTags<TagGroup.Global>.Parse("BlockingAngleOffsetVer") | commonTags;
    }

    public override void OnModificationsChanged(ItemActionData _data)
    {
        base.OnModificationsChanged(_data);
    }

    public override ItemActionData CreateModifierData(ItemInventoryData _invData, int _indexInEntityOfAction)
    {
        return new ItemActionBlockingData(_invData, _indexInEntityOfAction);
    }

    public override void CancelAction(ItemActionData _actionData)
    {
        base.CancelAction(_actionData);
        if (_actionData.invData.holdingEntity is not EntityPlayerLocal player)
        {
            return;
        }
        _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(BlockingHash, false);
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        var blockingData = _actionData as ItemActionBlockingData;
        if (_actionData.invData.holdingEntity is not EntityPlayerLocal player)
        {
            return;
        }
        if (!_bReleased)
        {
            if (blockingData.CanStartBlocking)
            {
                if (_actionData.invData.itemValue.PercentUsesLeft <= 0f)
                {
                    _actionData.HasExecuted = false;
                    return;
                }
                if (player.Stamina <= 20)
                {
                    _actionData.HasExecuted = false;
                    return;
                }
                if (player.movementInput.running)
                {
                    player.MoveController.ForceStopRunning();
                }
                player.emodel.avatarController.UpdateBool(BlockingHash, true);
                player.emodel.avatarController._resetTrigger(BlockingHitHash, false);
                //calculate parry time
                player.MinEventContext.ItemActionData = _actionData;
                blockingData.parryDuration = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 0.5f, player, null, tagsParryDuration);
                blockingData.blockingRangeHor = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 0f, player, null, tagsBlockingAngleHor);
                blockingData.blockingRangeVer = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 0f, player, null, tagsBlockingAngleVer);
                blockingData.blockingAngleOffsetHor = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 0f, player, null, tagsBlockingAngleOffsetHor);
                blockingData.blockingAngleOffsetVer = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 0f, player, null, tagsBlockingAngleOffsetVer);
            }
            else
            {
                _actionData.HasExecuted = false;
            }
        }
        else
        {
            _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(BlockingHash, false);
            player.emodel.avatarController._resetTrigger(BlockingHitHash, false);
        }
    }

    public override bool IsActionRunning(ItemActionData _actionData)
    {
        var blockingData = _actionData as ItemActionBlockingData;
        if (blockingData != null)
        {
            return blockingData.isBlockingRunning;
        }
        return false;
    }

    public override bool IsAimingGunPossible(ItemActionData _actionData)
    {
        var blockingData = _actionData as ItemActionBlockingData;
        if (blockingData != null && blockingData.isBlockingRunning)
        {
            return false;
        }
        return base.IsAimingGunPossible(_actionData);
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);
        if (_actionData.invData.holdingEntity is not EntityPlayerLocal player)
        {
            return;
        }
        var blockingData = _actionData as ItemActionBlockingData;
        if (blockingData != null)
        {
            if (blockingData.isBlockingRunning)
            {
                if (_actionData.invData.holdingEntity.Stamina <= 0)
                {
                    player.emodel.avatarController.UpdateBool(BlockingHash, false);
                }
            }
        }
    }

    public override void StartHolding(ItemActionData _data)
    {
        base.StartHolding(_data);
        var blockingData = _data as ItemActionBlockingData;
        if (blockingData != null)
        {
            blockingData.ResetBlocking();

            blockingData.targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
            if (_data.invData.holdingEntity is EntityPlayerLocal player)
            {
                player.emodel.avatarController.UpdateBool(BlockingHash, false);
                player.emodel.avatarController._resetTrigger(BlockingHitHash, false);
            }
        }
    }

    public override void StopHolding(ItemActionData _data)
    {
        base.StopHolding(_data);
        var blockingData = _data as ItemActionBlockingData;
        if (blockingData != null)
        {
            blockingData.ResetBlocking();
            if (_data.invData.holdingEntity is EntityPlayerLocal player)
            {
                player.emodel.avatarController.UpdateBool(BlockingHash, false);
                player.emodel.avatarController._resetTrigger(BlockingHitHash, false);
            }
        }
    }

    [Flags]
    public enum BlockableType
    {
        None,
        Melee,
        Ranged,
        All
    }

    public class ItemActionBlockingData : ItemActionData
    {
        public bool isBlockingRunning = false;
        public bool isBlockingExited = true;
        public AnimationTargetsAbs targets;
        public float blockingBeginTime = 0f;
        public float parryDuration = 0f;
        public float blockingRangeHor = 0f;
        public float blockingRangeVer = 0f;
        public float blockingAngleOffsetHor = 0f;
        public float blockingAngleOffsetVer = 0f;
        public BlockableType blockableTypes = BlockableType.All;

        public bool CanStartBlocking => !isBlockingRunning && isBlockingExited && targets && targets.IsAnimationSet;
        public bool IsParrying => isBlockingRunning && (Time.time - blockingBeginTime) <= parryDuration;

        public ItemActionBlockingData(ItemInventoryData _inventoryData, int _indexInEntityOfAction) : base(_inventoryData, _indexInEntityOfAction)
        {
        }

        public void ResetBlocking()
        {
            isBlockingRunning = false;
            isBlockingExited = true;
            blockingBeginTime = 0f;
            targets = null;
        }

        public bool IsInBlockingAngle(Vector3 attackStartPos)
        {
            var player = invData.holdingEntity as EntityPlayerLocal;
            if (!player)
            {
                return false;
            }

            if (blockingRangeHor <= 0f || blockingRangeVer <= 0f)
            {
                return true;
            }

            Transform cameraTrans = player.playerCamera.transform;
            Quaternion angleOffset = Quaternion.Euler(0f, blockingAngleOffsetHor, 0f) * Quaternion.Euler(blockingAngleOffsetVer, 0f, 0f);
            return IsTargetInAngle.IsPointInRange(attackStartPos - cameraTrans.position,
                                         angleOffset * cameraTrans.forward,
                                         angleOffset * cameraTrans.right,
                                         angleOffset * cameraTrans.up,
                                         new(blockingRangeHor, blockingRangeVer));
        }
    }
}

[HarmonyPatch]
public static class ItemActionBlockingPatches
{
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.DamageEntity))]
    [HarmonyPrefix]
    private static void Prefix_DamageEntity_EntityPlayerLocal(EntityPlayerLocal __instance, DamageSource _damageSource, ref int _strength, ref bool _criticalHit, ref float impulseScale)
    {
        if (_damageSource is not DamageSourceEntity eds)
        {
            //Log.Out($"[KFLib] Damage source is not entity");
            return;
        }

        if (eds.damageType == EnumDamageTypes.None || eds.damageType > EnumDamageTypes.Bashing)
        {
            //Log.Out($"[KFLib] Damage type {eds.damageType} is not blockable");
            return;
        }

        EntityAlive attacker = GameManager.Instance.World.GetEntity(eds.getEntityId()) as EntityAlive;
        if (attacker == null)
        {
            //Log.Out($"[KFLib] Failed to get attacker entity with id={eds.getEntityId()}");
            return;
        }

        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"[KFLib] Incoming attack from {attacker.GetDebugName()} (id={attacker.entityId}), damage: {_strength}, critical hit: {_criticalHit}");
        }
        if (__instance.AttachedToEntity == null && __instance.inventory.holdingItemData is IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData> dataModule && __instance.inventory.holdingItemData.itemValue.PercentUsesLeft >= 0f)
        {
            var multiInvData = dataModule.Instance;
            if (multiInvData.boundItemClass?.Actions[2] is ItemActionBlocking blockingAction && multiInvData.boundInvData?.actionData[2] is ItemActionBlocking.ItemActionBlockingData blockingData)
            {
                var prevData = __instance.MinEventContext.ItemActionData;
                multiInvData.SetBoundParams();
                __instance.MinEventContext.ItemActionData = blockingData;
                __instance.MinEventContext.Other = attacker;

                Vector3 lookRayOrigin = attacker.GetLookRay().origin;
                bool isParrying = false;
                bool fireEvent = false;
                if (blockingData.isBlockingRunning && blockingData.IsInBlockingAngle(lookRayOrigin - Origin.position))
                {
                    isParrying = blockingData.IsParrying;
                    float drainAmount = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, blockingData.invData.itemValue, 0f, blockingData.invData.holdingEntity, null, isParrying ? blockingAction.tagsStaminaOnParrying : blockingAction.tagsStaminaOnBlocking);
                    if (drainAmount > __instance.Stamina)
                    {
                        //not enough stamina to block/parry
                        __instance.emodel.avatarController.UpdateBool(ItemActionBlocking.BlockingHash, false);
                        if (ConsoleCmdReloadLog.LogInfo)
                            Log.Out($"[KFLib] Not enough stamina to {(isParrying ? "parry" : "block")}, current stamina: {__instance.Stamina}, required: {drainAmount}");
                    }
                    else
                    {
                        __instance.Stamina -= drainAmount;
                        float damagePrec = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, blockingData.invData.itemValue, 0f, blockingData.invData.holdingEntity, null, isParrying ? blockingAction.tagsDamageParryingPrec : blockingAction.tagsDamageBlockingPrec);
                        if (ConsoleCmdReloadLog.LogInfo)
                            Log.Out($"[KFLib] {(isParrying ? "Parried" : "Blocked")} attack from {attacker.GetDebugName()} (id={attacker.entityId}), original damage: {_strength}, damage reduction: {damagePrec * 100f}%, final damage: {Mathf.RoundToInt(_strength * (1f - damagePrec))}");
                        _strength = Mathf.RoundToInt(_strength * (1f - damagePrec));
                        if (isParrying)
                        {
                            _criticalHit = false;
                            if (Vector3.Distance(eds.hitTransformPosition, lookRayOrigin) <= 2.5f)
                            {
                                MinEventActionKnockDownTarget.ForceStunTargetServer(attacker, EnumEntityStunType.Prone, EnumBodyPartHit.Head, Utils.EnumHitDirection.Front, true, GameManager.Instance.World.GetGameRandom().RandomFloat, 1.5f);
                            }
                        }
                        __instance.emodel.avatarController._setTrigger(isParrying ? ItemActionBlocking.ParryingHitHash : ItemActionBlocking.BlockingHitHash, false);

                        fireEvent = true;
                        __instance.FireEvent(isParrying ? CustomEnums.onSelfParryingDamage : CustomEnums.onSelfBlockingDamage);
                        float degradation = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, blockingData.invData.itemValue, 1f, blockingData.invData.holdingEntity, null, isParrying ? blockingAction.tagDegradationParrying : blockingAction.tagDegradationBlocking);
                        if (degradation > 0f)
                        {
                            __instance.inventory.holdingItemData.itemValue.UseTimes += degradation;
                        }
                        if (__instance.Stamina <= 0f || __instance.inventory.holdingItemData.itemValue.PercentUsesLeft <= 0f)
                        {
                            __instance.emodel.avatarController.UpdateBool(ItemActionBlocking.BlockingHash, false);
                        }
                    }
                }
                else if (ConsoleCmdReloadLog.LogInfo && blockingData.isBlockingRunning)
                {
                    Log.Out($"[KFLib] blocking failed!");
                }

                multiInvData.RestoreParams(false);
                __instance.MinEventContext.ItemActionData = prevData;
                if (fireEvent)
                {
                    __instance.inventory.holdingItem.FireEvent(isParrying ? CustomEnums.onSelfParryingDamage : CustomEnums.onSelfBlockingDamage, __instance.MinEventContext);
                }
            }
        }
    }

    private static List<ItemAction> tempActionList = new List<ItemAction>();
    private static List<ItemActionData> tempDataList = new List<ItemActionData>();
    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        int local_holdingitem;
        if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 1))
        {
            local_holdingitem = 35;
        }
        else if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
        {
            local_holdingitem = 37;
        }
        else
        {
            local_holdingitem = 40;
        }

        var fld_jump = AccessTools.Field(typeof(MovementInput), nameof(MovementInput.jump));

        for (int i = 0; i < codes.Count; i++)
        {
            //holding item
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == local_holdingitem)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.CallClosure<Action<PlayerMoveController>>(static (PlayerMoveController controller) =>
                    {
                        if (DroneManager.Debug_LocalControl || !controller.gameManager.gameStateManager.IsGameStarted() || GameStats.GetInt(EnumGameStats.GameState) != 1)
                            return;

                        bool isUIOpen = controller.windowManager.IsCursorWindowOpen() || controller.windowManager.IsInputActive() || controller.windowManager.IsModalWindowOpen();
                        if (isUIOpen || controller.entityPlayerLocal.emodel.IsRagdollActive || controller.entityPlayerLocal.IsDead() || controller.entityPlayerLocal.AttachedToEntity != null)
                        {
                            return;
                        }

                        EntityPlayerLocal player = controller.entityPlayerLocal;
                        bool IsPressed = PlayerActionKFLib.Instance.WeaponBlocking.IsPressed;
                        bool wasReleased = PlayerActionKFLib.Instance.WeaponBlocking.WasReleased;

                        if (!IsPressed && !wasReleased)
                        {
                            return;
                        }

                        if (IsPressed && (controller.playerInput.Primary.IsPressed || controller.playerInput.Secondary.IsPressed))
                        {
                            return;
                        }

                        if (player.inventory.holdingItemData is not IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData> dataModule)
                        {
                            return;
                        }

                        if (IsPressed && (player.AimingGun || player.IsReloading() || !player.inventory.GetIsFinishedSwitchingHeldItem()))
                        {
                            return;
                        }

                        var multiInvData = dataModule.Instance;
                        if (multiInvData.boundItemClass?.Actions[2] is ItemActionBlocking blockingAction && multiInvData.boundInvData?.actionData[2] is ItemActionBlocking.ItemActionBlockingData blockingData)
                        {
                            bool isActionRunning = false;
                            bool isAllRunningActionInterruptable = false;
                            var interruptData = (blockingData as IModuleContainerFor<ActionModuleAnimationInterruptSource.AnimationInterruptSourceData>)?.Instance;
                            if (!wasReleased)
                            {
                                tempActionList.Clear();
                                tempDataList.Clear();
                                ActionModuleAnimationInterruptSource.GetAllRunningAndInterruptableActions(player, blockingAction, blockingData, tempActionList, tempDataList, out isActionRunning, out isAllRunningActionInterruptable);
                            }

                            if (PlayerActionKFLib.Instance.Enabled && (!isActionRunning || isAllRunningActionInterruptable || wasReleased) && !player.inventory.holdingItemData.IsAnyActionLocked())
                            {
                                if (isActionRunning && isAllRunningActionInterruptable)
                                {
                                    (player.inventory.holdingItem as ItemClassExtendedFunction)?.CancelAllActions(player.inventory.holdingItemData);
                                    if (interruptData != null)
                                    {
                                        interruptData.interrupted = true;
                                    }
                                }
                                var prevData = player.MinEventContext.ItemActionData;
                                multiInvData.SetBoundParams();
                                player.MinEventContext.ItemActionData = blockingData;
                                if (IsPressed)
                                {
                                    multiInvData.boundItemClass.ExecuteAction(2, multiInvData.boundInvData, false, null);
                                }
                                else if (wasReleased)
                                {
                                    multiInvData.boundItemClass.ExecuteAction(2, multiInvData.boundInvData, true, null);
                                }
                                multiInvData.RestoreParams(false);
                                player.MinEventContext.ItemActionData = prevData;
                                player.emodel.avatarController.UpdateBool(AvatarController.reloadHash, false);
                            }
                        }
                    })
                });
                i += 2;
            }
            else if (codes[i].StoresField(fld_jump))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i + 1].ExtractLabels()),
                    CodeInstruction.LoadField(typeof(PlayerMoveController), nameof(PlayerMoveController.entityPlayerLocal)),
                    CodeInstruction.CallClosure<Action<EntityPlayerLocal>>(static (player) =>
                    {
                        if (player.movementInput.running && IsPlayerBlocking(player))
                        {
                            player.emodel.avatarController.UpdateBool(ItemActionBlocking.BlockingHash, false);
                        }
                    })
                });
                i += 3;
            }
            //else if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == local_ispressed)
            //{
            //    codes.InsertRange(i, new[]
            //    {
            //        new CodeInstruction(OpCodes.Ldloc_S, local_vehicleenabled),
            //        new CodeInstruction(OpCodes.Ldarg_0),
            //        CodeInstruction.LoadField(typeof(PlayerMoveController), nameof(PlayerMoveController.entityPlayerLocal)),
            //        CodeInstruction.CallClosure<Func<bool, bool, EntityPlayerLocal, bool>>(static (isPressed, vehicleEnabled, player) =>
            //        {
            //            if (vehicleEnabled || !isPressed)
            //            {
            //                return isPressed;
            //            }

            //            return !IsPlayerBlocking(player);
            //        })
            //    });
            //    i += 4;
            //}
        }

        return codes;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.MoveByInput))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_MoveByInput_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var prop_aiming = AccessTools.PropertyGetter(typeof(EntityAlive), nameof(EntityAlive.AimingGun));
        for (int i = 0; i < codes.Count; i++)
        {
            //running check
            if (codes[i].Calls(prop_aiming))
            {
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(ItemActionBlockingPatches), nameof(IsPlayerBlocking)),
                    new CodeInstruction(OpCodes.Brtrue, codes[i + 1].operand)
                });
                break;
            }
        }
        return codes;
    }

    private static bool IsPlayerBlocking(EntityPlayerLocal player)
    {
        if (player.inventory.holdingItemData is IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData> dataModule)
        {
            var multiInvData = dataModule.Instance;
            if (multiInvData.boundItemClass?.Actions[2] is ItemActionBlocking blockingAction && multiInvData.boundInvData?.actionData[2] is ItemActionBlocking.ItemActionBlockingData blockingData)
            {
                if (blockingData.isBlockingRunning)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

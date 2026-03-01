using GearsAPI.Settings.Global;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[Flags]
public enum AimCorrectionMode
{
    None,
    FovByDistance,
    MixedOrtho,
    Both
}
public class AimRefData : IBlendSource
{
    public AimReference aimRef;
    public ActionModuleProceduralAiming.ProceduralAimingData aimingData;
    public Vector3 targetPosOffset;
    public Quaternion targetRotOffset;
    public float targetAimRefOffset;
    public float targetAimFov;
    public float CurBlendWeight { get; set; }

    public AimRefData(AimReference curAimRef, ActionModuleProceduralAiming.ProceduralAimingData aimingData)
    {
        this.aimRef = curAimRef;
        this.aimingData = aimingData;
    }


    public void RecalcTargetValues()
    {
        if (aimingData.scopeBasePosTransform && aimRef)
        {
            targetPosOffset = aimRef.positionOffset;
            targetRotOffset = aimRef.rotationOffset;
            targetAimRefOffset = 0;
            targetAimFov = aimRef.designedAimFov;
            if (aimRef.asReference)
            {
                Vector3 byReferenceOffset = Vector3.Project(targetPosOffset - aimingData.scopeBasePosTransform.InverseTransformPoint(aimingData.playerCameraPosRef.position), targetRotOffset * Vector3.forward);
                if (aimRef.scopeBase?.defaultReference)
                {
                    byReferenceOffset -= Vector3.Project(aimRef.scopeBase.defaultReference.positionOffset - aimingData.scopeBasePosTransform.InverseTransformPoint(aimingData.playerCameraPosRef.position), targetRotOffset * Vector3.forward);
                }
                targetPosOffset -= byReferenceOffset;
                targetAimRefOffset = byReferenceOffset.magnitude;
            }
            UpdateAimFovOverride();
        }
    }

    internal void UpdateAimFovOverride()
    {
    }
}

public static class AimingSettings
{
    private static AimCorrectionMode currentMode = AimCorrectionMode.None;

    public static void InitSettings(IModGlobalSettings modSettings)
    {
        var category = modSettings.GetTab("MiscSettings").GetCategory("AimCorrection");

        var enableSetting = category.GetSetting("AimCorrectionType") as ISelectorGlobalSetting;
        if (!Enum.TryParse(enableSetting.CurrentValue, out currentMode))
        {
            Log.Warning($"Failed to parse aim correction mode: {enableSetting.CurrentValue}");
        }

        enableSetting.OnSettingChanged += (setting, newValue) =>
        {
            if (!Enum.TryParse(enableSetting.CurrentValue, out currentMode))
            {
                Log.Warning($"Failed to parse aim correction mode: {enableSetting.CurrentValue}");
            }

            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player != null && player.inventory?.holdingItemData?.actionData?[1] is IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData> dataModule)
            {
                var data = dataModule.Instance;
                data.RefreshAimRefData();
                data.UpdateCurrentReference(true);
            }
        };
    }

    public static bool HasFlag(AimCorrectionMode flag)
    {
        return currentMode.HasFlag(flag);
    }
}

[TypeTarget(typeof(ItemActionZoom)), TypeDataTarget(typeof(ProceduralAimingData))]
public class ActionModuleProceduralAiming
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionZoom __instance, ItemActionData _data, ProceduralAimingData __customData)
    {
        if (_data is IModuleContainerFor<ActionModuleErgoAffected.ErgoData> dataModule)
        {
            __customData.zoomInTime = dataModule.Instance.module.zoomInTimeBase / dataModule.Instance.module.aimSpeedModifierBase;
            __customData.ergoData = dataModule.Instance;
        }
        else
        {
            float zoomInTimeBase = 0.3f;
            __instance.Properties.ParseFloat("ZoomInTimeBase", ref zoomInTimeBase);
            float aimSpeedModifierBase = 1f;
            __instance.Properties.ParseFloat("AimSpeedModifierBase", ref aimSpeedModifierBase);
            __customData.zoomInTime = zoomInTimeBase / aimSpeedModifierBase;
            __customData.ergoData = null;
        }

        __customData.playerOriginTransform = null;
        __customData.playerCameraPosRef = _data.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView ? player.cameraTransform : null;
        if (__customData.playerCameraPosRef != null)
        {
            CameraLateUpdater.RegisterUpdater(__customData);
        }
        __customData.targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
        if (__customData.playerCameraPosRef)
        {
            if (__customData.targets.ItemFpv)
            {
                if (__customData.targets is RigTargets)
                {
                    __customData.isRigWeapon = true;
                    __customData.playerOriginTransform = __customData.targets.ItemAnimator.transform;
                    __customData.rigWeaponLocalPosition = __customData.playerOriginTransform.localPosition;
                    __customData.rigWeaponLocalRotation = __customData.playerOriginTransform.localRotation;
                }
                else
                {
                    __customData.isRigWeapon = false;
                    __customData.playerOriginTransform = __customData.playerCameraPosRef.FindInAllChildren("Hips");
                }
                __customData.playerCameraPosRef = __customData.targets.ItemFpv.Find("PlayerCameraPositionReference");
            }
            else
            {
                __customData.playerCameraPosRef = null;
            }
        }
        if (__customData.playerCameraPosRef)
        {
            __customData.scopeBase = __customData.targets.ItemFpv.GetComponentInChildren<ScopeBase>();
            if (__customData.scopeBase)
            {
                __customData.scopeBase.aimingModule = __customData;
            }
            __customData.scopeBasePosTransform = __customData.targets.ItemFpv.Find("ScopeBasePositionReference");
        }
        else
        {
            __customData.scopeBasePosTransform = null;
        }

        __customData.ResetAiming();
        __customData.UpdateCurrentReference(true);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ProceduralAimingData __customData)
    {
        if (__customData.scopeBase)
        {
            __customData.scopeBase.aimingModule = null;
        }
        __customData.ResetAiming();
        CameraLateUpdater.UnregisterUpdater(__customData);
    }

    //[HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    //public void Postfix_ExecuteAction(ProceduralAimingData __customData, ItemActionData _actionData)
    //{
    //    if (__customData.isAiming != ((ItemActionZoom.ItemActionDataZoom)_actionData).aimingValue)
    //    {
    //        __customData.UpdateCurrentReference();
    //        __customData.isAiming = ((ItemActionZoom.ItemActionDataZoom)_actionData).aimingValue;
    //    }
    //}

    public class ProceduralAimingData : IRootMovementUpdater
    {
        public AnimationTargetsAbs targets;
        public ScopeBase scopeBase;
        public ActionModuleErgoAffected.ErgoData ergoData;
        public float zoomInTime;
        //public Transform aimRefTransform;
        public Transform playerCameraPosRef;
        public Transform scopeBasePosTransform;
        public Transform playerOriginTransform;
        public bool isRigWeapon;
        public Vector3 rigWeaponLocalPosition;
        public Quaternion rigWeaponLocalRotation;

        public bool isAiming;
        public int curAimRefIndex = -1;
        //move curAimRefOffset towards aimRefOffset first, then move curAimOffset towards curAimRefOffset
        //public Vector3 aimRefPosOffset;
        //public Quaternion aimRefRotOffset;
        public Vector3 curAimPosOffset;
        public Quaternion curAimRotOffset;
        public const float TARGET_SWITCH_TIME = 0.075f;
        public FloatValueDamper aimProcDamper = new();
        //public Vector3ValueDamper targetSwitchPosDamper = new() { targetTime = TARGET_SWITCH_TIME };
        //public QuaternionValueDamper targetSwitchRotDamper = new() { targetTime = TARGET_SWITCH_TIME };
        public FloatValueDamper focusDistanceDamper = new() { targetTime = TARGET_SWITCH_TIME };
        //public FloatValueDamper flattenFactorDamper = new() { targetTime = TARGET_SWITCH_TIME };
        //public FloatValueDamper aimFovDamper = new() { currentValue = 45f, targetValue = 45f, targetTime = TARGET_SWITCH_TIME };
        //public List<AimReference> registeredReferences = new List<AimReference>();
        public MultiSourceBlender<AimRefData> targetSwitchBlender = new(TARGET_SWITCH_TIME);
        public EntityPlayerLocal holdingEntity;
        public float CurAimProcValue => aimProcDamper.CurrentValue;
        public float CurAimFlattenFactor { get; private set; } = 0f;
        public float CurFocusDistance => focusDistanceDamper.CurrentValue;
        public float CurTargetAimFovValue { get; private set; } = 45f;
        public int CurAimRefIndex
        {
            get
            {
                for (int i = targetSwitchBlender.Count - 1; i >= 0; i--)
                {
                    if (targetSwitchBlender[i].aimRef.gameObject.activeInHierarchy)
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        public AimRefData CurAimRefData => curAimRefIndex >= 0 && curAimRefIndex < targetSwitchBlender.Count ? targetSwitchBlender[curAimRefIndex] : null;
        public AimReference CurAimRef => CurAimRefData?.aimRef;

        //public float AimRefOffset { get; internal set; }
        public int Priority => 100;

        public ProceduralAimingData(ItemInventoryData _inventoryData)
        {
            holdingEntity = _inventoryData.holdingEntity as EntityPlayerLocal;
        }

        public void ResetAiming()
        {
            isAiming = false;
            curAimRefIndex = -1;
            //aimRefPosOffset = Vector3.zero;
            //aimRefRotOffset = Quaternion.identity;
            curAimPosOffset = Vector3.zero;
            curAimRotOffset = Quaternion.identity;
            aimProcDamper.Reset();
            //targetSwitchPosDamper.Reset();
            //targetSwitchRotDamper.Reset();
            focusDistanceDamper.Reset();
            targetSwitchBlender.SnapTo();
            //flattenFactorDamper.Reset();
            //aimFovDamper.Reset(45f);
            if (isRigWeapon && playerOriginTransform)
            {
                playerOriginTransform.localPosition = rigWeaponLocalPosition;
                playerOriginTransform.localRotation = rigWeaponLocalRotation;
            }
            //AimRefOffset = 0;
        }

        public bool RegisterGroup(AimReference[] group, string name)
        {
            if (holdingEntity && holdingEntity.bFirstPersonView)
            {
                foreach (var reference in group)
                {
                    if (reference.index == -1)
                    {
                        reference.index = targetSwitchBlender.Count;
                        targetSwitchBlender.RegisterSource(new(reference, this));
                    }
                }
                UpdateCurrentReference();
                //Log.Out($"Register group {name}\n{StackTraceUtility.ExtractStackTrace()}");
                return true;
            }
            return false;
        }

        public void RefreshAimRefData()
        {
            if (holdingEntity && holdingEntity.bFirstPersonView)
            {
                foreach (var data in targetSwitchBlender)
                {
                    data.RecalcTargetValues();
                }
            }
        }

        public void UpdateCurrentReference(bool snapTo = false)
        {
            curAimRefIndex = CurAimRefIndex;
            //AimReference curAimRef = CurAimRef;
            //AimRefData curAimRefData = CurAimRefData;
            //if (scopeBasePosTransform)
            //{
            //aimRefPosOffset = curAimRef.positionOffset;
            //aimRefRotOffset = curAimRef.rotationOffset;
            //AimRefOffset = 0;
            //float aimFovOverride = curAimRef.designedAimFov;
            //if (curAimRef.asReference)
            //{
            //    Vector3 byReferenceOffset = Vector3.Project(aimRefPosOffset - scopeBasePosTransform.InverseTransformPoint(playerCameraPosRef.position), aimRefRotOffset * Vector3.forward);
            //    if (curAimRef.scopeBase?.defaultReference)
            //    {
            //        byReferenceOffset -= Vector3.Project(curAimRef.scopeBase.defaultReference.positionOffset - scopeBasePosTransform.InverseTransformPoint(playerCameraPosRef.position), aimRefRotOffset * Vector3.forward);
            //    }
            //    aimRefPosOffset -= byReferenceOffset;
            //    AimRefOffset = byReferenceOffset.magnitude;
            //}
            //UpdateAimFovOverride(curAimRef, ref aimFovOverride);
            //UpdateFlattenTargets(curAimRef, snapTo);
            //if (snapTo)
            //{
            //    targetSwitchPosDamper.Reset(aimRefPosOffset);
            //    targetSwitchRotDamper.Reset(aimRefRotOffset);
            //    aimFovDamper.Reset(aimFovOverride);
            //}
            //else
            //{
            //    targetSwitchPosDamper.TargetValue = aimRefPosOffset;
            //    targetSwitchRotDamper.TargetValue = aimRefRotOffset;
            //    aimFovDamper.TargetValue = aimFovOverride;
            //}
            //}
            CurAimRefData?.RecalcTargetValues();
            UpdateFlattenTargets(snapTo);
            for (int i = 0; i < targetSwitchBlender.Count; i++)
            {
                targetSwitchBlender[i].aimRef.UpdateEnableState(isAiming && curAimRefIndex == i);
            }
            targetSwitchBlender.SetTargetIndex(curAimRefIndex, snapTo);
            CalcCurrentOffset(out _, out _);
        }

        private void UpdateFlattenTargets(bool snapTo)
        {
            AimReference curAimRef = CurAimRef;
            AimRefData curAimRefData = CurAimRefData;
            if (curAimRef == null || curAimRefData == null)
            {
                return;
            }
            if (curAimRef.designedAimDistance <= 0)
            {
                if (snapTo || CurAimFlattenFactor <= 0)
                {
                    focusDistanceDamper.Reset(0);
                    //flattenFactorDamper.Reset(0);
                }
                else
                {
                    focusDistanceDamper.Reset(focusDistanceDamper.CurrentValue);
                    //flattenFactorDamper.TargetValue = 0;
                }
            }
            else
            {
                if (snapTo || CurAimFlattenFactor <= 0)
                {
                    focusDistanceDamper.Reset(curAimRef.designedAimDistance + curAimRefData.targetAimRefOffset);
                    //flattenFactorDamper.Reset(curAimRef.designedFlattenFactor);
                }
                else
                {
                    focusDistanceDamper.TargetValue = curAimRef.designedAimDistance + curAimRefData.targetAimRefOffset;
                    //flattenFactorDamper.TargetValue = curAimRef.designedFlattenFactor;
                }
            }
        }

        public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRigWeapon, float _dt)
        {
            if (holdingEntity.AimingGun != isAiming)
            {
                isAiming = holdingEntity.AimingGun;
                UpdateCurrentReference(isAiming);
            }

            if (scopeBasePosTransform && playerCameraPosRef && playerOriginTransform && CurAimRef)
            {
                aimProcDamper.TargetValue = isAiming ? 1f : 0f;
                float zoomInTimeMod = ergoData == null ? zoomInTime : zoomInTime / ergoData.ModifiedErgo;
                zoomInTimeMod *= 0.25f;
                aimProcDamper.targetTime = zoomInTimeMod;

                //move aimRef towards target
                targetSwitchBlender.Update(_dt);
                //Vector3 curAimRefPosOffset = targetSwitchPosDamper.UpdateDamper();
                //Quaternion curAimRefRotOffset = targetSwitchRotDamper.UpdateDamper();
                //flattenFactorDamper.UpdateDamper();
                focusDistanceDamper.UpdateDamper();
                //aimFovDamper.UpdateDamper();
                //calculate current target aim offset
                CalcCurrentOffset(out Vector3 curAimRefPosOffset, out Quaternion curAimRefRotOffset);
                Vector3 curWorldPivotPos = scopeBasePosTransform.TransformPoint(curAimRefPosOffset);
                Vector3 aimTargetPosOffset = playerCameraPosRef.parent.InverseTransformDirection(playerCameraPosRef.position - curWorldPivotPos);
                Quaternion aimTargetRotOffset = playerCameraPosRef.localRotation * Quaternion.Inverse(scopeBasePosTransform.localRotation * curAimRefRotOffset);
                //move current aim offset towards target aim offset
                //aimProcValue = Mathf.SmoothDamp(aimProcValue, isAiming ? 1f : 0f, ref aimProcVelocity, zoomInTimeMod);
                float aimProcValue = aimProcDamper.UpdateDamper();
                curAimPosOffset = Vector3.Lerp(Vector3.zero, aimTargetPosOffset, aimProcValue);
                curAimRotOffset = Quaternion.Slerp(Quaternion.identity, aimTargetRotOffset, aimProcValue);
                //apply offset to player
                Vector3 curWorldPosOffset = playerCameraPosRef.parent.TransformDirection(curAimPosOffset);
                (playerCameraPosRef.parent.rotation * curAimRotOffset * Quaternion.Inverse(playerCameraPosRef.parent.rotation)).ToAngleAxis(out var angle, out var axis);
                playerOriginTransform.RotateAround(curWorldPivotPos, axis, angle);
                playerOriginTransform.position += curWorldPosOffset;
            }
        }

        public void CalcCurrentOffset(out Vector3 curAimRefPosOffset, out Quaternion curAimRefRotOffset)
        {
            curAimRefPosOffset = Vector3.zero;
            curAimRefRotOffset = QuaternionExt.zero;
            CurAimFlattenFactor = 0f;
            CurTargetAimFovValue = 0f;
            foreach (var data in targetSwitchBlender)
            {
                if (data.CurBlendWeight > 0)
                {
                    float curBlendWeight = data.CurBlendWeight;
                    curAimRefPosOffset += data.targetPosOffset * curBlendWeight;
                    curAimRefRotOffset = QuaternionExt.Add(curAimRefRotOffset, QuaternionExt.Scale(data.targetRotOffset, curBlendWeight));
                    CurAimFlattenFactor += data.aimRef.designedFlattenFactor * curBlendWeight;
                    CurTargetAimFovValue += data.targetAimFov * curBlendWeight;
                }
            }
        }

        public void CalcCurrentWorldPos(out Vector3 curAimRefPosWorld, out Quaternion curAimRefRotWorld)
        {
            curAimRefPosWorld = Vector3.zero;
            curAimRefRotWorld = QuaternionExt.zero;

            foreach (var data in targetSwitchBlender)
            {
                if (data.CurBlendWeight > 0)
                {
                    float curBlendWeight = data.CurBlendWeight;
                    curAimRefPosWorld += data.aimRef.transform.position * curBlendWeight;
                    curAimRefRotWorld = QuaternionExt.Add(curAimRefRotWorld, QuaternionExt.Scale(data.aimRef.transform.rotation, curBlendWeight));
                }
            }
        }
    }
}
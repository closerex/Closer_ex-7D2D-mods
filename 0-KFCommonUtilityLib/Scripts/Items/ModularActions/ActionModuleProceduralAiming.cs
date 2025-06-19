﻿using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using UnityEngine;

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
        var targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
        if (__customData.playerCameraPosRef)
        {
            if (targets.ItemFpv)
            {
                if (targets is RigTargets)
                {
                    __customData.isRigWeapon = true;
                    __customData.playerOriginTransform = targets.ItemAnimator.transform;
                    __customData.rigWeaponLocalPosition = __customData.playerOriginTransform.localPosition;
                    __customData.rigWeaponLocalRotation = __customData.playerOriginTransform.localRotation;
                }
                else
                {
                    __customData.isRigWeapon = false;
                    __customData.playerOriginTransform = __customData.playerCameraPosRef.FindInAllChildren("Hips");
                }
                __customData.playerCameraPosRef = targets.ItemFpv.Find("PlayerCameraPositionReference");
            }
            else
            {
                __customData.playerCameraPosRef = null;
            }
        }
        if (__customData.playerCameraPosRef)
        {
            __customData.aimRefTransform = targets.ItemFpv.Find("ScopeBasePositionReference");
            if (__customData.aimRefTransform)
            {
                var scopeRefTrans = __customData.aimRefTransform.Find("ScopePositionReference");
                if (!scopeRefTrans)
                {
                    scopeRefTrans = new GameObject("ScopePositionReference").transform;
                    scopeRefTrans.SetParent(__customData.aimRefTransform, false);
                }
                scopeRefTrans.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                scopeRefTrans.localScale = Vector3.one;
                __customData.aimRefTransform = scopeRefTrans;
            }
        }
        else
        {
            __customData.aimRefTransform = null;
        }

        __customData.ResetAiming();
        __customData.UpdateCurrentReference(true);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(ProceduralAimingData __customData)
    {
        __customData.ResetAiming();
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

    public class ProceduralAimingData
    {
        public ActionModuleErgoAffected.ErgoData ergoData;
        public float zoomInTime;
        public Transform aimRefTransform;
        public Transform playerCameraPosRef;
        public Transform playerOriginTransform;
        public bool isRigWeapon;
        public Vector3 rigWeaponLocalPosition;
        public Quaternion rigWeaponLocalRotation;

        public bool isAiming;
        public int curAimRefIndex = -1;
        //move curAimRefOffset towards aimRefOffset first, then move curAimOffset towards curAimRefOffset
        public Vector3 aimRefPosOffset;
        public Quaternion aimRefRotOffset;
        public Vector3 curAimPosOffset;
        public Quaternion curAimRotOffset;
        private Vector3 curAimPosVelocity;
        private Quaternion curAimRotVelocity;
        private Vector3 targetSwitchPosVelocity;
        private Quaternion targetSwitchRotVelocity;
        public List<AimReference> registeredReferences = new List<AimReference>();
        private EntityPlayerLocal holdingEntity;
        public int CurAimRefIndex
        {
            get
            {
                for (int i = registeredReferences.Count - 1; i >= 0; i--)
                {
                    if (registeredReferences[i].gameObject.activeInHierarchy)
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        public AimReference CurAimRef => curAimRefIndex >= 0 && curAimRefIndex < registeredReferences.Count ? registeredReferences[curAimRefIndex] : null;

        public float AimRefOffset { get; private set; }

        public ProceduralAimingData(ItemActionData actionData, ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleProceduralAiming _module)
        {
            holdingEntity = _invData.holdingEntity as EntityPlayerLocal;
        }

        public void ResetAiming()
        {
            isAiming = false;
            curAimRefIndex = -1;
            aimRefPosOffset = Vector3.zero;
            aimRefRotOffset = Quaternion.identity;
            if (aimRefTransform)
            {
                aimRefTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            curAimPosOffset = Vector3.zero;
            curAimRotOffset = Quaternion.identity;
            curAimPosVelocity = Vector3.zero;
            curAimRotVelocity = Quaternion.identity;
            targetSwitchPosVelocity = Vector3.zero;
            targetSwitchRotVelocity = Quaternion.identity;
            if (isRigWeapon && playerOriginTransform)
            {
                playerOriginTransform.localPosition = rigWeaponLocalPosition;
                playerOriginTransform.localRotation = rigWeaponLocalRotation;
            }
            AimRefOffset = 0;
        }

        public bool RegisterGroup(AimReference[] group, string name)
        {
            if (holdingEntity && holdingEntity.bFirstPersonView)
            {
                foreach (var reference in group)
                {
                    if (reference.index == -1)
                    {
                        reference.index = registeredReferences.Count;
                        registeredReferences.Add(reference);
                    }
                }
                UpdateCurrentReference();
                //Log.Out($"Register group {name}\n{StackTraceUtility.ExtractStackTrace()}");
                return true;
            }
            return false;
        }

        public void UpdateCurrentReference(bool snapTo = false)
        {
            curAimRefIndex = CurAimRefIndex;
            AimReference curAimRef = CurAimRef;
            if (aimRefTransform && curAimRef)
            {
                aimRefPosOffset = curAimRef.positionOffset;
                aimRefRotOffset = curAimRef.rotationOffset;
                if (curAimRef.asReference)
                {
                    Vector3 byReferenceOffset = Vector3.Project(aimRefPosOffset - aimRefTransform.parent.InverseTransformPoint(playerCameraPosRef.position), aimRefRotOffset * Vector3.forward);
                    if (curAimRef.scopeBase?.defaultReference)
                    {
                        byReferenceOffset -= Vector3.Project(curAimRef.scopeBase.defaultReference.positionOffset - aimRefTransform.parent.InverseTransformPoint(playerCameraPosRef.position), aimRefRotOffset * Vector3.forward);
                    }
                    aimRefPosOffset -= byReferenceOffset;
                    AimRefOffset = byReferenceOffset.magnitude;
                }
                else
                {
                    AimRefOffset = 0;
                }
                if (snapTo)
                {
                    aimRefTransform.localPosition = aimRefPosOffset;
                    aimRefTransform.localRotation = aimRefRotOffset;
                }
            }

            for (int i = 0; i < registeredReferences.Count; i++)
            {
                registeredReferences[i].UpdateEnableState(isAiming && curAimRefIndex == i);
            }
        }

        public void LateUpdateAiming()
        {
            if (aimRefTransform && playerCameraPosRef && playerOriginTransform && CurAimRef)
            {
                if (isRigWeapon)
                {
                    playerOriginTransform.SetLocalPositionAndRotation(rigWeaponLocalPosition, rigWeaponLocalRotation);
                }
                float zoomInTimeMod = ergoData == null ? zoomInTime : zoomInTime / ergoData.ModifiedErgo;
                zoomInTimeMod *= 0.25f;
                //move aimRef towards target
                aimRefTransform.localPosition = Vector3.SmoothDamp(aimRefTransform.localPosition, aimRefPosOffset, ref targetSwitchPosVelocity, 0.075f);
                aimRefTransform.localRotation = QuaternionUtil.SmoothDamp(aimRefTransform.localRotation, aimRefRotOffset, ref targetSwitchRotVelocity, 0.075f);
                //calculate current target aim offset
                Vector3 aimTargetPosOffset = playerCameraPosRef.InverseTransformDirection(playerCameraPosRef.position - aimRefTransform.position);
                Quaternion aimTargetRotOffset = playerCameraPosRef.localRotation * Quaternion.Inverse(aimRefTransform.parent.localRotation * aimRefTransform.localRotation);
                //move current aim offset towards target aim offset
                if (isAiming)
                {
                    curAimPosOffset = Vector3.SmoothDamp(curAimPosOffset, aimTargetPosOffset, ref curAimPosVelocity, zoomInTimeMod);
                    curAimRotOffset = QuaternionUtil.SmoothDamp(curAimRotOffset, aimTargetRotOffset, ref curAimRotVelocity, zoomInTimeMod);
                }
                else
                {
                    curAimPosOffset = Vector3.SmoothDamp(curAimPosOffset, Vector3.zero, ref curAimPosVelocity, zoomInTimeMod);
                    curAimRotOffset = QuaternionUtil.SmoothDamp(curAimRotOffset, Quaternion.identity, ref curAimRotVelocity, zoomInTimeMod);
                }
                //apply offset to player
                if (isRigWeapon)
                {
                    (playerCameraPosRef.parent.rotation * curAimRotOffset * Quaternion.Inverse(playerCameraPosRef.parent.rotation)).ToAngleAxis(out var angle, out var axis);
                    playerOriginTransform.RotateAround(aimRefTransform.position, axis, angle);
                    playerOriginTransform.position += playerCameraPosRef.TransformDirection(curAimPosOffset);
                }
                else
                {
                    playerOriginTransform.position += playerCameraPosRef.TransformDirection(curAimPosOffset);
                    (playerCameraPosRef.parent.rotation * curAimRotOffset * Quaternion.Inverse(playerCameraPosRef.parent.rotation)).ToAngleAxis(out var angle, out var axis);
                    playerOriginTransform.RotateAround(aimRefTransform.position, axis, angle);
                }
            }
        }
    }
}

[HarmonyPatch]
public static class ProceduralAimingPatches
{
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.LateUpdate))]
    [HarmonyPostfix]
    private static void Postfix_LateUpdate_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        if (__instance.inventory?.holdingItemData?.actionData?[1] is IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData> module)
        {
            if (__instance.AimingGun != module.Instance.isAiming)
            {
                module.Instance.isAiming = __instance.AimingGun;
                module.Instance.UpdateCurrentReference(true);
            }
            module.Instance.LateUpdateAiming();
        }
    }
}
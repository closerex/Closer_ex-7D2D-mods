using KFCommonUtilityLib.Attributes;
using HarmonyLib;
using UnityEngine;
using KFCommonUtilityLib;
using System.Reflection.Emit;
using System.Collections.Generic;
using UniLinq;
using KFCommonUtilityLib.Scripts.Utilities;

public struct ShotIndexRange
{
    public IntRange IndexRange;
    public Vector2 RecoilPositionStrength;
    public Vector2 RecoilRotationStrength;
    public Vector2 RecoilAngleRange;

    public static ShotIndexRange Parse(DynamicProperties prop)
    {
        var ret = new ShotIndexRange();
        StringParsers.TryParseRange(prop.GetString("IndexRange"), out ret.IndexRange);
        prop.ParseVec("RecoilPositionStrength", ref ret.RecoilPositionStrength);
        prop.ParseVec("RecoilRotationStrength", ref ret.RecoilRotationStrength);
        prop.ParseVec("RecoilAngleRange", ref ret.RecoilAngleRange);
        return ret;
    }
}

public class ShotIndexRangeGroup
{
    private ShotIndexRange[] shotRange;

    private ShotIndexRangeGroup()
    {

    }

    public static ShotIndexRangeGroup Parse(DynamicProperties props)
    {
        ShotIndexRangeGroup group = new ShotIndexRangeGroup();
        List<ShotIndexRange> list = new List<ShotIndexRange>();
        for (int i = 0; i < 99; i++)
        {
            if (props.Classes.TryGetValue($"ShotsGroupSettings{i}", out var shotProps))
            {
                list.Add(ShotIndexRange.Parse(shotProps));
            }
            else
            {
                break;
            }
        }
        if (list.Count > 0)
        {
            group.shotRange = list.ToArray();
        }
        return group;
    }

    public bool FindIndexGroup(int index, out ShotIndexRange range)
    {
        range = default;
        if (shotRange == null)
        {
            return false;
        }

        foreach (var shotRange in shotRange)
        {
            if (shotRange.IndexRange.min <= index && shotRange.IndexRange.max >= index)
            {
                range = shotRange;
                return true;
            }
        }

        return false;
    }
}

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(EFTProceduralRecoilData))]
public class ActionModuleProceduralRecoil
{
    //======
    public const float BASE_RECOIL_ROTATION_STR_MIN = 0.9f;
    public const float BASE_RECOIL_ROTATION_STR_MAX = 1.15f;
    public const float BASE_RECOIL_POSITION_STR_MIN = 0.65f;
    public const float BASE_RECOIL_POSITION_STR_MAX = 1.05f;
    public const float CONSTANT_ROTATION_STR_MULTIPLIER = 0.1399f;
    public const float INTENSITY_MULTIPLIER_CROUCHING = 0.85f;

    public struct RecoilPassiveTags
    {
        public FastTags<TagGroup.Global> WeaponRecoilModifier;
        public FastTags<TagGroup.Global> DeltaAnglePerShot;
        public FastTags<TagGroup.Global> DeltaAngleMin;
        public FastTags<TagGroup.Global> DeltaAngleMax;
        public FastTags<TagGroup.Global> CameraRecoilConversionPerc;
        public FastTags<TagGroup.Global> WeaponRotIntensity;
        public FastTags<TagGroup.Global> WeaponPosIntensity;
        public FastTags<TagGroup.Global> CameraRotIntensity;
        public FastTags<TagGroup.Global> WeaponRotIntensityMultiplier;
        public FastTags<TagGroup.Global> WeaponRotationForceDamping;
        public FastTags<TagGroup.Global> WeaponRotationForceReturnSpeed;
        public FastTags<TagGroup.Global> WeaponPositionForceDamping;
        public FastTags<TagGroup.Global> WeaponPositionForceReturnSpeed;
        public FastTags<TagGroup.Global> CameraRotationForceDamping;
        public FastTags<TagGroup.Global> CameraRotationForceReturnSpeed;
        public FastTags<TagGroup.Global> RecoilReturnBias;
        public FastTags<TagGroup.Global> RecoilReturnBiasDamping;
        public FastTags<TagGroup.Global> RecoilForceStrength;
        public FastTags<TagGroup.Global> RecoilAimingIntensity;
    }

    public static readonly FastTags<TagGroup.Global> WeaponRecoilModifer = FastTags<TagGroup.Global>.Parse("RecoilIntensityModifier");
    public static readonly FastTags<TagGroup.Global> DeltaAnglePerShot = FastTags<TagGroup.Global>.Parse("RecoilStableAngleIncreaseStep");
    public static readonly FastTags<TagGroup.Global> DeltaAngleMin = FastTags<TagGroup.Global>.Parse("ProgressRecoilAngleOnStableMin");
    public static readonly FastTags<TagGroup.Global> DeltaAngleMax = FastTags<TagGroup.Global>.Parse("ProgressRecoilAngleOnStableMax");
    public static readonly FastTags<TagGroup.Global> CameraRecoilConversionPerc = FastTags<TagGroup.Global>.Parse("RecoilCamera");
    public static readonly FastTags<TagGroup.Global> WeaponRotIntensity = FastTags<TagGroup.Global>.Parse("WeaponRotIntensity");
    public static readonly FastTags<TagGroup.Global> WeaponPosIntensity = FastTags<TagGroup.Global>.Parse("WeaponPosIntensity");
    public static readonly FastTags<TagGroup.Global> CameraRotIntensity = FastTags<TagGroup.Global>.Parse("CameraRotIntensity");
    public static readonly FastTags<TagGroup.Global> WeaponRotIntensityMultiplier = FastTags<TagGroup.Global>.Parse("RecoilCategoryMultiplierHandRotation");
    public static readonly FastTags<TagGroup.Global> WeaponRotationForceDamping = FastTags<TagGroup.Global>.Parse("RecoilDampingHandRotation");
    public static readonly FastTags<TagGroup.Global> WeaponRotationForceReturnSpeed = FastTags<TagGroup.Global>.Parse("RecoilReturnSpeedHandRotation");
    public static readonly FastTags<TagGroup.Global> WeaponPositionForceDamping = FastTags<TagGroup.Global>.Parse("RecoilDampingHandPosition");
    public static readonly FastTags<TagGroup.Global> WeaponPositionForceReturnSpeed = FastTags<TagGroup.Global>.Parse("RecoilReturnSpeedHandPosition");
    public static readonly FastTags<TagGroup.Global> CameraRotationForceDamping = FastTags<TagGroup.Global>.Parse("RecoilDampingCameraRotation");
    public static readonly FastTags<TagGroup.Global> CameraRotationForceReturnSpeed = FastTags<TagGroup.Global>.Parse("RecoilReturnSpeedCameraRotation");
    public static readonly FastTags<TagGroup.Global> RecoilReturnBias = FastTags<TagGroup.Global>.Parse("RecoilReturnPathOffsetHandRotation");
    public static readonly FastTags<TagGroup.Global> RecoilReturnBiasDamping = FastTags<TagGroup.Global>.Parse("RecoilReturnPathDampingHandRotation");
    public static readonly FastTags<TagGroup.Global> RecoilAimingIntensity = FastTags<TagGroup.Global>.Parse("RecoilAimingIntensity");
    public RecoilPassiveTags tags;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props, ItemAction __instance)
    {
        var itemTags = __instance.item.ItemTags;
        tags = new RecoilPassiveTags()
        {
            WeaponRecoilModifier = itemTags | WeaponRecoilModifer,
            DeltaAnglePerShot = itemTags | DeltaAnglePerShot,
            DeltaAngleMin = itemTags | DeltaAngleMin,
            DeltaAngleMax = itemTags | DeltaAngleMax,
            CameraRecoilConversionPerc = itemTags | CameraRecoilConversionPerc,
            WeaponRotIntensity = itemTags | WeaponRotIntensity,
            WeaponPosIntensity = itemTags | WeaponPosIntensity,
            CameraRotIntensity = itemTags | CameraRotIntensity,
            WeaponRotIntensityMultiplier = itemTags | WeaponRotIntensityMultiplier,
            WeaponRotationForceDamping = itemTags | WeaponRotationForceDamping,
            WeaponRotationForceReturnSpeed = itemTags | WeaponRotationForceReturnSpeed,
            WeaponPositionForceDamping = itemTags | WeaponPositionForceDamping,
            WeaponPositionForceReturnSpeed = itemTags | WeaponPositionForceReturnSpeed,
            CameraRotationForceDamping = itemTags | CameraRotationForceDamping,
            CameraRotationForceReturnSpeed = itemTags | CameraRotationForceReturnSpeed,
            RecoilReturnBias = itemTags | RecoilReturnBias,
            RecoilReturnBiasDamping = itemTags | RecoilReturnBiasDamping,
            RecoilForceStrength = FastTags<TagGroup.Global>.Parse("RecoilForceStrength"),
            RecoilAimingIntensity = itemTags | RecoilAimingIntensity
        };
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationChanged(ItemActionRanged __instance, ItemActionData _data, EFTProceduralRecoilData __customData)
    {
        Vector2 recoilForce = default;
        string originalValue = "0,0";
        DynamicProperties props = __instance.Properties;
        props.ParseString("WeaponRecoilForce", ref originalValue);
        recoilForce = StringParsers.ParseVector2(_data.invData.itemValue.GetPropertyOverrideForAction("WeaponRecoilForce", originalValue, __instance.ActionIndex));

        __customData.WeaponRecoilForceUp = recoilForce.x;
        __customData.WeaponRecoilForceBack = recoilForce.y;

        originalValue = "80,100";
        props.ParseString("RecoilAngleRange", ref originalValue);
        __customData.BaseRecoilRadianRange = StringParsers.ParseVector2(_data.invData.itemValue.GetPropertyOverrideForAction("RecoilAngleRange", originalValue, __instance.ActionIndex)) * Mathf.Deg2Rad;

        originalValue = "5";
        props.ParseString("StableShotIndex", ref originalValue);
        __customData.StableShotIndex = StringParsers.ParseSInt32(_data.invData.itemValue.GetPropertyOverrideForAction("StableShotIndex", originalValue, __instance.ActionIndex));

        originalValue = "true";
        props.ParseString("RampUpRecoil", ref originalValue);
        __customData.RampRecoil = StringParsers.ParseBool(_data.invData.itemValue.GetPropertyOverrideForAction("RampUpRecoil", originalValue, __instance.ActionIndex));

        originalValue = "3";
        props.ParseString("RampRecoilIndex", ref originalValue);
        __customData.RampRecoilIndex = StringParsers.ParseSInt32(_data.invData.itemValue.GetPropertyOverrideForAction("RampRecoilIndex", originalValue, __instance.ActionIndex));

        __customData.ShotRangeGroup = ShotIndexRangeGroup.Parse(props);

        __customData.playerOriginTransform = null;
        if (_data.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
        {
            __customData.playerCameraTransform = player.cameraTransform;
            __customData.targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);

            __customData.recoilPivotTransform = null;
            __customData.hasPivotOverride = false;
            if (__customData.targets)
            {
                __customData.recoilPivotTransform = AnimationRiggingManager.GetAddPartTransformOverride(__customData.targets.transform, "RecoilPivot");
            }
            if (__customData.recoilPivotTransform)
            {
                __customData.hasPivotOverride = true;
            }
            else
            {
                __customData.recoilPivotTransform = player.cameraTransform.FindInAllChildren("RightHand");
            }

            if (__customData.targets && __customData.targets.ItemFpv && __customData.targets is RigTargets)
            {
                __customData.playerOriginTransform = __customData.targets.ItemAnimator.transform;
                __customData.isRigWeapon = true;
            }
            else
            {
                __customData.playerOriginTransform = player.cameraTransform.FindInAllChildren("Hips");
                __customData.isRigWeapon = false;
            }

            var oldRecoil = __customData.targets.ItemAnimator?.GetComponent<AnimationRandomRecoil>();
            if (oldRecoil)
            {
                oldRecoil.enabled = false;
            }

            CameraLateUpdater.RegisterUpdater(__customData);
        }

        __customData.ResetRecoil();
        if (!EFTProceduralRecoilData.dontUpdateParam || (EFTProceduralRecoilData.dontUpdateParam && !__customData.passivesInited))
        {
            CalcRecoilParams(__customData, _data as ItemActionRanged.ItemActionDataRanged);
            __customData.passivesInited = true;
        }
        __customData.isHolding = true;
        //CalcDampFactors(__customData, _data as ItemActionRanged.ItemActionDataRanged);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(EFTProceduralRecoilData __customData)
    {
        __customData.ResetRecoil();
        __customData.isHolding = false;
        CameraLateUpdater.UnregisterUpdater(__customData);
    }

    //[HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    //public void Postfix_OnHoldingUpdate(EFTProceduralRecoilData __customData, ItemActionData _actionData)
    //{
    //    if (_actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
    //    {
    //        bool aimingGun = player.AimingGun;
    //        if (aimingGun)
    //        {
    //            __customData.WeaponRotIntensity = 0.75f;
    //            //__customData.WeaponPosIntensity = 0f;
    //        }
    //        else
    //        {
    //            __customData.WeaponRotIntensity = 1f;
    //            //__customData.WeaponPosIntensity = 1f;
    //        }
    //    }
    //}

    [HarmonyPatch(nameof(ItemActionRanged.onHoldingEntityFired)), MethodTargetPostfix]
    public void Postfix_onHoldingEntityFired(ItemActionData _actionData, EFTProceduralRecoilData __customData)
    {
        if (_actionData.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView)
        {
            if (!EFTProceduralRecoilData.dontUpdateParam)
            {
                CalcRecoilParams(__customData, __customData.rangedData);
            }
            __customData.AddRecoilForce(EffectManager.GetValue(CustomEnums.CustomTaggedEffect, _actionData.invData.itemValue, 1, player, null, tags.RecoilForceStrength, false, true, false, false, false, 1, false, false));
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.fireShot)), MethodTargetTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_fireShot(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_ray = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetLookRay));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_ray))
            {
                codes.RemoveRange(i - 1, 2);
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    CodeInstruction.Call(typeof(ActionModuleProceduralRecoil), nameof(GetLookRayOverride))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.GetActionEffectsValues)), MethodTargetTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler_ItemActionLauncher_getImageActionEffectsStartPosAndDirection(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_ray = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetLookRay));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_ray))
            {
                codes.RemoveRange(i - 3, 4);
                codes.InsertRange(i - 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(ActionModuleProceduralRecoil), nameof(GetLookRayOverride))
                });
                break;
            }
        }
        return codes;
    }

    public static Ray GetLookRayOverride(ItemActionData data)
    {
        if (data.invData.holdingEntity is EntityPlayerLocal player && player.bFirstPersonView && data is IModuleContainerFor<EFTProceduralRecoilData> dataModule && data.invData.holdingEntity.AimingGun)
        {
            var aimingModule = (data.invData.actionData[1] as IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData>)?.Instance;
            if (aimingModule != null)
            {
                Transform transform = aimingModule.CurAimRef.transform;
                return new Ray(transform.position + Origin.position, transform.forward);
            }
        }
        return data.invData.holdingEntity.GetLookRay();
    }

    public void CalcRecoilParams(EFTProceduralRecoilData recoilData, ItemActionRanged.ItemActionDataRanged rangedData)
    {
        recoilData.WeaponRecoilModifier = Mathf.Max(0, EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 1, rangedData.invData.holdingEntity, null, tags.WeaponRecoilModifier));
        recoilData.BaseWeaponRecoilStrRot = new Vector2(BASE_RECOIL_ROTATION_STR_MIN, BASE_RECOIL_ROTATION_STR_MAX) * (recoilData.WeaponRecoilForceUp * recoilData.WeaponRecoilModifier + 20) * CONSTANT_ROTATION_STR_MULTIPLIER;
        recoilData.BaseWeaponRecoilStrPos = new Vector2(BASE_RECOIL_POSITION_STR_MIN, BASE_RECOIL_POSITION_STR_MAX) * (recoilData.WeaponRecoilForceBack *  recoilData.WeaponRecoilModifier + 20) * CONSTANT_ROTATION_STR_MULTIPLIER;

        recoilData.DeltaAnglePerShot = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 2.5f, rangedData.invData.holdingEntity, null, tags.DeltaAnglePerShot);
        recoilData.DeltaAngleRange.x = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0, rangedData.invData.holdingEntity, null, tags.DeltaAngleMin);
        recoilData.DeltaAngleRange.y = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 30, rangedData.invData.holdingEntity, null, tags.DeltaAngleMax);
        recoilData.CameraRecoilConversionPerc = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.04f, rangedData.invData.holdingEntity, null, tags.CameraRecoilConversionPerc);
        recoilData.WeaponRotIntensity = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 1, rangedData.invData.holdingEntity, null, tags.WeaponRotIntensity);
        recoilData.WeaponPosIntensity = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 1, rangedData.invData.holdingEntity, null, tags.WeaponPosIntensity);
        recoilData.CameraRotIntensity = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 1, rangedData.invData.holdingEntity, null, tags.CameraRotIntensity);
        recoilData.WeaponRotIntensityMultiplier = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.2f, rangedData.invData.holdingEntity, null, tags.WeaponRotIntensityMultiplier);
        recoilData.WeaponRotationForceDamping = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.85f, rangedData.invData.holdingEntity, null, tags.WeaponRotationForceDamping);
        recoilData.WeaponRotationForceReturnSpeed = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 3, rangedData.invData.holdingEntity, null, tags.WeaponRotationForceReturnSpeed);
        recoilData.WeaponPositionForceDamping = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.5f, rangedData.invData.holdingEntity, null, tags.WeaponPositionForceDamping);
        recoilData.WeaponPositionForceReturnSpeed = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.08f, rangedData.invData.holdingEntity, null, tags.WeaponPositionForceReturnSpeed);
        recoilData.CameraRotationForceDamping = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.5f, rangedData.invData.holdingEntity, null, tags.CameraRotationForceDamping);
        recoilData.CameraRotationForceReturnSpeed = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.05f, rangedData.invData.holdingEntity, null, tags.CameraRotationForceReturnSpeed);
        recoilData.RecoilReturnBias = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.01f, rangedData.invData.holdingEntity, null, tags.RecoilReturnBias);
        recoilData.BiasDamping = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 0.48f, rangedData.invData.holdingEntity, null, tags.RecoilReturnBiasDamping);
        recoilData.HandRotValueAimIntensity = EffectManager.GetValue(CustomEnums.CustomTaggedEffect, rangedData.invData.itemValue, 1f, rangedData.invData.holdingEntity, null, tags.RecoilAimingIntensity);
    }

    //public void CalcDampFactors(ProceduralRecoilData recoilData, ItemActionRanged.ItemActionDataRanged rangedData)
    //{
    //    recoilData.recoilFollowDampPerc = EffectManager.GetValue(CustomEnums.PRDampPerc, rangedData.invData.itemValue, 0f, rangedData.invData.holdingEntity);
    //    recoilData.recoilFollowReturnSpeed = EffectManager.GetValue(CustomEnums.PRReturnSpeed, rangedData.invData.itemValue, 1f, rangedData.invData.holdingEntity);
    //}

    public class EFTProceduralRecoilData : IRootMovementUpdater
    {
        public ItemActionRanged.ItemActionDataRanged rangedData;
        public ActionModuleProceduralRecoil module;
        public ItemInventoryData invData;
        public Transform playerOriginTransform;
        public Transform recoilPivotTransform;
        public Transform playerCameraTransform;
        public AnimationTargetsAbs targets;
        public bool isRigWeapon;
        public bool hasPivotOverride;
        public int actionIndex;
        public bool isHolding;
        public static bool dontUpdateParam;
        public bool passivesInited;

        // initial weapon recoil property
        public float WeaponRecoilForceUp, WeaponRecoilForceBack;
        public ShotIndexRangeGroup ShotRangeGroup;

        //======
        // the recoil angle range in radian, where 90 is up and 0 is right
        public Vector2 BaseRecoilRadianRange;
        // the amount of shots before recoil state is considered stable
        public int StableShotIndex = 3;
        // whether there is a ramping stage after reaching RampRecoilIndex
        // where the recoil force is multiplied by clamp(1, StableShotIndex) / StableShotIndex
        public bool RampRecoil = true;
        public int RampRecoilIndex = 1;

        //====== temp
        // modifier for the weapon recoil force
        public float WeaponRecoilModifier;
        
        //======
        // calculated basic weapon recoil values, multiplied by mod/perk modifiers
        public Vector2 BaseWeaponRecoilStrRot, BaseWeaponRecoilStrPos;
        // angle increase per shot
        public float DeltaAnglePerShot;
        // delta angle clamp value
        public Vector2 DeltaAngleRange;
        // the percentage of the recoil strength that is converted to camera kick,
        // which in our case should also be applied inversely to the weapon
        public float CameraRecoilConversionPerc;
        // the intensity modifier of the accumulated recoil force
        public float WeaponRotIntensity = 1, WeaponPosIntensity = 1, CameraRotIntensity = 1;
        // the base intensity multiplier of incoming weapon rotation force
        public float WeaponRotIntensityMultiplier;
        // damping and return speed, only need to set for weapon rotation
        public float WeaponRotationForceDamping, WeaponRotationForceReturnSpeed;
        public float WeaponPositionForceDamping = 0.5f, WeaponPositionForceReturnSpeed = 0.08f;
        public float CameraRotationForceDamping = 0.5f, CameraRotationForceReturnSpeed = 0.05f;

        //======
        // offset perc during recoil return to apply
        public float RecoilReturnBias = 0.01f;
        // recoil return bias damping
        public float BiasDamping;

        //======
        // x -> up (negative) | y -> hor (positive = right) | z -> back (positive)
        public Vector3 RecoilDirection;

        //====== hand rotation fields
        public bool IsStable;
        public bool IsReturning;
        public bool IsHandRotDirty;
        public Vector2 CurrentStableRotationOffset;
        public float RampMultiplier;
        public Vector2 CurrentRotationOffset;
        public float CurrentRotationAccumulated;
        public float HandRotValueAimIntensity = 1f;
        public Vector2 HandRotValueCur, HandRotValuePrev, HandRotValueApply, HandRotVelocity, HandRotForce;
        public float HandRotValueXAfterRecoil = 0.01f;
        public AnimationCurve HandRotReturnSpeedCurve = new AnimationCurve(new Keyframe(0, 0.008f, 0.0002f, 0.0002f) { inWeight = 0, outWeight = 0.0775f },
                                                                           new Keyframe(2.5f, 0.008f, 0.0001f, 0.0001f) { inWeight = 0.0717f, outWeight = 0 })
        {
            preWrapMode = WrapMode.ClampForever,
            postWrapMode = WrapMode.ClampForever
        };
        public float HandRotCurveTime;
        public Vector2 HandRotStableOffsetRange = new Vector2(0.1f, 8f);
        public Vector2 TargetStableRotationOffset;
        public float AutoFireReturnSpeed;
        public int LastReturnOffsetSign;
        public float RecoilOffsetImpulse;
        public float RecoilOffsetLerpSpeed = 0.01f;

        //====== hand position fields
        public bool IsHandPosDirty;
        public float HandPosValue, HandPosVelocity, HandPosForce;

        //====== cam rotation fields
        public bool IsCamRotDirty;
        public Vector2 CamRotValue, CamRotVelocity, CamRotForce;

        public int Priority => 200 + actionIndex;

        public EFTProceduralRecoilData(ItemActionData __instance, ItemInventoryData _inventoryData, int _indexInEntityOfAction, ActionModuleProceduralRecoil __customModule)
        {
            rangedData = __instance as ItemActionRanged.ItemActionDataRanged;
            module = __customModule;
            invData = _inventoryData;
            actionIndex = _indexInEntityOfAction;
        }

        public void AddRecoilForce(float forceStr = 1)
        {
            if (rangedData.curBurstCount >= StableShotIndex)
            {
                SetStable(true);
            }

            if (RampRecoil && rangedData.curBurstCount >= RampRecoilIndex)
            {
                RampMultiplier = Mathf.Clamp01((float)rangedData.curBurstCount / StableShotIndex);
            }
            else
            {
                RampMultiplier = 1;
            }

            CalcRecoilForceStr(forceStr, out float rotStr, out float posStr);
            CalcRecoilDirRadian(out Vector2 dirRad);
            CalcFinalRecoilDirection(dirRad, rotStr, posStr);
            RedirectRecoilForceToHandRot();
            RedirectRecoilForceToPosAndCam();
            ProceduralRecoilUpdater.LastShotTime = Time.time + 0.2f;
        }

        public void ResetRecoil()
        {
            IsStable = false;
            IsReturning = false;
            IsHandRotDirty = false;
            CurrentStableRotationOffset = Vector2.zero;
            CurrentRotationOffset = Vector2.zero;
            CurrentRotationAccumulated = 0;
            HandRotValueCur = HandRotValuePrev = HandRotVelocity = HandRotForce = Vector2.zero;
            HandRotValueXAfterRecoil = 0.01f;
            HandRotCurveTime = 0f;
            TargetStableRotationOffset = Vector2.zero;
            AutoFireReturnSpeed = 0f;
            LastReturnOffsetSign = 0;

            IsHandPosDirty = false;
            HandPosValue = HandPosVelocity = HandPosForce = 0f;

            IsCamRotDirty = false;
            CamRotValue = CamRotVelocity = CamRotForce = Vector2.zero;

            //ProceduralRecoilUpdater.SetTargetRecoilPosOffset(Vector3.zero);
        }
        

        public void FixedUpdate(float dt)
        {
            if (!isHolding)
            {
                return;
            }
            if (rangedData.state == ItemActionFiringState.Off)
            {
                SetStable(false);
            }
            FixedUpdateHandRot(dt);
            FixedUpdateHandPos(dt);
            FixedUpdateCamRot(dt);
        }

        public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRigWeapon, float dt)
        {
            if (!isHolding)
            {
                return;
            }
            var aimingData = ((IModuleContainerFor<ActionModuleProceduralAiming.ProceduralAimingData>)invData.actionData[1]).Instance;
            //ProceduralRecoilUpdater.InverseWorldCamKickOffsetCur.ToAngleAxis(out float camRotAngle, out Vector3 camRotAxis);
            //playerOriginTransform.RotateAround(aimingData.aimRefTransform.position, camRotAxis, camRotAngle);
            AimReference aimref = aimingData.CurAimRef;
            Transform aimRefTransform = aimref?.transform ?? aimingData.aimRefTransform;
            if (!aimRefTransform)
            {
                return;
            }
            Vector3 prevAimRefPos = aimRefTransform.position;

            // right hand forward = right, right = -up, up = -forward
            Vector3 targetHandPosOffset = -playerCameraTransform.forward * HandPosValue;
            //Vector3 targetHandForward = Quaternion.AngleAxis(HandRotValueCur.x, playerCameraTransform.right)
            //                            * (Quaternion.AngleAxis(HandRotValueCur.y, playerCameraTransform.up) * playerCameraTransform.forward);

            //Quaternion.FromToRotation(playerCameraTransform.forward, targetHandForward).ToAngleAxis(out float worldAngleOffset, out Vector3 worldRotAxis);
            ((playerCameraTransform.rotation * Quaternion.Euler(HandRotValueApply)) * Quaternion.Inverse(playerCameraTransform.rotation)).ToAngleAxis(out float worldAngleOffset, out Vector3 worldRotAxis);
            if (isRigWeapon && !hasPivotOverride)
            {
                playerOriginTransform.RotateAround(recoilPivotTransform.position, worldRotAxis, worldAngleOffset);
                playerOriginTransform.position += targetHandPosOffset;
            }
            else
            {
                playerOriginTransform.position += targetHandPosOffset;
                playerOriginTransform.RotateAround(recoilPivotTransform.position, worldRotAxis, worldAngleOffset);
            }
            Vector3 alignmentPos = aimref?.alignmentTarget?.position ?? (aimRefTransform.position - aimingData.AimRefOffset * aimRefTransform.forward);
            ((playerCameraTransform.rotation * Quaternion.Euler(ProceduralRecoilUpdater.CamAimRecoilRotOffsetCur)) * Quaternion.Inverse(playerCameraTransform.rotation)).ToAngleAxis(out worldAngleOffset, out worldRotAxis);
            playerOriginTransform.RotateAround(alignmentPos, worldRotAxis, worldAngleOffset);
            ProceduralRecoilUpdater.LateUpdateCamRecoilPosOffset(aimRefTransform.position - prevAimRefPos, dt, invData.holdingEntity.AimingGun);
            playerOriginTransform.position -= ProceduralRecoilUpdater.GetCurRecoilPosOffset();

            //ProceduralRecoilUpdater.InverseWorldCamKickOffsetCur.ToAngleAxis(out float camRotAngle, out Vector3 camRotAxis);
            //playerOriginTransform.RotateAround(aimRefTransform.position - aimingData.AimRefOffset * aimRefTransform.forward, camRotAxis, camRotAngle);
        }

        private void FixedUpdateHandRot(float dt)
        {
            bool isAiming = invData.holdingEntity.AimingGun;
            if (IsHandRotDirty)
            {
                float weaponRotIntensity = WeaponRotIntensity;
                if (isAiming)
                {
                    weaponRotIntensity *= 0.75f;
                }
                HandRotVelocity += HandRotForce * weaponRotIntensity;
                HandRotForce = Vector2.zero;
                HandRotCurveTime = Mathf.Min(HandRotValueCur.magnitude, HandRotCurveTime);
                if (IsStable)
                {
                    float stableOffset = Random.Range(HandRotStableOffsetRange.x, HandRotStableOffsetRange.y);
                    TargetStableRotationOffset = CurrentStableRotationOffset + HandRotVelocity * stableOffset;
                    Vector2 stableOffsetDir = TargetStableRotationOffset - HandRotValueCur;
                    if (stableOffsetDir.magnitude > 0.95f)
                    {
                        stableOffsetDir *= 0.95f;
                        TargetStableRotationOffset = HandRotValueCur + stableOffsetDir;
                    }
                }
                IsHandRotDirty = false;
            }

            if (rangedData.state == ItemActionFiringState.Off)
            {
                AutoFireReturnSpeed = WeaponRotationForceReturnSpeed * HandRotReturnSpeedCurve.Evaluate(HandRotCurveTime);
                //mount?
            }

            if (IsHandRotDirty && rangedData.state != ItemActionFiringState.Off && !IsStable)
            {
                AutoFireReturnSpeed *= RampMultiplier;
            }

            HandRotCurveTime += dt;
            HandRotVelocity *= WeaponRotationForceDamping;
            if (!IsStable)
            {
                HandRotVelocity -= HandRotValueCur * AutoFireReturnSpeed;
                HandRotValueCur += HandRotVelocity;
            }
            else
            {
                HandRotValueCur = HandRotValueCur + (TargetStableRotationOffset - HandRotValueCur) * 0.2f;
            }

            UpdateReturnBias(dt);

            if (rangedData.state == ItemActionFiringState.Off)
            {
                if (HandRotValueXAfterRecoil != 0)
                {
                    float speedDamp = Mathf.Abs(Mathf.Clamp(HandRotValueCur.x, HandRotValueXAfterRecoil, -0.01f) / HandRotValueXAfterRecoil);
                    RecoilOffsetLerpSpeed = Mathf.Clamp(1 - speedDamp, 0.01f, 1f) * 0.01f;
                    HandRotValueCur = Vector2.Lerp(HandRotValueCur, Vector2.zero, RecoilOffsetLerpSpeed);
                    //Log.Out($"LerpBack RecoilOffsetLerpSpeed {RecoilOffsetLerpSpeed} HandRotValueCurY {HandRotValueCur.y}");
                }
            }
            HandRotValuePrev = HandRotValueCur;

            if (isAiming)
            {
                //HandRotValueApply = Vector2.Lerp(HandRotValueApply, new Vector2(HandRotValueCur.x * HandRotValueAimIntensity, HandRotValueCur.y), 8 * dt);
                HandRotValueApply = new Vector2(HandRotValueCur.x * HandRotValueAimIntensity, HandRotValueCur.y);
            }
            else
            {
                //HandRotValueApply = Vector2.Lerp(HandRotValueApply, HandRotValueCur, 8 * dt);
                HandRotValueApply = HandRotValueCur;
            }
        }

        private void FixedUpdateHandPos(float dt)
        {
            if (IsHandPosDirty)
            {
                HandPosVelocity += HandPosForce * WeaponPosIntensity;
                HandPosForce = 0;
                IsHandPosDirty = false;
            }

            HandPosVelocity -= HandPosValue * WeaponPositionForceReturnSpeed;
            HandPosVelocity *= WeaponPositionForceDamping;
            HandPosValue += HandPosVelocity;
        }

        private void FixedUpdateCamRot(float dt)
        {
            if (IsCamRotDirty)
            {
                CamRotVelocity += CamRotForce * CameraRotIntensity;
                CamRotForce = Vector2.zero;
                IsCamRotDirty = false;
            }
            CamRotVelocity -= CamRotValue * CameraRotationForceReturnSpeed;
            CamRotVelocity *= CameraRotationForceDamping;
            CamRotValue += CamRotVelocity;
        }

        public void SetStable(bool stable)
        {
            if (stable)
            {
                if (!IsStable)
                {
                    IsStable = true;
                    CurrentStableRotationOffset = CurrentRotationOffset;
                }
            }
            else if (IsStable)
            {
                CurrentRotationAccumulated = 0;
                IsStable = false;
            }
        }

        private void CalcRecoilForceStr(float forceStr, out float rotStr, out float posStr)
        {
            //todo: random range according to burst count
            Vector2 rotRange = BaseWeaponRecoilStrRot;
            Vector2 posRange = BaseWeaponRecoilStrPos;
            if (ShotRangeGroup.FindIndexGroup(rangedData.curBurstCount, out var range))
            {
                rotRange += range.RecoilRotationStrength;
                posRange += range.RecoilPositionStrength;
            }
            rotStr = Random.Range(rotRange.x, rotRange.y) * forceStr;
            posStr = Random.Range(posRange.x, posRange.y) * forceStr;
        }

        private void CalcRecoilDirRadian(out Vector2 dirRad)
        {
            Vector2 randomRange = default;
            if (ShotRangeGroup.FindIndexGroup(rangedData.curBurstCount, out var range))
            {
                randomRange = range.RecoilAngleRange;
            }
            if (IsStable)
            {
                CurrentRotationAccumulated = Mathf.Clamp(CurrentRotationAccumulated + DeltaAnglePerShot, DeltaAngleRange.x, DeltaAngleRange.y);
                randomRange += new Vector2(CurrentRotationAccumulated, -CurrentRotationAccumulated);
            }
            dirRad = BaseRecoilRadianRange + randomRange * Mathf.Deg2Rad;
        }

        private void CalcFinalRecoilDirection(Vector2 recoilDirRad, float recoilRotStr, float recoilPosStr)
        {
            float recoilRad = Random.Range(recoilDirRad.x, recoilDirRad.y);
            float poseFactor = rangedData.invData.holdingEntity.IsCrouching ? ActionModuleProceduralRecoil.INTENSITY_MULTIPLIER_CROUCHING : 1f;
            RecoilDirection = new Vector3(-Mathf.Sin(recoilRad) * recoilRotStr * poseFactor, Mathf.Cos(recoilRad) * recoilRotStr * poseFactor, recoilPosStr * poseFactor); //aiming intensity?
        }

        private void RedirectRecoilForceToHandRot()
        {
            HandRotForce += new Vector2(RecoilDirection.x, RecoilDirection.y) * 0.15f * WeaponRotIntensityMultiplier * RampMultiplier; //perhaps mount multiplier in the future?
            IsReturning = true;
            if (IsStable)
            {
                HandRotVelocity = Vector2.zero;
            }
            IsHandRotDirty = true;
        }

        private void RedirectRecoilForceToPosAndCam()
        {
            HandPosForce += RecoilDirection.z * 0.0007f;
            IsHandPosDirty = true;

            CamRotForce += new Vector2(RecoilDirection.x, -RecoilDirection.y) * CameraRecoilConversionPerc;
            IsCamRotDirty = true;
        }

        private void UpdateReturnBias(float dt)
        {
            if (HandRotValueCur.x < HandRotValuePrev.x && IsReturning)
            {
                //skipped the random offset
                HandRotValueXAfterRecoil = HandRotValueCur.x;
                RecoilOffsetImpulse = Mathf.Abs(HandRotValueCur.x * WeaponRotationForceReturnSpeed) * 0.01f;
                //mount?

                if (HandRotValueCur.y > 0)
                {
                    LastReturnOffsetSign = 1;
                }
                else if (HandRotValueCur.y < 0)
                {
                    LastReturnOffsetSign = -1;
                }
                else
                {
                    LastReturnOffsetSign = Random.Range(-1f, 1f) >= 0f ? 1 : -1;
                }
                //Log.Out($"Upkick RecoilOffsetImpulse {RecoilOffsetImpulse} Sign {LastReturnOffsetSign} HandRotVelocityY {HandRotVelocity.y} HandRotValueCurY {HandRotValueCur.y}");

                return;
            }

            if (rangedData.state == ItemActionFiringState.Off && IsReturning)
            {
                RecoilOffsetImpulse *= BiasDamping;
                if (RecoilOffsetImpulse <= 0.001f)
                {
                    IsReturning = false;
                    //RecoilOffsetImpulse = 0f;
                    //LastReturnOffsetSign = 0;
                    return;
                }
                HandRotVelocity.y += RecoilOffsetImpulse * LastReturnOffsetSign;
                //Log.Out($"Apply RecoilOffsetImpulse {RecoilOffsetImpulse} Sign {LastReturnOffsetSign} HandRotVelocityY {HandRotVelocity.y} HandRotValueCurY {HandRotValueCur.y}");
            }
        }
    }
}

public static class ProceduralRecoilUpdater
{
    public static EntityPlayerLocal player;
    public static ActionModuleProceduralRecoil.EFTProceduralRecoilData RecoilData
    {
        get
        {
            return (player?.inventory?.holdingItemData?.actionData?[MultiActionManager.GetActionIndexForEntity(player)] as IModuleContainerFor<ActionModuleProceduralRecoil.EFTProceduralRecoilData>)?.Instance;
        }
    }

    private static float SetAimIntensity
    {
        set
        {
            if (RecoilData != null)
            {
                RecoilData.HandRotValueAimIntensity = value;
            }
        }
    }

    private static bool SetDontUpdate
    {
        set
        {
            ActionModuleProceduralRecoil.EFTProceduralRecoilData.dontUpdateParam = value;
        }
    }

    //====== cam rotation apply
    public static Vector3 PreUpdateCamFwd;
    public static float CamRecoilLerpSpeed, CamRecoilLerpSpeedStep = 0.1f;
    public static Vector2 CamRecoilLerpSpeedRange = new Vector2(0.1f, 0.2f);
    public static Vector2 CamRecoilOffsetStable, CamRecoilOffsetCur;
    public static Quaternion LocalCamRotOffsetCur, InverseWorldCamShakeOffsetCur, InverseWorldCamKickOffsetCur, InverseWorldCamTotalOffsetCur;
    public static float CamAimRecoilPosSmoothIn = 8f, CamAimRecoilPosSmoothOut = 6f;
    private static Vector3 CamAimRecoilPosOffsetCur, CamAimRecoilPosOffsetStable/*, CamAimRecoilPosOffsetTarget*/;
    public static float LastShotTime, CamAimRecoilPosLerpSpeedXYMin = 7, CamAimRecoilPosLerpSpeedXYMax = 8, CamAimRecoilPosLerpSpeedStep = 5;
    public static Vector2 CamAimRecoilRotOffsetCur;
    public static float CamAimRecoilRotOffsetLerpSpeed = 15f;

    private static PRCameraUpdate PRCameraUpdater = new PRCameraUpdate();
    private static PRApply PRApplyUpdater = new PRApply();
    public static void InitPlayer(EntityPlayerLocal player)
    {
        ProceduralRecoilUpdater.player = player;
        PreUpdateCamFwd = player.cameraTransform?.forward ?? Vector3.forward;
        CamRecoilLerpSpeed = 0.01f;
        CamRecoilOffsetCur = Vector2.zero;
        CamRecoilOffsetStable = Vector2.zero;
        LocalCamRotOffsetCur = Quaternion.identity;
        InverseWorldCamShakeOffsetCur = Quaternion.identity;
        InverseWorldCamKickOffsetCur = Quaternion.identity;
        InverseWorldCamTotalOffsetCur = Quaternion.identity;
        CamAimRecoilPosOffsetCur = Vector3.zero;
        CamAimRecoilPosOffsetStable = Vector3.zero;
        //CamAimRecoilPosOffsetTarget = Vector3.zero;
        LastShotTime = 0;
        CamAimRecoilRotOffsetCur = Vector2.zero;

        CameraLateUpdater.RegisterUpdater(PRCameraUpdater);
        CameraLateUpdater.RegisterUpdater(PRApplyUpdater);
    }

    //public static void SetTargetRecoilPosOffset(Vector3 offset)
    //{
    //    CamAimRecoilPosOffsetTarget = player.cameraTransform.InverseTransformDirection(offset);
    //}

    public static Vector3 GetCurRecoilPosOffset()
    {
        return player.cameraTransform.TransformDirection(CamAimRecoilPosOffsetCur);
    }

    public static void FixedUpdate(float dt)
    {
        if (!player)
        {
            return;
        }
        ActionModuleProceduralRecoil.EFTProceduralRecoilData recoilData = RecoilData;
        if (recoilData != null)
        {
            recoilData.FixedUpdate(dt);
        }
    }

    public static void LateUpdateCameraRot(float dt)
    {
        if (!player)
        {
            return;
        }
        PreUpdateCamFwd = player.cameraTransform.forward;
        ActionModuleProceduralRecoil.EFTProceduralRecoilData recoilData = RecoilData;
        if (recoilData != null)
        {
            var rangedData = recoilData.rangedData;
            bool aiming = rangedData.invData.holdingEntity.AimingGun;
            if (recoilData.HandRotValueCur != Vector2.zero)
            {
                //when shooting while aiming, adds a portion of hand rot to camera;
                //return to 0 when not aiming or shooting
                if (rangedData.state != ItemActionFiringState.Off && aiming)
                {
                    CamRecoilLerpSpeed = Mathf.Clamp(CamRecoilLerpSpeed + CamRecoilLerpSpeedStep * dt, CamRecoilLerpSpeedRange.x, CamRecoilLerpSpeedRange.y);
                    if (!recoilData.IsStable)
                    {
                        CamRecoilOffsetStable = recoilData.HandRotValueCur;
                    }
                    CamRecoilOffsetCur = Vector2.Lerp(CamRecoilOffsetCur, CamRecoilOffsetStable, CamRecoilLerpSpeed);
                }
                else
                {
                    CamRecoilLerpSpeed = Mathf.Clamp(CamRecoilLerpSpeed - CamRecoilLerpSpeedStep * dt, CamRecoilLerpSpeedRange.x, CamRecoilLerpSpeedRange.y);
                    if (rangedData.state == ItemActionFiringState.Off && aiming)
                    {
                        CamRecoilOffsetStable = CamRecoilOffsetCur = Vector2.Lerp(CamRecoilOffsetCur, recoilData.HandRotValueCur, CamRecoilLerpSpeed);
                    }
                    else
                    {
                        CamRecoilOffsetStable = CamRecoilOffsetCur = Vector2.Lerp(CamRecoilOffsetCur, Vector2.zero, CamRecoilLerpSpeed);
                    }
                }
            }
            else
            {
                CamRecoilLerpSpeed = Mathf.Clamp(CamRecoilLerpSpeed - CamRecoilLerpSpeedStep * dt, CamRecoilLerpSpeedRange.x, CamRecoilLerpSpeedRange.y);
                CamRecoilOffsetStable = CamRecoilOffsetCur = Vector2.Lerp(CamRecoilOffsetCur, Vector2.zero, CamRecoilLerpSpeed);
            }

            //if (CamRecoilOffsetCur.sqrMagnitude > 0.0001f)
            //{
            //    Log.Out($"CamRecoilOffsetCur {CamRecoilOffsetCur}, CamRecoilOffsetStable {CamRecoilOffsetStable}, HandRotValueCur {recoilData.HandRotValueCur}");
            //}

            if (aiming && rangedData.state != ItemActionFiringState.Off)
            {
                CamAimRecoilRotOffsetCur = Vector2.Lerp(CamAimRecoilRotOffsetCur, -recoilData.HandRotValueApply, CamAimRecoilRotOffsetLerpSpeed * dt);
            }
            else
            {
                CamAimRecoilRotOffsetCur = Vector2.Lerp(CamAimRecoilRotOffsetCur, Vector2.zero, 5 * dt);
            }
        }
        else
        {
            CamRecoilLerpSpeed = Mathf.Clamp(CamRecoilLerpSpeed - CamRecoilLerpSpeedStep * dt, CamRecoilLerpSpeedRange.x, CamRecoilLerpSpeedRange.y);
            CamRecoilOffsetStable = CamRecoilOffsetCur = Vector2.Lerp(CamRecoilOffsetCur, Vector2.zero, CamRecoilLerpSpeed);
            CamAimRecoilPosOffsetStable = CamAimRecoilPosOffsetCur = Vector3.Lerp(CamAimRecoilPosOffsetCur, Vector3.zero, CamAimRecoilPosSmoothOut * dt);
            CamAimRecoilRotOffsetCur = Vector2.Lerp(CamAimRecoilRotOffsetCur, Vector2.zero, 5 * dt);
        }
        Vector2 realCamRecoilOffset = CamRecoilOffsetCur;
        if (recoilData != null)
        {
            realCamRecoilOffset += recoilData.CamRotValue;
        }
        //realCamRecoilOffset.x = Mathf.Min(0, realCamRecoilOffset.x);
        LocalCamRotOffsetCur = Quaternion.Euler(realCamRecoilOffset);
        Quaternion targetCameraRotation = Quaternion.Euler(CamRecoilOffsetCur) * player.cameraTransform.localRotation;
        Quaternion cameraParentRotation = player.cameraTransform.parent?.rotation ?? Quaternion.identity;
        InverseWorldCamKickOffsetCur = player.cameraTransform.rotation * Quaternion.Inverse(cameraParentRotation * targetCameraRotation);

        if (recoilData != null)
        {
            targetCameraRotation = Quaternion.Euler(recoilData.CamRotValue) * player.cameraTransform.localRotation;
            InverseWorldCamShakeOffsetCur = player.cameraTransform.rotation * Quaternion.Inverse(cameraParentRotation * targetCameraRotation);
        }
        else
        {
            InverseWorldCamShakeOffsetCur = Quaternion.identity;
        }
        targetCameraRotation = LocalCamRotOffsetCur * player.cameraTransform.localRotation;
        InverseWorldCamTotalOffsetCur = player.cameraTransform.rotation * Quaternion.Inverse(cameraParentRotation * targetCameraRotation);
    }

    public static void LateUpdateCamRecoilPosOffset(Vector3 CamAimRecoilPosOffsetTarget, float dt, bool aiming)
    {
        CamAimRecoilPosOffsetTarget = player.cameraTransform.InverseTransformDirection(CamAimRecoilPosOffsetTarget);
        if (CamAimRecoilPosOffsetTarget != Vector3.zero && aiming)
        {
            float lerpStepXY = Mathf.Lerp(CamAimRecoilPosLerpSpeedXYMin, CamAimRecoilPosLerpSpeedXYMax, (Time.time - LastShotTime) * CamAimRecoilPosLerpSpeedStep);
            ActionModuleProceduralRecoil.EFTProceduralRecoilData recoilData = RecoilData;
            ItemActionRanged.ItemActionDataRanged rangedData = recoilData.rangedData;
            if (rangedData.state == ItemActionFiringState.Loop && aiming)
            {
                if (!recoilData.IsStable)
                {
                    CamAimRecoilPosOffsetStable = CamAimRecoilPosOffsetTarget;
                }
                CamAimRecoilPosOffsetCur = new Vector3(Mathf.Lerp(CamAimRecoilPosOffsetCur.x, CamAimRecoilPosOffsetTarget.x, lerpStepXY * dt),
                                                       Mathf.Lerp(CamAimRecoilPosOffsetCur.y, CamAimRecoilPosOffsetTarget.y, lerpStepXY * dt),
                                                       Mathf.Lerp(CamAimRecoilPosOffsetCur.z, CamAimRecoilPosOffsetTarget.z, CamAimRecoilPosSmoothIn * dt));
            }
            else
            {
                if (rangedData.state != ItemActionFiringState.Loop && aiming)
                {
                    CamAimRecoilPosOffsetCur = new Vector3(Mathf.Lerp(CamAimRecoilPosOffsetCur.x, CamAimRecoilPosOffsetTarget.x, lerpStepXY * dt),
                                                           Mathf.Lerp(CamAimRecoilPosOffsetCur.y, CamAimRecoilPosOffsetTarget.y, lerpStepXY * dt),
                                                           Mathf.Lerp(CamAimRecoilPosOffsetCur.z, CamAimRecoilPosOffsetTarget.z, CamAimRecoilPosSmoothIn * dt));
                }
                else
                {
                    CamAimRecoilPosOffsetCur = Vector3.Lerp(CamAimRecoilPosOffsetCur, Vector3.zero, CamAimRecoilPosSmoothOut * dt);
                }
                CamAimRecoilPosOffsetStable = CamAimRecoilPosOffsetCur;
            }
        }
        else
        {
            CamAimRecoilPosOffsetStable = CamAimRecoilPosOffsetCur = Vector3.Lerp(CamAimRecoilPosOffsetCur, Vector3.zero, CamAimRecoilPosSmoothOut * dt);
        }
    }

    public static void LateUpdateApplyCamRot()
    {
        if (!player || !player.bFirstPersonView)
        {
            return;
        }
        player.cameraTransform.localRotation *= LocalCamRotOffsetCur;
        player.cameraTransform.localPosition += CamAimRecoilPosOffsetCur;
    }
}

public class PRCameraUpdate : IRootMovementUpdater
{
    public int Priority => 150;
    public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRiggedWeapon, float _dt)
    {
        ProceduralRecoilUpdater.LateUpdateCameraRot(_dt);
    }
}

public class PRApply : IRootMovementUpdater
{
    public int Priority => 250;

    public void LateUpdateMovement(Transform playerCameraTransform, Transform playerOriginTransform, bool isRiggedWeapon, float _dt)
    {
        ProceduralRecoilUpdater.LateUpdateApplyCamRot();
    }
}

[HarmonyPatch]
public static class ProceduralRecoilPatches
{
    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Awake))]
    [HarmonyPostfix]
    private static void Postfix_Awake_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        ProceduralRecoilUpdater.InitPlayer(__instance);
    }

    //[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.LateUpdate))]
    //[HarmonyPostfix]
    //private static void Postfix_LateUpdate_EntityPlayerLocal(EntityPlayerLocal __instance)
    //{
    //    ProceduralRecoilUpdater.LateUpdateCameraRot(Time.deltaTime);
    //    if (__instance.bFirstPersonView)
    //    {
    //        ProceduralRecoilUpdater.RecoilData?.LateUpdate(Time.deltaTime);
    //    }
    //}

    //[HarmonyPatch(typeof(vp_FPCamera), nameof(vp_FPCamera.LateUpdate))]
    //[HarmonyPostfix]
    //private static void Postfix_LateUpdate_vp_FPCamera()
    //{
    //    ProceduralRecoilUpdater.LateUpdateApplyCamRot();
    //}

    [HarmonyPatch(typeof(vp_FPWeapon), nameof(vp_FPWeapon.FixedUpdate))]
    [HarmonyPostfix]
    private static void Postfix_FixedUpdate_vp_FPWeapon()
    {
        ProceduralRecoilUpdater.FixedUpdate(Time.fixedDeltaTime);
    }
}

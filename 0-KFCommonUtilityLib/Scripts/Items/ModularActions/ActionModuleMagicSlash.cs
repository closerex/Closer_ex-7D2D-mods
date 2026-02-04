using Audio;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionDynamicMelee)), TypeDataTarget(typeof(MagicSlashData)), RequireUserDataBits(nameof(mask), nameof(shiftBits), 1)]
public class ActionModuleMagicSlash
{
    public int mask;
    public byte shiftBits;
    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_Raycast(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var fld_avatar = AccessTools.Field(typeof(EModelBase), nameof(EModelBase.avatarController));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_avatar))
            {
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleMagicSlash>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleMagicSlash>), nameof(IModuleContainerFor<ActionModuleMagicSlash>.Instance))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<MagicSlashData>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<MagicSlashData>), nameof(IModuleContainerFor<MagicSlashData>.Instance))),
                    CodeInstruction.Call(typeof(ActionModuleMagicSlash), nameof(FireSlash))
                });
                break;
            }
        }

        return codes;
    }

    private void FireSlash(ItemActionDynamic action, ItemActionDynamic.ItemActionDynamicData data, MagicSlashData customData)
    {
        if (customData.slashPrefab && data.invData.holdingEntity is EntityPlayerLocal localPlayer)
        {
            ItemValue itemValue = data.invData.itemValue;
            float degration = EffectManager.GetValue(PassiveEffects.DegradationPerUse, itemValue, 1, localPlayer, null, FastTags<TagGroup.Global>.Parse("AreaSweep"));
            if (itemValue.MaxUseTimes - itemValue.UseTimes >= degration)
            {
                itemValue.UseTimes += degration;
                Ray ray = localPlayer.GetMeleeRay();
                Quaternion slashRot = Quaternion.AngleAxis(action.SwingAngle - 90, ray.direction) * localPlayer.cameraTransform.rotation;
                Vector3 slashPos = ray.origin + action.SphereRadius * ray.direction;
                data.invData.gameManager.ItemActionEffectsServer(localPlayer.entityId, data.invData.slotIdx, data.indexInEntityOfAction, 0, slashPos, slashRot.eulerAngles, RequireUserDataBits.InjectUserDataBits(0, 1, shiftBits));
            }
        }
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    public void Postfix_ItemActionEffects(ItemActionDynamicMelee __instance, ItemActionData _actionData, Vector3 _startPos, Vector3 _direction, ref int _userData, MagicSlashData __customData)
    {
        int bit = RequireUserDataBits.ExtractUserDataBits(ref _userData, mask, shiftBits);
        if (bit == 1)
        {
            if (__customData.slashPrefab)
            {
                GameObject slashObj = GameObject.Instantiate(__customData.slashPrefab);
                AreaSweep sweepScript = slashObj.GetOrAddComponent<AreaSweep>();
                sweepScript.Fire(_startPos, Quaternion.Euler(_direction), __customData.extents, __customData.blockExtents, __customData.initialScale, __customData.scaleFactors, _actionData.invData.holdingEntity, _actionData.invData.itemValue, __instance, _actionData, __instance.DamageType, __customData.lifetime, __customData.fixedBlockExtents, __customData.surface, __customData.hitMask, __customData.deathtime);
                if (!string.IsNullOrEmpty(__customData.castingSound))
                {
                    Manager.Play(_actionData.invData.holdingEntity, __customData.castingSound);
                }
            }
        }
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationChanged(ItemAction __instance, ItemActionData _data, MagicSlashData __customData)
    {
        string str = "";
        __instance.Properties.ParseString("MagicSlashPrefab", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashPrefab", str, _data.indexInEntityOfAction);
        bool reloadAsset = str != __customData.loadedPrefabPath;
        if (__customData.slashPrefab && reloadAsset)
        {
            Object.Destroy(__customData.slashPrefab);
            __customData.slashPrefab = null;
        }

        if (reloadAsset)
        {
            __customData.loadedPrefabPath = str;
            if (!string.IsNullOrEmpty(str))
            {
                __customData.slashPrefab = LoadManager.LoadAsset<GameObject>(str, null, null, false, true).Asset;
                __customData.slashPrefab.SetActive(false);
            }
        }

        str = "1,1,1";
        __instance.Properties.ParseString("MagicSlashSize", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashSize", str, _data.indexInEntityOfAction);
        __customData.extents = StringParsers.ParseVector3(str);

        str = "1,1";
        __instance.Properties.ParseString("MagicSlashBlockSizeScale", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashBlockSizeScale", str, _data.indexInEntityOfAction);
        __customData.blockExtents = StringParsers.ParseVector2(str);

        str = "false";
        __instance.Properties.ParseString("MagicSlashFixedBlockExtents", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashFixedBlockExtents", str, _data.indexInEntityOfAction);
        __customData.fixedBlockExtents = StringParsers.ParseBool(str);

        str = "1,1,1";
        __instance.Properties.ParseString("MagicSlashInitialScale", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashInitialScale", str, _data.indexInEntityOfAction);
        __customData.initialScale = StringParsers.ParseVector3(str);

        str = "1,1,1,0";
        __instance.Properties.ParseString("MagicSlashScaleFactors", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashScaleFactors", str, _data.indexInEntityOfAction);
        __customData.scaleFactors = StringParsers.ParseVector4(str);

        str = "1";
        __instance.Properties.ParseString("MagicSlashLifeTime", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashLifeTime", str, _data.indexInEntityOfAction);
        StringParsers.TryParseFloat(str, out __customData.lifetime);

        str = "0";
        __instance.Properties.ParseString("MagicSlashDeathTime", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashDeathTime", str, _data.indexInEntityOfAction);
        StringParsers.TryParseFloat(str, out __customData.deathtime);

        str = "";
        __instance.Properties.ParseString("MagicSlashHitMask", ref str);
        str = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashHitMask", str, _data.indexInEntityOfAction);
        __customData.hitMask = Voxel.ToHitMask(str);

        __customData.surface = _data.invData.item.MadeOfMaterial.SurfaceCategory;
        __instance.Properties.ParseString("MagicSlashMaterial", ref __customData.surface);
        __customData.surface = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashMaterial", __customData.surface, _data.indexInEntityOfAction);

        str = "";
        __instance.Properties.ParseString("MagicSlashCastingSound", ref __customData.castingSound);
        __customData.castingSound = _data.invData.itemValue.GetPropertyOverrideForAction("MagicSlashCastingSound", str, _data.indexInEntityOfAction);
    }

    public class MagicSlashData
    {
        public string loadedPrefabPath;
        public GameObject slashPrefab;
        public Vector3 extents;
        public Vector3 blockExtents;
        public Vector3 initialScale;
        public Vector4 scaleFactors;
        public float lifetime;
        public float deathtime;
        public int hitMask;
        public string surface;
        public bool fixedBlockExtents;
        public string castingSound;
    }
}

using HarmonyLib;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;

[HarmonyPatch]
class AnimationRiggingPatches
{
    [HarmonyPatch(typeof(MinEventActionSetTransformActive), nameof(MinEventActionSetTransformActive.Execute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Execute_MinEventActionSetTransformActive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_find = AccessTools.Method(typeof(GameUtils), nameof(GameUtils.FindDeepChild));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_find))
            {
                codes.RemoveAt(i);
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Self)),
                    CodeInstruction.Call(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetAttachmentReferenceOverrideTransform))
                });
                break;
            }
        }


        return codes;
    }

    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
    [HarmonyPostfix]
    private static void Postfix_OnStateEnter_AnimatorRangedReloadState(AnimatorStateInfo stateInfo)
    {
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out(string.Format("ANIMATION LENGTH: length {0} speed {1} speedMultiplier {2} original length {3}", stateInfo.length, stateInfo.speed, stateInfo.speedMultiplier, stateInfo.length * stateInfo.speedMultiplier));
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnHoldingItemChanged))]
    [HarmonyPostfix]
    private static void Postfix_OnHoldingItemChanged_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        AnimationRiggingManager.OnHoldingItemIndexChanged(__instance);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
    [HarmonyPostfix]
    private static void Postfix_SaveAndCleanupWorld_GameManager()
    {
        AnimationRiggingManager.Clear();
    }

    //altmode workarounds
    private static void ParseTakeOverReloadTime(XElement _node)
    {
        string itemName = _node.GetAttribute("name");
        if (string.IsNullOrEmpty(itemName))
        {
            return;
        }
        ItemClass item = ItemClass.GetItemClass(itemName);
        if (item.Properties.GetBool("TakeOverReloadTime"))
        {
            AnimationRiggingManager.AddReloadTimeTakeOverItem(item.Id);
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), "parseItem")]
    [HarmonyPostfix]
    private static void Postfix_parseItem_ItemClassesFromXml(XElement _node)
    {
        ParseTakeOverReloadTime(_node);
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.StopHolding))]
    [HarmonyPostfix]
    private static void Postfix_StopHolding_ItemClass(Transform _modelTransform)
    {
        if (_modelTransform != null && _modelTransform.TryGetComponent<RigTargets>(out var targets))
        {
            targets.SetEnabled(false, true);
        }
    }

    [HarmonyPatch(typeof(Inventory), "createHeldItem")]
    [HarmonyPostfix]
    private static void Postfix_createHeldItem_Inventory(EntityAlive ___entity, Transform __result)
    {
        if (__result != null && __result.TryGetComponent<RigTargets>(out var targets))
        {
            if (GameManager.IsDedicatedServer || !(___entity is EntityPlayerLocal player))
            {
                targets.Destroy();
            }
            else
            {
                Transform fpsArms = (player.emodel.avatarController as AvatarLocalPlayerController)?.FPSArms?.Parts.BodyObj.transform;
                if (fpsArms != null)
                    targets.Init(fpsArms);
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate))]
    [HarmonyPrefix]
    private static bool Prefix_ForceHoldingItemUpdate(Inventory __instance, EntityAlive ___entity)
    {
        if (___entity is EntityPlayerLocal)
            AnimationRiggingManager.OnClearInventorySlot(__instance, __instance.holdingItemIdx);
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 4)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 4),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), "particlesMuzzleFire")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), "particlesMuzzleFireFpv")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), "particlesMuzzleSmoke")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ItemActionAttack), "particlesMuzzleSmokeFpv")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.SpawnFpvParticles))),
                    new CodeInstruction(OpCodes.Brtrue_S, codes[i - 9].operand)
                });
                break;
            }
        }
        //FieldInfo fld_muzzle = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.muzzle));
        //FieldInfo fld_muzzle2 = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.muzzle2));
        //MethodInfo mtd_getmuzzle = AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetMuzzleOverrideFPV));
        //MethodInfo mtd_getmuzzle2 = AccessTools.Method(typeof(AnimationRiggingManager), nameof(AnimationRiggingManager.GetMuzzle2OverrideFPV));
        //for (int i = 0; i < codes.Count; i++)
        //{
        //    if (codes[i].LoadsField(fld_muzzle))
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldloc_S, 4),
        //            new CodeInstruction(OpCodes.Call, mtd_getmuzzle)
        //        });
        //    }
        //    else if (codes[i].LoadsField(fld_muzzle2))
        //    {
        //        codes.InsertRange(i + 1, new[]
        //        {
        //            new CodeInstruction(OpCodes.Ldloc_S, 4),
        //            new CodeInstruction(OpCodes.Call, mtd_getmuzzle2)
        //        });
        //    }
        //}

        return codes;
    }

    [HarmonyPatch(typeof(Inventory), "clearSlotByIndex")]
    [HarmonyPrefix]
    private static bool Prefix_clearSlotByIndex(Inventory __instance, EntityAlive ___entity, int _idx)
    {
        if(___entity is EntityPlayerLocal)
            AnimationRiggingManager.OnClearInventorySlot(__instance, _idx);
        return true;
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), "Update")]
    [HarmonyPostfix]
    private static void Postfix_Update_AvatarMultiBodyController(AvatarMultiBodyController __instance)
    {
        if (__instance is AvatarLocalPlayerController avatarLocalPlayer)
        {
            AnimationRiggingManager.UpdateLocalPlayerAvatar(avatarLocalPlayer);
        }
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.StartAnimationReloading))]
    [HarmonyPrefix]
    private static bool Prefix_StartAnimationReloding_AvatarController(AvatarMultiBodyController __instance)
    {
        __instance.Entity?.FireEvent(CustomEnums.onReloadAboutToStart);
        return true;
    }

    [HarmonyPatch(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.StartAnimationReloading))]
    [HarmonyPostfix]
    private static void Postfix_StartAnimationReloading_AvatarMultibodyController(AvatarMultiBodyController __instance, int ___reloadSpeedHash)
    {
        if (__instance.HeldItemTransform != null && __instance.HeldItemTransform.TryGetComponent<RigTargets>(out var targets))
        {
            EntityAlive entity = __instance.Entity;
            float reloadSpeed = EffectManager.GetValue(PassiveEffects.ReloadSpeedMultiplier, entity.inventory.holdingItemItemValue, 1f, entity);
            float reloadSpeedRatio = EffectManager.GetValue(CustomEnums.ReloadSpeedRatioFPV2TPV, entity.inventory.holdingItemItemValue, 1f, entity);
            float finalMultiplier;
            bool isFPV = entity as EntityPlayerLocal != null && (entity as EntityPlayerLocal).emodel.IsFPV;
            bool takeOverReloadTime = AnimationRiggingManager.IsReloadTimeTakeOverItem(entity.inventory.holdingItem.Id);
            if (isFPV && takeOverReloadTime)
            {
                finalMultiplier = reloadSpeed / reloadSpeedRatio;
            }
            else if (!isFPV && takeOverReloadTime)
            {
                finalMultiplier = reloadSpeed * reloadSpeedRatio;
            }
            else
            {
                finalMultiplier = reloadSpeed;
            }
            if(ConsoleCmdReloadLog.LogInfo)
                Log.Out($"Set reload multiplier: isFPV {isFPV}, reloadSpeed {reloadSpeed}, reloadSpeedRatio {reloadSpeedRatio}, finalMultiplier {finalMultiplier}");
            __instance.UpdateFloat(___reloadSpeedHash, finalMultiplier, true);
        }
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), nameof(AvatarLocalPlayerController.SetInRightHand))]
    [HarmonyPostfix]
    private static void Postfix_SetInRightHand_AvatarLocalPlayerController(Transform _transform, AvatarLocalPlayerController __instance, bool ___isFPV)
    {
        if (_transform != null && _transform.TryGetComponent<RigTargets>(out var targets))
        {
            targets.SetEnabled(___isFPV);
            _transform.SetParent(__instance.CharacterBody.Parts.RightHandT, false);
            _transform.localPosition = Vector3.zero;
            _transform.localRotation = Quaternion.identity;
        }
    }

    [HarmonyPatch(typeof(Inventory), "setHoldingItemTransfrom")]
    [HarmonyPrefix]
    private static bool Prefix_setHoldingItemTransform_Inventory(Transform ___lastdrawnHoldingItemTransform)
    {
        if (___lastdrawnHoldingItemTransform != null && ___lastdrawnHoldingItemTransform.TryGetComponent<RigTargets>(out var targets))
        {
            targets.SetEnabled(false, true);
        }
        return true;
    }

    [HarmonyPatch(typeof(vp_FPWeapon), "Start")]
    [HarmonyPostfix]
    private static void Postfix_Start_vp_FPWeapon(vp_FPWeapon __instance)
    {
        var player = __instance.GetComponentInParent<EntityPlayerLocal>();
        if(player != null)
        {
            foreach (var model in player.inventory.models)
            {
                if (model != null && model.TryGetComponent<RigTargets>(out var targets))
                {
                    targets.Init(__instance.transform);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Inventory), "setHoldingItemTransfrom")]
    [HarmonyPostfix]
    private static void Postfix_setHoldingItemTransform_Inventory(Transform _t, EntityAlive ___entity)
    {
        if(_t != null && _t.TryGetComponent<RigTargets>(out var targets))
        {
            targets.SetEnabled(___entity.emodel.IsFPV);
        }
    }

    [HarmonyPatch(typeof(World), nameof(World.SpawnEntityInWorld))]
    [HarmonyPrefix]
    private static bool Prefix_SpawnEntityInWorld_World(Entity _entity)
    {
        if (_entity != null && _entity is EntityItem _entityItem)
        {
            var targets = _entityItem.GetComponentInChildren<RigTargets>();
            if (targets != null)
            {
                targets.Destroy();
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "_setTrigger")]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetTrigger(int _pid)
    {
        AnimationRiggingManager.SetTrigger(_pid);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "_resetTrigger")]
    [HarmonyPostfix]
    private static void Postfix_Avatar_ResetTrigger(int _pid)
    {
        AnimationRiggingManager.ResetTrigger(_pid);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "_setFloat")]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetFloat(int _pid, float _value)
    {
        AnimationRiggingManager.SetFloat(_pid, _value);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "_setBool")]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetBool(int _pid, bool _value)
    {
        AnimationRiggingManager.SetBool(_pid, _value);
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "_setInt")]
    [HarmonyPostfix]
    private static void Postfix_Avatar_SetInt(int _pid, int _value)
    {
        AnimationRiggingManager.SetInt(_pid, _value);
    }
}

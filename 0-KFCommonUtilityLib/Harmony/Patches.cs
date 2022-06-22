using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Xml;
using SystemInformation;
using UnityEngine;

[HarmonyPatch]
class CommonUtilityPatch
{
    //SCore NPC compatibility
    public static void FakeAttackOther(Entity entity, EntityAlive attacker, ItemValue damageItemValue, WorldRayHitInfo hitInfo, bool useInventory)
    {
        if(attacker is EntityAlive && entity is EntityAlive entityAlive)
        {
            MinEventParams context = attacker.MinEventContext;
            context.Other = entityAlive;
            context.ItemValue = damageItemValue;
            context.StartPosition = hitInfo.ray.origin;
            attacker.FireEvent(MinEventTypes.onSelfAttackedOther, useInventory);
        }
    }

    static bool need_postfix = true;

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Hit_ItemActionAttack(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        codes.InsertRange(0, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Ldc_I4_0),
            CodeInstruction.StoreField(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.need_postfix))
        });

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
    [HarmonyPostfix]
    private static void Postfix_Hit_ItemActionAttack(WorldRayHitInfo hitInfo, int _attackerEntityId, ItemValue damagingItemValue)
    {
        if (!need_postfix)
        {
            need_postfix = true;
            return;
        }

        if (hitInfo != null && hitInfo.tag != null && hitInfo.tag.StartsWith("E_"))
        {
            World _world = GameManager.Instance.World;
            EntityPlayer attacker = _world.GetEntity(_attackerEntityId) as EntityPlayer;
            if(attacker != null)
            {
                Entity entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string str);
                if (entity != null && entity.entityId != _attackerEntityId)
                {
                    bool useInventory = false;
                    if (damagingItemValue == null)
                    {
                        damagingItemValue = attacker.inventory.holdingItemItemValue;
                    }
                    useInventory = damagingItemValue.Equals(attacker.inventory.holdingItemItemValue);
                    FakeAttackOther(entity, attacker, damagingItemValue, hitInfo, useInventory);
                }
            }
        }
    }

    //fix reloading issue and onSelfRangedBurstShot timing
    public static void FakeReload(EntityAlive holdingEntity, ItemActionRanged.ItemActionDataRanged _actionData)
    {
        if (!holdingEntity)
            return;
        _actionData.isReloading = true;
        holdingEntity.MinEventContext.ItemActionData = _actionData;
        holdingEntity.FireEvent(MinEventTypes.onReloadStart, true);
        _actionData.isReloading = false;
        _actionData.isReloadCancelled = false;
        holdingEntity.FireEvent(MinEventTypes.onReloadStop);
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapAmmoType))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_SwapAmmoType_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for(int i = 0; i < codes.Count; ++i)
        {
            if(codes[i].opcode == OpCodes.Ret)
            {
                codes.InsertRange(i, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.FakeReload))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_fire_event = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.FireEvent));
        var mtd_get_model_layer = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.GetModelLayer));
        var mtd_get_perc_left = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.PercentUsesLeft));

        int take = -1, insert = -1, label = -1;
        for (int i = 0; i < codes.Count; ++i)
        {
            if(codes[i].opcode == OpCodes.Callvirt)
            {
                if (codes[i].Calls(mtd_fire_event))
                {
                    take = i - 25;
                    label = i + 1;
                }
                else if (codes[i].Calls(mtd_get_model_layer))
                    insert = i + 2;
                
            }
        }

        if(take < insert)
        {
            codes[take].MoveLabelsTo(codes[label]);
            var list = codes.GetRange(take, 26);
            codes.InsertRange(insert, list);
            codes.RemoveRange(take, 26);
        }

        return codes;
    }
    //fix recoil animation does not match weapon RPM
    private static int weaponFireHash = Animator.StringToHash("WeaponFire");
    private static int aimHash = Animator.StringToHash("IsAiming");
    private static HashSet<int> hash_shot_state = new HashSet<int>();
    private static HashSet<int> hash_aimshot_state = new HashSet<int>();

    public static void InitShotStates()
    {
        string[] weapons =
        {
            "fpvAK47",
            "fpvMagnum",
            "fpvRocketLauncher",
            "fpvSawedOffShotgun",
            "fpvBlunderbuss",
            "fpvCrossbow",
            "fpvPistol",
            "fpvHuntingRifle",
            "fpvSMG",
            "fpvSniperRifle",
            "M60",
            "fpvDoubleBarrelShotgun",
            "fpvJunkTurret",
            "fpvTacticalAssaultRifle",
            "fpvDesertEagle",
            "fpvAutoShotgun",
            "fpvSharpShooterRifle",
            "fpvPipeMachineGun",
            "fpvPipeRifle",
            "fpvPipeRevolver",
            "fpvPipeShotgun",
            "fpvLeverActionRifle",
        };
        foreach(string weapon in weapons)
        {
            hash_shot_state.Add(Animator.StringToHash(weapon + "Fire"));
            hash_aimshot_state.Add(Animator.StringToHash(weapon + "AimFire"));
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnFired))]
    [HarmonyPostfix]
    private static void Postfix_OnFired_EntityPlayerLocal(EntityPlayerLocal __instance)
    {
        if (!__instance.bFirstPersonView)
            return;

        ItemActionRanged.ItemActionDataRanged _rangedData;
        if ((_rangedData = __instance.inventory.holdingItemData.actionData[0] as ItemActionRanged.ItemActionDataRanged) == null && (_rangedData = __instance.inventory.holdingItemData.actionData[1] as ItemActionRanged.ItemActionDataRanged) == null)
            return;

        var anim = (__instance.emodel.avatarController as AvatarLocalPlayerController).FPSArms.Animator;
        if (anim.IsInTransition(0))
            return;

        var curState = anim.GetCurrentAnimatorStateInfo(0);
        if (curState.length > _rangedData.Delay)
        {
            bool aimState = anim.GetBool(aimHash);
            short shotState = 0;
            if (hash_shot_state.Contains(curState.shortNameHash))
                shotState = 1;
            else if(hash_aimshot_state.Contains(curState.shortNameHash))
                shotState = 2;
            if (shotState == 0 || (shotState == 1 && aimState) || (shotState == 2 && !aimState))
            {
                if(shotState > 0)
                    anim.ResetTrigger(weaponFireHash);
                return;
            }

            //current state, layer 0, offset 0
            anim.PlayInFixedTime(0, 0, 0);
            if (_rangedData.invData.itemValue.Meta == 0)
                __instance.emodel.avatarController.ResetTrigger(weaponFireHash);
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyPostfix]
    private static void Postfix_ItemActionEffects_ItemActionRanged(ItemActionData _actionData, int _firingState)
    {
        if(_firingState == 0)
            _actionData.invData.holdingEntity.emodel.avatarController.ResetTrigger(weaponFireHash);
    }

    [HarmonyPatch(typeof(GameManager), "gmUpdate")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_gmUpdate_GameManager(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_unload = AccessTools.Method(typeof(Resources), nameof(Resources.UnloadUnusedAssets));
        var fld_duration = AccessTools.Field(typeof(GameManager), "unloadAssetsDuration");

        for(int i = 0; i < codes.Count; ++i)
        {
            if(codes[i].opcode == OpCodes.Call && codes[i].Calls(mtd_unload))
            {
                for(int j = i; j >= 0; --j)
                {
                    if (codes[j].opcode == OpCodes.Ldfld && codes[j].LoadsField(fld_duration) && codes[j + 1].opcode == OpCodes.Ldc_R4)
                        codes[j + 1].operand = (float)codes[j + 1].operand / 2;
                }
                break;
            }
        }

        return codes;
    }

    internal static void ForceUpdateGC()
    {
        if (GameManager.IsDedicatedServer)
            return;
        if(GameManager.frameCount % 18000 == 0)
        {
            long rss = GetRSS.GetCurrentRSS();
            if(rss / 1024 / 1024 > 6144)
            {
                Log.Out("Memory usage exceeds threshold, now performing garbage collection...");
                GC.Collect();
            }
        }
    }

    //altmode workarounds
    private static void ParseAltRequirements(XmlElement _node, ItemClass item)
    {
        for(int i = 0; i < item.Actions.Length; i++)
        {
            if (item.Actions[i] is ItemActionAltMode _alt)
                _alt.ParseAltRequirements(_node, i);
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), "parseItem")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_parseItem_ItemClassesFromXml(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        codes.InsertRange(codes.Count - 1, new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldloc, 5),
            CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.ParseAltRequirements))
        });

        return codes;
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyPrefix]
    private static bool Prefix_ExecuteAction_ItemClass(ItemClass __instance, int _actionIdx, ItemInventoryData _data, bool _bReleased)
    {
        if (!_bReleased && __instance.Actions[_actionIdx] is ItemActionAltMode _alt)
            _alt.SetAltRequirement(_data.actionData[_actionIdx]);
            
        return true;
    }

    [HarmonyPatch(typeof(DynamicProperties), nameof(DynamicProperties.Parse))]
    [HarmonyPrefix]
    private static bool Prefix_Parse_DynamicProperties(XmlNode _propertyNode)
    {
        if (_propertyNode.Name != "property")
            return false;
        return true;
    }
}


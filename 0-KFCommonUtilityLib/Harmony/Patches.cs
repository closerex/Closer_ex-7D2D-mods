using Autodesk.Fbx;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using System.Xml.Linq;
using SystemInformation;
using UnityEngine;
using UnityEngine.SceneManagement;

[HarmonyPatch]
public class CommonUtilityPatch
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
    [HarmonyPrefix]
    private static bool Prefix_Hit_ItemActionAttack(ItemActionAttack.AttackHitInfo _attackDetails)
    {
        if(_attackDetails != null)
        {
            _attackDetails.hitPosition = Vector3i.zero;
            _attackDetails.bKilled = false;
        }

        return true;
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Hit_ItemActionAttack(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo mtd_can_damage_entity = AccessTools.Method(typeof(Entity), nameof(Entity.CanDamageEntity));

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            if(code.Calls(mtd_can_damage_entity))
            {
                codes.InsertRange(i + 2, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    CodeInstruction.StoreField(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.need_postfix))
                });
                break;
            }
        }

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

    [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
    [HarmonyPostfix]
    private static void Postfix_OnStateEnter_AnimatorRangedReloadState(ItemActionRanged.ItemActionDataRanged ___actionData, ItemActionRanged ___actionRanged)
    {
        //___actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(AvatarController.isAimingHash, false, false);
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

        int take = -1, insert = -1;
        for (int i = 0; i < codes.Count; ++i)
        {
            if(codes[i].opcode == OpCodes.Ldc_I4_S && codes[i].OperandIs((int)MinEventTypes.onSelfRangedBurstShotStart) && codes[i + 2].Calls(mtd_fire_event))
                take = i - 3;
            else if (codes[i].Calls(mtd_get_model_layer))
                insert = i + 2;
        }

        if(take < insert)
        {
            var list = codes.GetRange(take, 6);
            codes.InsertRange(insert, list);
            codes.RemoveRange(take, 6);
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
            {
                __instance.emodel.avatarController.CancelEvent(weaponFireHash);
                Log.Out("Cancel fire event because meta is 0");
            }
        }
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
    [HarmonyPostfix]
    private static void Postfix_ItemActionEffects_ItemActionRanged(ItemActionData _actionData, int _firingState)
    {
        if(_firingState == 0 && _actionData.invData.holdingEntity is EntityPlayerLocal && !(_actionData.invData.itemValue.ItemClass.Actions[0] is ItemActionCatapult))
            _actionData.invData.holdingEntity?.emodel.avatarController.CancelEvent(weaponFireHash);
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
    private static void ParseAltRequirements(XElement _node)
    {
        string itemName = _node.GetAttribute("name");
        if (string.IsNullOrEmpty(itemName))
        {
            return;
        }
        ItemClass item = ItemClass.GetItemClass(itemName);
        for(int i = 0; i < item.Actions.Length; i++)
        {
            if (item.Actions[i] is ItemActionAltMode _alt)
                _alt.ParseAltRequirements(_node, i);
        }
    }

    [HarmonyPatch(typeof(ItemClassesFromXml), "parseItem")]
    [HarmonyPostfix]
    private static void Postfix_parseItem_ItemClassesFromXml(XElement _node)
    {
        ParseAltRequirements(_node);
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
    private static bool Prefix_Parse_DynamicProperties(XElement elementProperty)
    {
        if (elementProperty.Name.LocalName != "property")
            return false;
        return true;
    }

    //MinEventParams workarounds
    [HarmonyPatch(typeof(ItemActionRanged), "fireShot")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_fireShot_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var fld_ranged_tag = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.RangedTag));
        var fld_params = AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.MinEventContext));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_ranged_tag))
            {
                if (!codes[i + 3].LoadsField(fld_params))
                {
                    codes.InsertRange(i + 2, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Ldloc, 10),
                        CodeInstruction.LoadField(typeof(WorldRayHitInfo), nameof(WorldRayHitInfo.hit)),
                        CodeInstruction.LoadField(typeof(HitInfoDetails), nameof(HitInfoDetails.pos)),
                        CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.Position)),
                        new CodeInstruction(OpCodes.Ldloc_1),
                        CodeInstruction.Call(typeof(EntityAlive), nameof(EntityAlive.GetPosition)),
                        CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.StartPosition))
                    });
                }
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.OnHoldingUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var mtd_release = AccessTools.Method(typeof(ItemActionRanged), "triggerReleased");
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_release))
            {
                codes[i + 1].labels.Clear();
                codes[i + 1].MoveLabelsFrom(codes[i - 20]);
                codes.RemoveRange(i - 20, 21);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), "triggerReleased")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_triggerReleased_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
    {
        var mtd_effect = AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemActionEffectsServer));
        var mtd_data = AccessTools.Method(typeof(ItemActionRanged), "getUserData");
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_effect))
            {
                codes.InsertRange(i, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Callvirt, mtd_data)
                });
                codes.RemoveAt(i - 1);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
    [HarmonyPrefix]
    private static bool Prefix_StartGame_GameManager()
    {
        CustomEffectEnumManager.InitFinal();
        return true;
    }

    [HarmonyPatch(typeof(PassiveEffect), nameof(PassiveEffect.ParsePassiveEffect))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParsePassiveEffect_PassiveEffect(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        MethodInfo mtd_enum_parse = AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(PassiveEffects) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_enum_parse))
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string) }, new[] { typeof(PassiveEffects) }));
                codes.RemoveRange(i - 1, 2);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(MinEventActionBase), nameof(MinEventActionBase.ParseXmlAttribute))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ParseXmlAttribute_MinEventActionBase(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        MethodInfo mtd_enum_parse = AccessTools.Method(typeof(EnumUtils), nameof(EnumUtils.Parse), new[] { typeof(string), typeof(bool) }, new[] { typeof(MinEventTypes) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_enum_parse))
            {
                codes.Insert(i + 1, CodeInstruction.Call(typeof(CustomEffectEnumManager), nameof(CustomEffectEnumManager.RegisterOrGetEnum), new[] { typeof(string) }, new[] { typeof(MinEventTypes) }));
                codes.RemoveRange(i - 1, 2);
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ProjectileMoveScript), "checkCollision")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_checkCollision_ProjectileMoveScript(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var fld_strain = AccessTools.Field(typeof(ItemActionLauncher.ItemActionDataLauncher), nameof(ItemActionLauncher.ItemActionDataLauncher.strainPercent));
        var mtd_block = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(fld_strain))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.itemValueProjectile)),
                    new CodeInstruction(OpCodes.Ldloc_S, 4),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), codes[i - 3].Calls(mtd_block) ? nameof(CommonUtilityPatch.GetProjectileBlockDamagePerc) : nameof(CommonUtilityPatch.GetProjectileEntityDamagePerc)),
                    new CodeInstruction(OpCodes.Mul)
                });
            }
        }

        return codes;
    }

    public static float GetProjectileBlockDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
    {
        return EffectManager.GetValue(CustomEnums.ProjectileImpactDamagePercentBlock, _itemValue, 1, _holdingEntity, null);
    }

    public static float GetProjectileEntityDamagePerc(ItemValue _itemValue, EntityAlive _holdingEntity)
    {
        return EffectManager.GetValue(CustomEnums.ProjectileImpactDamagePercentEntity, _itemValue, 1, _holdingEntity, null);
    }

    //private static bool exported = false;
    //[HarmonyPatch(typeof(EModelUMA), nameof(EModelUMA.onCharacterUpdated))]
    //[HarmonyPostfix]
    //private static void Postfix_test(Entity ___entity)
    //{
    //    if (!exported)
    //    {
    //        exported = true;
    //        var objects = new[] { ___entity.RootTransform.gameObject.GetComponentsInChildren<Animator>()[1] };
    //        Log.Out($"exporting objs: {objects.Length} avatar {objects[0].avatar.name} is human {objects[0].avatar.isHuman}");
    //        FbxExporter07.OnExport(objects, @"E:\Unity Projects\AnimationPlayground\Assets\ExportedProject\example_skinned_mesh_with_bones.fbx");
    //        Application.Quit();
    //    }
    //}
}


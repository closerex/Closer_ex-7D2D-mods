using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

[HarmonyPatch]
class CommonUtilityPatch
{
    /*
    private static readonly FastTags tag_medicgun = FastTags.Parse("medicgun");
    private static readonly FastTags tag_medicpistol = FastTags.Parse("medicpistol");
    private static readonly FastTags tag_medicshotgun = FastTags.Parse("medicshotgun");
    private static readonly FastTags tag_medicar401 = FastTags.Parse("medicar401");

    private const string str_is_heal_mode = "isHealMode";
    private const string str_cvar_stock_consumption = "$medicBuffConsumption";
    private const string str_cvar_consumption_temp_0 = "$medicBuffConsumptionTemp0";

    private const string str_cvar_stock_pistol = "$medicBuffStockPistol";
    private const string str_cvar_stock_shotgun = "$medicBuffStockSG301";
    private const string str_cvar_stock_ar401 = "$medicBuffStockAR401";

    private struct OutState
    {
        public bool exec;
        public string str_cvar;
        public float value;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyPrefix]
    private static bool Prefix_ExecuteAction_ItemActionRanged(ItemActionRanged __instance, ItemActionData _actionData, bool _bReleased, out OutState __state)
    {
        __state.exec = false;
        __state.str_cvar = null;
        __state.value = -1;
        if (_bReleased)
            return true;

        EntityPlayerLocal player = _actionData.invData.holdingEntity as EntityPlayerLocal;
        if (player == null)
            return true;

        ItemClass item = __instance.item;
        if (item.HasAnyTags(tag_medicgun))
        {
            ItemActionRanged.ItemActionDataRanged itemActionDataRanged = _actionData as ItemActionRanged.ItemActionDataRanged;
            if (player.GetCVar(str_is_heal_mode) == 1)
            {
                if (!((int)itemActionDataRanged.curBurstCount < __instance.GetBurstCount(_actionData) | __instance.GetBurstCount(_actionData) == -1))
                {
                    return false;
                }
                __instance.InfiniteAmmo = true;
                __state.exec = true;
                float isConsumption0 = player.GetCVar(str_cvar_consumption_temp_0);
                float consumption = isConsumption0 == 1 ? 0 : player.GetCVar(str_cvar_stock_consumption);
                if (consumption <= 0)
                    player.SetCVar(str_cvar_consumption_temp_0, 0);
                float stock = 0;
                if (item.HasAnyTags(tag_medicpistol) && (stock = player.GetCVar(str_cvar_stock_pistol)) >= consumption)
                {
                    __state.str_cvar = str_cvar_stock_pistol;
                    __state.value = stock - consumption;
                }else if(item.HasAnyTags(tag_medicar401) && (stock = player.GetCVar(str_cvar_stock_ar401)) >= consumption)
                {
                    __state.str_cvar = str_cvar_stock_ar401;
                    __state.value = stock - consumption;
                }else if(item.HasAnyTags(tag_medicshotgun) && (stock = player.GetCVar(str_cvar_stock_shotgun)) >= consumption)
                {
                    __state.str_cvar = str_cvar_stock_shotgun;
                    __state.value = stock - consumption;
                }else
                {
                    __state.exec = false;
                    player.PlayOneShot("medic_module_toggle_off", false);
                    return false;
                }
            }else
                __instance.InfiniteAmmo = false;
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyPostfix]
    private static void Postfix_ExecuteAction_ItemActionRanged(ItemActionData _actionData, OutState __state)
    {
        if (!__state.exec || __state.str_cvar == null || __state.value < 0)
            return;

        //if ((_actionData as ItemActionRanged.ItemActionDataRanged).invData.itemValue.PercentUsesLeft <= 0f)
        //    return;

        EntityPlayerLocal player = _actionData.invData.holdingEntity as EntityPlayerLocal;
        if (player == null || player.inventory.holdingItemItemValue.PercentUsesLeft <= 0f)
            return;

        player.SetCVar(__state.str_cvar, __state.value);
    }

    private static readonly int hash_animator_reload_multiplier = Animator.StringToHash("reload_multiplier");
    private static readonly int hash_animator_shoot_multiplier = Animator.StringToHash("shoot_multiplier");
    private static readonly int hash_animator_shoot = Animator.StringToHash("shoot");
    private static readonly int hash_animator_reload = Animator.StringToHash("reload");
    private static readonly int hash_animator_dart = Animator.StringToHash("dart");
    private static readonly int hash_animator_empty = Animator.StringToHash("empty");

    private const string str_sound_pistol_fire = "medic_pistol_fire";
    private const string str_sound_shotgun_fire = "medic_shotgun_fire";
    private const string str_sound_ar401_fire = "medic_ar401_fire";
    private const string str_sound_dart = "medic_pistol_dart";

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.FireEvent))]
    [HarmonyPrefix]
    private static bool Prefix_FireEvent_EntityPlayerLocal(EntityPlayerLocal __instance, MinEventTypes _eventType)
    {
        if (__instance == null)
            return true;

        Inventory inv = __instance.inventory;
        if (!inv.holdingItem.HasAnyTags(tag_medicgun))
            return true;
        Animator item_animator = (__instance.emodel.avatarController as AvatarLocalPlayerController).HeldItemAnimator;
        if (item_animator == null)
            return true;
        if (_eventType == MinEventTypes.onSelfRangedBurstShot)
        {
            float rpm_change = EffectManager.GetValue(PassiveEffects.RoundsPerMinute, inv.holdingItemItemValue, 1, __instance, null, inv.holdingItem.ItemTags);
            int rounds_left = __instance.MinEventContext.ItemActionData.invData.itemValue.Meta;

            item_animator.SetFloat(hash_animator_shoot_multiplier, rpm_change);
            if (__instance.GetCVar(str_is_heal_mode) == 1)
            {
                item_animator.SetTrigger(hash_animator_dart);
                __instance.PlayOneShot(str_sound_dart, false);
            }
            else
            {
                if (rounds_left <= 0)
                    return true;
                ItemClass item = inv.holdingItem;
                if (item.HasAnyTags(tag_medicpistol))
                    __instance.PlayOneShot(str_sound_pistol_fire, false);
                else if (item.HasAnyTags(tag_medicshotgun))
                    __instance.PlayOneShot(str_sound_shotgun_fire, false);
                else if (item.HasAnyTags(tag_medicar401))
                    __instance.PlayOneShot(str_sound_ar401_fire, false);
                item_animator.SetTrigger(hash_animator_shoot);
                if (rounds_left <= 1)
                    item_animator.SetBool(hash_animator_empty, true);
            }
        }else if (_eventType == MinEventTypes.onReloadStart)
        {
            float rsm_change = EffectManager.GetValue(PassiveEffects.ReloadSpeedMultiplier, inv.holdingItemItemValue, 1, __instance, null, inv.holdingItem.ItemTags);
            item_animator.SetFloat(hash_animator_reload_multiplier, rsm_change);
            item_animator.SetTrigger(hash_animator_reload);
            item_animator.SetBool(hash_animator_empty, false);
        }
        return true;
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.FireEvent))]
    [HarmonyPostfix]
    private static void Postfix_FireEvent_EntityPlayerLocal(EntityPlayerLocal __instance, MinEventTypes _eventType)
    {
        if (_eventType == MinEventTypes.onSelfEquipStart)
        {
            int rounds_left = __instance.MinEventContext.ItemActionData.invData.itemValue.Meta;
            Animator item_animator = (__instance.emodel.avatarController as AvatarLocalPlayerController).HeldItemAnimator;
            if (item_animator == null)
                return;
            if (rounds_left <= 0)
                item_animator.SetBool(hash_animator_empty, true);
        }
    }
    */

    //static MethodInfo mtdinfo_cde = AccessTools.Method(typeof(Entity), nameof(Entity.CanDamageEntity), new Type[] { typeof(int) });
    static bool need_postfix = true;

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

    //SCore compatibility
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
}


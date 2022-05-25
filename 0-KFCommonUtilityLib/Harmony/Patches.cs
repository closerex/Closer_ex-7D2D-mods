using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

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
}


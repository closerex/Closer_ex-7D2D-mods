using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;

namespace SCoreEntityHitCompatibilityPatch
{
    public class SCoreEntityHitCompatibilityInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class Patches
    {
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInit))]
        [HarmonyPostfix]
        private static void Postfix_LateInit_ItemClass(ItemClass __instance)
        {
            FakeAttackManager.ParseFakeAttackItem(__instance);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPrefix]
        private static bool Prefix_StartGame_GameManager()
        {
            CustomEffectEnumManager.InitFinal();
            FakeAttackManager.PreloadCleanup();
            return true;
        }

        private static bool CanDamageEntity(int sourceID, EntityAlive target)
        {
            if (GameManager.Instance.World.GetEntity(sourceID) == null || target == null)
                return true;

            return !EntityUtilities.IsAnAlly(target.entityId, sourceID);
        }

        //[HarmonyPatch(typeof(Explosion), nameof(Explosion.AttackEntites))]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_Explosion_AttackEntites(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = new List<CodeInstruction>(instructions);

        //    MethodInfo mtd_awake = AccessTools.Method(typeof(EntityAlive), nameof(EntityAlive.ConditionalTriggerSleeperWakeUp));

        //    //explosion does not hit allies, score workaround
        //    for (int i = 0; i < codes.Count; i++)
        //    {
        //        if (codes[i].Calls(mtd_awake))
        //        {
        //            var label = codes[i - 2].operand;
        //            codes.InsertRange(i - 1, new[]
        //            {
        //                new CodeInstruction(OpCodes.Ldarg_1),
        //                new CodeInstruction(codes[i - 1].opcode, codes[i - 1].operand),
        //                CodeInstruction.Call(typeof(Patches), nameof(Patches.CanDamageEntity)),
        //                new CodeInstruction(OpCodes.Brfalse, label)
        //            });
        //            break;
        //        }
        //    }

        //    return codes;
        //}

        //[HarmonyPatch(typeof(MultiActionProjectileRewrites), nameof(MultiActionProjectileRewrites.ProjectileHit))]
        //[HarmonyPrefix]
        //private static bool Prefix_MultiActionProjectileRewrites_ProjectileHit(WorldRayHitInfo hitInfo, int _attackerEntityId, ItemActionAttack.AttackHitInfo _attackDetails, ItemValue projectileValue)
        //{
        //    if (hitInfo?.tag == null)
        //        return false;

        //    var entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out var text3);
        //    if (entity == null)
        //        return true;

        //    if (EntityUtilities.IsAnAlly(entity.entityId, _attackerEntityId))
        //    {
        //        // This prevents the "infinite harvest bug"
        //        _attackDetails.bBlockHit = false;
        //        _attackDetails.bHarvestTool = false;
        //        _attackDetails.itemsToDrop = null;
        //        World _world = GameManager.Instance.World;
        //        EntityPlayer attacker = _world.GetEntity(_attackerEntityId) as EntityPlayer;
        //        if (entity != null && entity.entityId != _attackerEntityId)
        //        {
        //            FakeAttackOther(entity, attacker, projectileValue, hitInfo, projectileValue.SelectedAmmoTypeIndex, false);
        //        }
        //        return false;
        //    }
        //    return true;
        //}


        //SCore NPC compatibility
        public static void FakeAttackOther(Entity entity, EntityAlive attacker, ItemValue damageItemValue, WorldRayHitInfo hitInfo, int actionIndex, bool useInventory)
        {
            if (entity is EntityAlive entityAlive && FakeAttackManager.ShouldFakeAttack(damageItemValue.type, actionIndex))
            {
                //Log.Out($"Fake attack {entity.GetDebugName()} with {damageItemValue.ItemClass.Name} action index {actionIndex}");
                MinEventParams context = attacker.MinEventContext;
                context.Other = entityAlive;
                context.ItemValue = damageItemValue;
                context.StartPosition = hitInfo.ray.origin;
                attacker.FireEvent(MinEventTypes.onSelfAttackedOther, useInventory);
            }
        }

        //static bool need_postfix = true;

        //[HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
        //[HarmonyPrefix]
        //private static bool Prefix_Hit_ItemActionAttack(ItemActionAttack.AttackHitInfo _attackDetails)
        //{
        //    if (_attackDetails != null)
        //    {
        //        _attackDetails.hitPosition = Vector3i.zero;
        //        _attackDetails.bKilled = false;
        //    }

        //    return true;
        //}

        //[HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_Hit_ItemActionAttack(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = new List<CodeInstruction>(instructions);
        //    MethodInfo mtd_can_damage_entity = AccessTools.Method(typeof(Entity), nameof(Entity.CanDamageEntity));

        //    for (int i = 0; i < codes.Count; i++)
        //    {
        //        var code = codes[i];
        //        if (code.Calls(mtd_can_damage_entity))
        //        {
        //            codes.InsertRange(i + 2, new CodeInstruction[]
        //            {
        //            new CodeInstruction(OpCodes.Ldc_I4_1),
        //            CodeInstruction.StoreField(typeof(Patches), nameof(Patches.need_postfix))
        //            });
        //            break;
        //        }
        //    }

        //    codes.InsertRange(0, new CodeInstruction[]
        //    {
        //    new CodeInstruction(OpCodes.Ldc_I4_0),
        //    CodeInstruction.StoreField(typeof(Patches), nameof(Patches.need_postfix))
        //    });

        //    return codes;
        //}

        //[HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
        //[HarmonyPostfix]
        //private static void Postfix_Hit_ItemActionAttack(WorldRayHitInfo hitInfo, int _attackerEntityId, ItemValue damagingItemValue)
        //{
        //    if (!need_postfix)
        //    {
        //        need_postfix = true;
        //        return;
        //    }

        //    if (hitInfo != null && hitInfo.tag != null && hitInfo.tag.StartsWith("E_"))
        //    {
        //        World _world = GameManager.Instance.World;
        //        EntityPlayer attacker = _world.GetEntity(_attackerEntityId) as EntityPlayer;
        //        if (attacker != null)
        //        {
        //            Entity entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string str);
        //            if (entity != null && entity.entityId != _attackerEntityId)
        //            {
        //                bool useInventory = false;
        //                if (damagingItemValue == null)
        //                {
        //                    damagingItemValue = attacker.inventory.holdingItemItemValue;
        //                }
        //                useInventory = damagingItemValue.Equals(attacker.inventory.holdingItemItemValue);
        //                FakeAttackOther(entity, attacker, damagingItemValue, hitInfo, MultiActionManager.GetActionIndexForEntity(attacker), useInventory);
        //            }
        //        }
        //    }
        //}
    }
}

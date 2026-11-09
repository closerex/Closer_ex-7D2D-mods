using HarmonyLib;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public class BackgroundInventoryUpdatePatch
    {
        [HarmonyPatch(typeof(ItemInventoryData), nameof(ItemInventoryData.Teardown))]
        [HarmonyPrefix]
        private static void Prefix_Teardown_ItemInventoryData(ItemInventoryData __instance)
        {
            int slot = __instance.slotIdx;
            var hand = __instance.holdingEntity.Hand;
            if (slot >= 0 && ReferenceEquals(hand.slotDatas[slot], __instance))
                BackgroundInventoryUpdateManager.UnregisterUpdater(__instance.holdingEntity, slot);
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
        [HarmonyPostfix]
        private static void Postfix_OnEntityDeath_EntityAlive(EntityAlive __instance)
        {
            BackgroundInventoryUpdateManager.UnregisterUpdater(__instance);
        }

        [HarmonyPatch(typeof(Hand), nameof(Hand.OnUpdate))]
        [HarmonyPostfix]
        private static void Postfix_OnUpdate_Hand(Hand __instance)
        {
            BackgroundInventoryUpdateManager.Update(__instance.entity);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
        [HarmonyPostfix]
        private static void Postfix_SaveAndCleanupWorld_GameManager()
        {
            BackgroundInventoryUpdateManager.Cleanup();
        }

    }
}

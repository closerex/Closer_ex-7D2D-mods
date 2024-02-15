using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public class BackgroundInventoryUpdatePatch
    {
        [HarmonyPatch(typeof(Inventory), "clearSlotByIndex")]
        [HarmonyPostfix]
        private static void Postfix_clearSlotByIndex_Inventory(EntityAlive ___entity, int _idx)
        {
            BackgroundInventoryUpdateManager.UnregisterUpdater(___entity, _idx);
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
        [HarmonyPostfix]
        private static void Postfix_OnEntityDeath_EntityAlive(EntityAlive __instance)
        {
            BackgroundInventoryUpdateManager.UnregisterUpdater(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.OnUpdate))]
        [HarmonyPostfix]
        private static void Postfix_OnUpdate_Inventory(EntityAlive ___entity)
        {
            BackgroundInventoryUpdateManager.Update(___entity);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
        [HarmonyPostfix]
        private static void Postfix_SaveAndCleanupWorld_GameManager()
        {
            BackgroundInventoryUpdateManager.Cleanup();
        }
    }
}

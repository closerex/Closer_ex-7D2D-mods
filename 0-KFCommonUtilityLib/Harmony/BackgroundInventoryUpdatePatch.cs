using HarmonyLib;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public class BackgroundInventoryUpdatePatch
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.clearSlotByIndex))]
        [HarmonyPostfix]
        private static void Postfix_clearSlotByIndex_Inventory(Inventory __instance, int _idx)
        {
            BackgroundInventoryUpdateManager.UnregisterUpdater(__instance.entity, _idx);
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
        [HarmonyPostfix]
        private static void Postfix_OnEntityDeath_EntityAlive(EntityAlive __instance)
        {
            BackgroundInventoryUpdateManager.UnregisterUpdater(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.OnUpdate))]
        [HarmonyPostfix]
        private static void Postfix_OnUpdate_Inventory(Inventory __instance)
        {
            BackgroundInventoryUpdateManager.Update(__instance.entity);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveAndCleanupWorld))]
        [HarmonyPostfix]
        private static void Postfix_SaveAndCleanupWorld_GameManager()
        {
            BackgroundInventoryUpdateManager.Cleanup();
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.AttachToEntity))]
        [HarmonyPostfix]
        private static void Postfix_AttachToEntity_EntityAlive(EntityAlive __instance)
        {
            //BackgroundInventoryUpdateManager.DisableUpdater(__instance);
            BackgroundInventoryUpdateManager.UnregisterUpdater(__instance);
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.Detach))]
        [HarmonyPostfix]
        private static void Postfix_Detach_EntityAlive(EntityAlive __instance)
        {
            if (__instance.isEntityRemote)
            {
                return;
            }
            if (__instance.inventory != null && __instance.inventory.slots != null)
            {
                for (int i = 0; i < __instance.inventory.PUBLIC_SLOTS; i++)
                {
                    ItemInventoryData invData = __instance.inventory.slots[i];
                    if (invData != null && invData.actionData != null)
                    {
                        for (int j = 0; j < invData.actionData.Count; j++)
                        {
                            var actionData = invData.actionData[j];
                            if (actionData is IModuleContainerFor<IBackgroundInventoryUpdater> updater)
                            {
                                BackgroundInventoryUpdateManager.RegisterUpdater(__instance, i, updater.Instance);
                            }
                        }
                    }
                }
            }
        }
    }
}

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

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.AttachToEntity))]
        [HarmonyPostfix]
        private static void Postfix_AttachToEntity_EntityAlive(EntityAlive __instance, int __result)
        {
            if (__result >= 0)
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
            if (__instance.inventory != null && __instance.inventory.Hand.slotDatas != null)
            {
                var slotDatas = __instance.inventory.Hand.slotDatas;
                for (int i = 0; i < slotDatas.Length; i++)
                {
                    ItemInventoryData invData = slotDatas[i];
                    if (invData is { actionData: not null })
                    {
                        for (int j = 0; j < invData.actionData.Count; j++)
                        {
                            var actionData = invData.actionData[j];
                            if (actionData.IsModuleContainerFor(out IBackgroundInventoryUpdater updater))
                            {
                                BackgroundInventoryUpdateManager.RegisterUpdater(__instance, i, updater);
                            }
                        }
                    }
                }
            }
        }
    }
}

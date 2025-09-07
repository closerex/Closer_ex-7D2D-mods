using System.Collections.Generic;

namespace KFCommonUtilityLib
{
    public interface IBackgroundInventoryUpdater
    {
        int Index { get; }
        bool OnUpdate(ItemInventoryData invData);
    }

    public static class BackgroundInventoryUpdateManager
    {
        private static readonly Dictionary<int, List<IBackgroundInventoryUpdater>[]> dict_updaters = new Dictionary<int, List<IBackgroundInventoryUpdater>[]>();
        private static readonly Dictionary<int, List<IBackgroundInventoryUpdater>[]> dict_disabled = new Dictionary<int, List<IBackgroundInventoryUpdater>[]>();

        public static void Cleanup()
        {
            dict_updaters.Clear();
            dict_disabled.Clear();
        }

        public static void RegisterUpdater(EntityAlive entity, int slot, IBackgroundInventoryUpdater updater)
        {
            //do not handle remote entity update
            if (entity == null || entity.isEntityRemote)
                return;

            Inventory inv = entity.inventory;
            if (inv == null || slot < 0 || slot >= inv.GetSlotCount())
                return;

            if (!dict_updaters.TryGetValue(entity.entityId, out var arr_updaters))
            {
                arr_updaters = new List<IBackgroundInventoryUpdater>[inv.GetSlotCount()];
                dict_updaters[entity.entityId] = arr_updaters;
            }
            if (arr_updaters[slot] == null)
                arr_updaters[slot] = new List<IBackgroundInventoryUpdater>();
            int lastIndex = arr_updaters[slot].FindIndex(u => u.Index == updater.Index);
            if (lastIndex >= 0)
                //replace old updater, this happens on inventory initialization when player enters game
                arr_updaters[slot][lastIndex] = updater;
            else
                arr_updaters[slot].Add(updater);
        }

        public static void DisableUpdater(EntityAlive entity)
        {
            if (dict_updaters.TryGetValue(entity.entityId, out var updater))
            {
                dict_updaters.Remove(entity.entityId);
                dict_disabled.Add(entity.entityId, updater);
            }
        }

        public static void EnableUpdater(EntityAlive entity)
        {
            if (dict_disabled.TryGetValue(entity.entityId, out var updaters))
            {
                dict_disabled.Remove(entity.entityId);
                dict_updaters.Add(entity.entityId, updaters);
            }
        }

        public static void UnregisterUpdater(EntityAlive entity)
        {
            dict_updaters.Remove(entity.entityId);
        }

        public static void UnregisterUpdater(EntityAlive entity, int slot)
        {
            if (dict_updaters.TryGetValue(entity.entityId, out var arr_updaters) && arr_updaters != null)
                arr_updaters[slot] = null;
        }

        public static void Update(EntityAlive entity)
        {
            if (!entity.isEntityRemote && dict_updaters.TryGetValue(entity.entityId, out var arr_updaters) && arr_updaters != null)
            {
                Inventory inv = entity.inventory;
                int slotCount = inv.GetSlotCount();
                var prevInvData = entity.MinEventContext.ItemInventoryData;
                var prevItemValue = entity.MinEventContext.ItemValue;
                var prevActionData = entity.MinEventContext.ItemActionData;
                bool invChanged = false;
                for (int i = 0; i < slotCount; i++)
                    if (arr_updaters[i] != null)
                        foreach (var updater in arr_updaters[i])
                            if (updater != null)
                                invChanged |= updater.OnUpdate(inv.GetItemDataInSlot(i));
                entity.MinEventContext.ItemInventoryData = prevInvData;
                entity.MinEventContext.ItemActionData = prevActionData;
                entity.MinEventContext.ItemValue = prevItemValue;
                if (invChanged)
                    entity.inventory.CallOnToolbeltChangedInternal();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public interface IBackgroundInventoryUpdater
    {
        void OnUpdate(ItemInventoryData invData);
    }

    public static class BackgroundInventoryUpdateManager
    {
        private static readonly Dictionary<int, List<IBackgroundInventoryUpdater>[]> dict_updaters = new Dictionary<int, List<IBackgroundInventoryUpdater>[]>();

        public static void Cleanup()
        {
            dict_updaters.Clear();
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
            {
                arr_updaters[slot] = new List<IBackgroundInventoryUpdater>();
            }
            arr_updaters[slot].Add(updater);
        }

        public static void UnregisterUpdater(EntityAlive entity)
        {
            dict_updaters.Remove(entity.entityId);
        }

        public static void UnregisterUpdater(EntityAlive entity, int slot)
        {
            if (dict_updaters.TryGetValue(entity.entityId, out var arr_updaters) && arr_updaters != null)
            {
                arr_updaters[slot] = null;
            }
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
                for (int i = 0; i < slotCount; i++)
                {
                    if (arr_updaters[i] != null)
                    {
                        foreach (var updater in arr_updaters[i])
                        {
                            updater?.OnUpdate(inv.GetItemDataInSlot(i));
                        }
                    }
                }
                entity.MinEventContext.ItemInventoryData = prevInvData;
                entity.MinEventContext.ItemActionData = prevActionData;
                entity.MinEventContext.ItemValue = prevItemValue;
            }
        }
    }
}

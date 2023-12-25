using System;
using UnityEngine;
using static Inventory;

namespace KFCommonUtilityLib.Scripts.Utilities
{
    public static class MultiActionUtils
    {
        public static readonly string[] ActionMetaNames = new string[]
        {
            "Meta0",
            "Meta1",
            "Meta2",
            "Meta3",
            "Meta4",
        };
        public static readonly string[] ActionSelectedAmmoNames = new string[]
        {
            "AmmoIndex0",
            "AmmoIndex1",
            "AmmoIndex2",
            "AmmoIndex3",
            "AmmoIndex4",
        };

        public static void SetMinEventArrays()
        {
            MinEvent.Start = new[]
            {
                MinEventTypes.onSelfPrimaryActionStart,
                MinEventTypes.onSelfSecondaryActionStart,
                MinEventTypes.onSelfAction2Start,
                MinEventTypes.onSelfPrimaryActionStart,
                MinEventTypes.onSelfPrimaryActionStart,
            };

            MinEvent.Update = new[]
            {
                MinEventTypes.onSelfPrimaryActionUpdate,
                MinEventTypes.onSelfSecondaryActionUpdate,
                MinEventTypes.onSelfAction2Update,
                MinEventTypes.onSelfPrimaryActionUpdate,
                MinEventTypes.onSelfPrimaryActionUpdate,
            };

            MinEvent.End = new[]
            {
                MinEventTypes.onSelfPrimaryActionEnd,
                MinEventTypes.onSelfSecondaryActionEnd,
                MinEventTypes.onSelfAction2End,
                MinEventTypes.onSelfPrimaryActionEnd,
                MinEventTypes.onSelfPrimaryActionEnd,
            };
        }

        public static string GetPropertyName(int index, string prop)
        {
            return $"Action{index}.{prop}";
        }

        public static void FixedItemReloadServer(int entityId, int actionIndex)
        {
            if (GameManager.Instance.World == null)
            {
                return;
            }

            FixedItemReloadClient(entityId, actionIndex);

            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageFixedReload>().Setup(entityId, actionIndex), false);
                return;
            }
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageFixedReload>().Setup(entityId, actionIndex), false, -1, entityId, -1, -1);
        }

        public static void FixedItemReloadClient(int entityId, int actionIndex)
        {
            if (GameManager.Instance.World == null)
            {
                return;
            }
            EntityAlive entityAlive = (EntityAlive)GameManager.Instance.World.GetEntity(entityId);
            if (entityAlive != null && entityAlive.inventory.holdingItem.Actions[actionIndex] is ItemActionRanged actionRanged)
            {
                actionRanged.ReloadGun(entityAlive.inventory.holdingItemData.actionData[actionIndex]);
            }
        }

        public static void UpdateExecutingActionIndex(int index, ItemInventoryData invData, PlayerActionsLocal playerActions)
        {
            if (playerActions == null || !(invData.holdingEntity is EntityPlayerLocal player))
            {
                return;
            }

            player.MinEventContext.ItemActionData = invData.actionData[index];
        }

        public static void CopyLauncherValueToProjectile(ItemValue launcherValue, ItemValue projectileValue, int index)
        {
            projectileValue.Meta = launcherValue.type;
            projectileValue.SelectedAmmoTypeIndex = (byte)index;
            projectileValue.UseTimes = launcherValue.UseTimes;
            projectileValue.Quality = launcherValue.Quality;
            Array.Copy(launcherValue.Modifications, projectileValue.Modifications, launcherValue.Modifications.Length);
            Array.Copy(launcherValue.CosmeticMods, projectileValue.CosmeticMods, launcherValue.Modifications.Length);
        }

        public static void SetMinEventParamsActionData(EntityAlive entity, int actionIndex)
        {
            entity.MinEventContext.ItemActionData = entity.inventory.holdingItemData.actionData[actionIndex];
        }
    }
}
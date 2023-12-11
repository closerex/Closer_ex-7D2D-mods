using static Inventory;

namespace KFCommonUtilityLib.Scripts.Utilities
{
    public static class MultiActionUtils
    {
        public static readonly FastTags[] ActionIndexTags = new FastTags[]
        {
            FastTags.Parse("primary"),
            FastTags.Parse("secondary"),
            FastTags.Parse("tertiary"),
            FastTags.Parse("quaternary"),
            FastTags.Parse("quinary")
        };
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

        public static FastTags ActionIndexToTag(int index)
        {
            return ActionIndexTags[index];
        }

        public static FastTags GetItemTagsWithActionIndex(ItemActionData actionData)
        {
            return actionData.invData.item.ItemTags | actionData.ActionTags;
        }

        public static int TagToActionIndex(FastTags tag)
        {
            for (int i = 0; i < ActionIndexTags.Length; i++)
            {
                if (tag.Test_AnySet(ActionIndexTags[i]))
                    return i;
            }
            return 0;
        }

        public static string GetPropertyName(int index, string prop)
        {
            return $"Action{index}.{prop}";
        }

        public static void MultiActionReload(ItemActionRanged.ItemActionDataRanged itemActionData)
        {
            ItemValue itemValue = itemActionData.invData.itemValue;
            int actionIndex = itemActionData.indexInEntityOfAction;
            if (actionIndex == 0)
            {
                itemValue.Meta = itemValue.Meta + itemActionData.reloadAmount;
            }
            string metaname = ActionMetaNames[actionIndex];
            if (!itemValue.HasMetadata(metaname))
            {
                itemValue.SetMetadata(metaname, itemActionData.reloadAmount, TypedMetadataValue.TypeTag.Integer);
            }
            else
            {
                int meta = (int)itemValue.GetMetadata(metaname);
                itemValue.SetMetadata(metaname, meta + itemActionData.reloadAmount, TypedMetadataValue.TypeTag.Integer);
            }
        }

        public static int MultiActionGetMeta(ItemActionData itemActionData)
        {
            ItemValue itemValue = itemActionData.invData.itemValue;
            if (itemActionData.indexInEntityOfAction == 0)
            {
                return itemValue.Meta;
            }

            string metaname = ActionMetaNames[itemActionData.indexInEntityOfAction];
            if (!itemValue.HasMetadata(metaname))
            {
                itemValue.SetMetadata(metaname, 0, TypedMetadataValue.TypeTag.Integer);
                return 0;
            }
            return (int)itemValue.GetMetadata(metaname);
        }

        public static int MultiActionGetSelectedAmmoTypeIndex(ItemActionData itemActionData)
        {
            ItemValue itemValue = itemActionData.invData.itemValue;
            if (itemActionData.indexInEntityOfAction == 0)
            {
                return itemValue.SelectedAmmoTypeIndex;
            }

            string ammoindex = ActionSelectedAmmoNames[itemActionData.indexInEntityOfAction];
            if (!itemValue.HasMetadata(ammoindex))
            {
                itemValue.SetMetadata(ammoindex, 0, TypedMetadataValue.TypeTag.Integer);
                return 0;
            }
            return (int)itemValue.GetMetadata(ammoindex);
        }

        public static void SetCurrentMetaAndAmmoIndex(ItemActionData itemActionData)
        {
            if(itemActionData.indexInEntityOfAction == 0)
                return;
            ItemValue itemValue = itemActionData.invData.itemValue;
            string metaname = ActionMetaNames[0];
            string ammoindex = ActionSelectedAmmoNames[0];
            itemValue.SetMetadata(metaname, itemValue.Meta, TypedMetadataValue.TypeTag.Integer);
            itemValue.SetMetadata(ammoindex, itemValue.SelectedAmmoTypeIndex, TypedMetadataValue.TypeTag.Integer);

            metaname = ActionMetaNames[itemActionData.indexInEntityOfAction];
            ammoindex = ActionSelectedAmmoNames[itemActionData.indexInEntityOfAction];

            if (!itemValue.HasMetadata(ammoindex))
            {
                itemValue.SetMetadata(ammoindex, 0, TypedMetadataValue.TypeTag.Integer);
                itemValue.SelectedAmmoTypeIndex = 0;
            }
            else
            {
                itemValue.SelectedAmmoTypeIndex = (byte)itemValue.GetMetadata(ammoindex);
            }
            if (!itemValue.HasMetadata(metaname))
            {
                itemValue.SetMetadata(metaname, 0, TypedMetadataValue.TypeTag.Integer);
                itemValue.Meta = 0;
            }
            else
            {
                itemValue.Meta = (int)itemValue.GetMetadata(metaname);
            }
        }

        public static void ResetCurrentMetaAndAmmoIndex(ItemActionData itemActionData)
        {
            if(itemActionData.indexInEntityOfAction == 0)
                return;
            ItemValue itemValue = itemActionData.invData.itemValue;
            string metaname = ActionMetaNames[itemActionData.indexInEntityOfAction];
            string ammoindex = ActionSelectedAmmoNames[itemActionData.indexInEntityOfAction];
            itemValue.SetMetadata(metaname, itemValue.Meta, TypedMetadataValue.TypeTag.Integer);
            itemValue.SetMetadata(ammoindex, itemValue.SelectedAmmoTypeIndex, TypedMetadataValue.TypeTag.Integer);

            metaname = ActionMetaNames[0];
            ammoindex = ActionSelectedAmmoNames[0];
            if (!itemValue.HasMetadata(ammoindex))
            {
                itemValue.SetMetadata(ammoindex, 0, TypedMetadataValue.TypeTag.Integer);
                itemValue.SelectedAmmoTypeIndex = 0;
            }
            else
            {
                itemValue.SelectedAmmoTypeIndex = (byte)itemValue.GetMetadata(ammoindex);
            }
            if (!itemValue.HasMetadata(metaname))
            {
                itemValue.SetMetadata(metaname, 0, TypedMetadataValue.TypeTag.Integer);
                itemValue.Meta = 0;
            }
            else
            {
                itemValue.Meta = (int)itemValue.GetMetadata(metaname);
            }
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
    }
}
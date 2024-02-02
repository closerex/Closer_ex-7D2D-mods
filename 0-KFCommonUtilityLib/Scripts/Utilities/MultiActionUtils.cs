using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections;
using System.Collections.Generic;
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
        public static readonly int ExecutingActionIndexHash = Animator.StringToHash("ExecutingActionIndex");

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

        //public static int GetActionIndexForItemValue(this ItemValue self)
        //{
        //    object val = self.GetMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX);
        //    if (val is int index)
        //        return index;
        //    return 0;
        //}

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
                entityAlive.MinEventContext.ItemActionData = entityAlive.inventory.holdingItemData.actionData[actionIndex];
                actionRanged.ReloadGun(entityAlive.inventory.holdingItemData.actionData[actionIndex]);
            }
        }

        public static void CopyLauncherValueToProjectile(ItemValue launcherValue, ItemValue projectileValue, int index)
        {
            projectileValue.Meta = launcherValue.type;
            projectileValue.SelectedAmmoTypeIndex = (byte)index;
            projectileValue.UseTimes = launcherValue.UseTimes;
            projectileValue.Quality = launcherValue.Quality;
            projectileValue.Modifications = new ItemValue[launcherValue.Modifications.Length];
            Array.Copy(launcherValue.Modifications, projectileValue.Modifications, launcherValue.Modifications.Length);
            projectileValue.CosmeticMods = new ItemValue[launcherValue.CosmeticMods.Length];
            Array.Copy(launcherValue.CosmeticMods, projectileValue.CosmeticMods, launcherValue.CosmeticMods.Length);
        }

        public static void SetMinEventParamsActionData(EntityAlive entity, int actionIndex)
        {
            if (actionIndex >= 0 && actionIndex < entity.inventory?.holdingItemData?.actionData?.Count)
                entity.MinEventContext.ItemActionData = entity.inventory.holdingItemData.actionData[actionIndex];
        }

        public static string GetPropertyOverrideForAction(ItemValue self, string _propertyName, string _originalValue, int actionIndex)
        {
            if (self.Modifications.Length == 0 && self.CosmeticMods.Length == 0)
            {
                return _originalValue;
            }
            string text = "";
            string itemName = self.ItemClass.GetItemName();
            for (int i = 0; i < self.Modifications.Length; i++)
            {
                ItemValue itemValue = self.Modifications[i];
                if (itemValue != null)
                {
                    ItemClassModifier itemClassModifier = itemValue.ItemClass as ItemClassModifier;
                    if (itemClassModifier != null && GetPropertyOverrideForMod(itemClassModifier, _propertyName, itemName, ref text, actionIndex))
                    {
                        return text;
                    }
                }
            }
            text = "";
            for (int j = 0; j < self.CosmeticMods.Length; j++)
            {
                ItemValue itemValue2 = self.CosmeticMods[j];
                if (itemValue2 != null)
                {
                    ItemClassModifier itemClassModifier2 = itemValue2.ItemClass as ItemClassModifier;
                    if (itemClassModifier2 != null && GetPropertyOverrideForMod(itemClassModifier2, _propertyName, itemName, ref text, actionIndex))
                    {
                        return text;
                    }
                }
            }
            return _originalValue;
        }

        public static bool GetPropertyOverrideForMod(ItemClassModifier mod, string _propertyName, string _itemName, ref string _value, int actionIndex)
        {
            string itemNameWithActionIndex = $"{_itemName}_{actionIndex}";
            if (mod.PropertyOverrides.ContainsKey(itemNameWithActionIndex) && mod.PropertyOverrides[itemNameWithActionIndex].Values.ContainsKey(_propertyName))
            {
                _value = mod.PropertyOverrides[itemNameWithActionIndex].Values[_propertyName];
                return true;
            }
            if (mod.PropertyOverrides.ContainsKey(_itemName) && mod.PropertyOverrides[_itemName].Values.ContainsKey(_propertyName))
            {
                _value = mod.PropertyOverrides[_itemName].Values[_propertyName];
                return true;
            }
            if (mod.PropertyOverrides.ContainsKey("*") && mod.PropertyOverrides["*"].Values.ContainsKey(_propertyName))
            {
                _value = mod.PropertyOverrides["*"].Values[_propertyName];
                return true;
            }
            return false;
        }

        public static int GetMode(this ItemValue self)
        {
            object mode = self.GetMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX);
            if (mode is int)
            {
                return (int)mode;
            }
            return 0;
        }

        public static int GetActionIndexByEntityEventParams(EntityAlive entity)
        {
            return GetActionIndexByEventParams(entity?.MinEventContext ?? MinEventParams.CachedEventParam);
        }

        public static int GetActionIndexByEventParams(MinEventParams pars)
        {
            if (pars?.ItemActionData == null)
                return 0;
            return pars.ItemActionData.indexInEntityOfAction;
        }

        public static int GetActionIndexByMetaData(this ItemValue self)
        {
            int mode = self.GetMode();
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            return indice.GetActionIndexForMode(mode);
        }

        public static int GetSelectedAmmoIndexByMode(this ItemValue self, int mode)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            int metaIndex = indice.GetMetaIndexForMode(mode);
            object ammoIndex = self.GetMetadata(ActionSelectedAmmoNames[metaIndex]);
            if (ammoIndex is int)
            {
                return (int)ammoIndex;
            }
            return self.SelectedAmmoTypeIndex;
        }

        public static int GetMetaByMode(this ItemValue self, int mode)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            int metaIndex = indice.GetMetaIndexForMode(mode);
            object meta = self.GetMetadata(ActionMetaNames[metaIndex]);
            if (meta is int)
            {
                return (int)meta;
            }
            return self.Meta;
        }

        public static int GetSelectedAmmoIndexByActionIndex(this ItemValue self, int actionIndex)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            int mode = indice.GetModeForAction(actionIndex);
            if (mode < 0)
                return self.SelectedAmmoTypeIndex;
            int metaIndex = indice.GetMetaIndexForMode(mode);
            object ammoIndex = self.GetMetadata(ActionSelectedAmmoNames[metaIndex]);
            if (ammoIndex is int)
            {
                return (int)ammoIndex;
            }
            return self.SelectedAmmoTypeIndex;
        }

        public static int GetMetaByActionIndex(this ItemValue self, int actionIndex)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            int mode = indice.GetModeForAction(actionIndex);
            if (mode < 0)
                return self.Meta;
            int metaIndex = indice.GetMetaIndexForMode(mode);
            object meta = self.GetMetadata(ActionMetaNames[metaIndex]);
            if (meta is int)
            {
                //Log.Out($"GetMetaByActionIndex: mode: {mode}, action: {metaIndex}, meta: {(int)meta}\n{StackTraceUtility.ExtractStackTrace()}");
                return (int)meta;
            }
            return self.Meta;
        }

        public static void SetMinEventParamsByEntityInventory(EntityAlive entity)
        {
            if (entity != null && entity.MinEventContext != null)
            {
                entity.MinEventContext.ItemActionData = entity.inventory?.holdingItemData?.actionData[MultiActionManager.GetActionIndexForEntityID(entity.entityId)];
            }
        }

        public static bool MultiActionRemoveAmmoFromItemStack(ItemStack stack, List<ItemStack> result)
        {
            ItemValue itemValue = stack.itemValue;
            object mode = itemValue.GetMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX);
            if (mode is false || mode is null)
            {
                return false;
            }
            MultiActionIndice indices = MultiActionManager.GetActionIndiceForItemID(itemValue.type);
            ItemClass item = ItemClass.GetForId(itemValue.type);
            for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
            {
                int metaIndex = indices.GetMetaIndexForMode(i);
                if (metaIndex < 0)
                {
                    break;
                }

                int actionIndex = indices.GetActionIndexForMode(i);
                if (item.Actions[actionIndex] is ItemActionRanged ranged && !(ranged is ItemActionTextureBlock))
                {
                    object meta = itemValue.GetMetadata(MultiActionUtils.ActionMetaNames[metaIndex]);
                    object ammoIndex = itemValue.GetMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex]);
                    if (meta is int && ammoIndex is int && (int)meta > 0)
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[metaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                        ItemStack ammoStack = new ItemStack(ItemClass.GetItem(ranged.MagazineItemNames[(int)ammoIndex]), (int)meta);
                        result.Add(ammoStack);
                        Log.Out($"Remove ammo: metadata {MultiActionUtils.ActionMetaNames[metaIndex]}, meta {(int)meta}, left {itemValue.GetMetadata(MultiActionUtils.ActionMetaNames[metaIndex])}");
                    }
                }
            }
            itemValue.Meta = 0;
            return true;
        }

        public static readonly ItemActionData[] DummyActionDatas = new ItemActionData[]
        {
            new ItemActionData(null, 0),
            new ItemActionData(null, 3),
            new ItemActionData(null, 4)
        };

        public static int GetMultiActionInitialMetaData(this ItemClass itemClass, ItemValue itemValue)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(itemClass.Id);
            if (indice.modeCount <= 1)
            {
                return itemClass.GetInitialMetadata(itemValue);
            }

            var prevItemValue = MinEventParams.CachedEventParam.ItemValue;
            var prevActionData = MinEventParams.CachedEventParam.ItemActionData;
            MinEventParams.CachedEventParam.ItemValue = itemValue;
            itemValue.SetMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX, 0, TypedMetadataValue.TypeTag.Integer);
            int ret = 0;
            for (int i = 0; i < indice.modeCount; i++)
            {
                MinEventParams.CachedEventParam.ItemActionData = DummyActionDatas[i];
                int meta = itemClass.Actions[indice.GetActionIndexForMode(i)].GetInitialMeta(itemValue);
                if(i == 0)
                {
                    ret = meta;
                }
                itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[indice.GetMetaIndexForMode(i)], meta, TypedMetadataValue.TypeTag.Integer);
            }
            MinEventParams.CachedEventParam.ItemValue = prevItemValue;
            MinEventParams.CachedEventParam.ItemActionData = prevActionData;
            return ret;
        }

        public static void SetCachedEventParamsDummyAction(ItemStack itemStack)
        {
            ItemClass itemClass = itemStack.itemValue.ItemClass;
            if (itemClass != null)
            {
                MinEventParams.CachedEventParam.ItemActionData = MultiActionUtils.DummyActionDatas[itemStack.itemValue.GetMode()];
            }
            MinEventParams.CachedEventParam.ItemValue = itemStack.itemValue;
            MinEventParams.CachedEventParam.Seed = itemStack.itemValue.Seed;
        }
    }
}
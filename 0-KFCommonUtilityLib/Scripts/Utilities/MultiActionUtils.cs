﻿using System;
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
    }
}
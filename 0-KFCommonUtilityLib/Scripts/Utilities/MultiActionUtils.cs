using System;
using System.Collections.Generic;
using UnityEngine;

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

        public static void SetMinEventArrays(ref ModEvents.SGameAwakeData _)
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
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageFixedReload>().Setup(entityId, actionIndex), false, -1, entityId);
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

        public static string GetPropertyOverrideForAction(this ItemValue self, string _propertyName, string _originalValue, int actionIndex)
        {
            if (self.Modifications.Length == 0 && self.CosmeticMods.Length == 0)
            {
                return _originalValue;
            }
            string text = "";
            ItemClass item = self.ItemClass;
            for (int i = 0; i < self.Modifications.Length; i++)
            {
                ItemValue itemValue = self.Modifications[i];
                if (itemValue != null)
                {
                    if (itemValue.ItemClass is ItemClassModifier mod && GetPropertyOverrideForMod(mod, _propertyName, item, ref text, actionIndex))
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
                    if (itemValue2.ItemClass is ItemClassModifier cos && GetPropertyOverrideForMod(cos, _propertyName, item, ref text, actionIndex))
                    {
                        return text;
                    }
                }
            }
            return _originalValue;
        }

        public static IEnumerable<string> GetAllPropertyOverridesForAction(this ItemValue self, string _propertyName, int actionIndex)
        {
            if (self.Modifications.Length == 0 && self.CosmeticMods.Length == 0)
            {
                yield break;
            }

            string text = "";
            ItemClass item = self.ItemClass;
            for (int i = 0; i < self.Modifications.Length; i++)
            {
                ItemValue itemValue = self.Modifications[i];
                if (itemValue != null)
                {
                    if (itemValue.ItemClass is ItemClassModifier mod && GetPropertyOverrideForMod(mod, _propertyName, item, ref text, actionIndex))
                    {
                        yield return text;
                    }
                }
            }
            text = "";
            for (int j = 0; j < self.CosmeticMods.Length; j++)
            {
                ItemValue itemValue2 = self.CosmeticMods[j];
                if (itemValue2 != null)
                {
                    if (itemValue2.ItemClass is ItemClassModifier cos && GetPropertyOverrideForMod(cos, _propertyName, item, ref text, actionIndex))
                    {
                        yield return text;
                    }
                }
            }
        }

        public static bool GetPropertyOverrideForMod(ItemClassModifier mod, string _propertyName, ItemClass _item, ref string _value, int actionIndex)
        {
            //Log.Out($"Get property override for item {_item.Name} itemID{_item.Id} property {_propertyName} mod {mod.Name} modID {mod.Id} action {actionIndex} should exclude {MultiActionManager.ShouldExcludeMod(_item.Id, mod.Id, actionIndex)}");
            if (MultiActionManager.ShouldExcludeProperty(_item.Id, mod.Id, actionIndex))
                return false;
            string _itemName = _item.GetItemName();
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
            itemNameWithActionIndex = $"*_{actionIndex}";
            if (mod.PropertyOverrides.ContainsKey(itemNameWithActionIndex) && mod.PropertyOverrides[itemNameWithActionIndex].Values.ContainsKey(_propertyName))
            {
                _value = mod.PropertyOverrides[itemNameWithActionIndex].Values[_propertyName];
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
            if (mode is int v)
            {
                return v;
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
            if (metaIndex >= 0)
            {
                object ammoIndex = self.GetMetadata(ActionSelectedAmmoNames[metaIndex]);
                if (ammoIndex is int)
                {
                    return (int)ammoIndex;
                }
            }
            return self.SelectedAmmoTypeIndex;
        }

        public static int GetMetaByMode(this ItemValue self, int mode)
        {
            MultiActionIndice indice = MultiActionManager.GetActionIndiceForItemID(self.type);
            int metaIndex = indice.GetMetaIndexForMode(mode);
            if (metaIndex >= 0)
            {
                object meta = self.GetMetadata(ActionMetaNames[metaIndex]);
                if (meta is int)
                {
                    return (int)meta;
                }
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
            if (metaIndex >= 0)
            {
                object ammoIndex = self.GetMetadata(ActionSelectedAmmoNames[metaIndex]);
                if (ammoIndex is int)
                {
                    return (int)ammoIndex;
                }
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
            if (metaIndex >= 0)
            {
                object meta = self.GetMetadata(ActionMetaNames[metaIndex]);
                if (meta is int)
                {
                    //Log.Out($"GetMetaByActionIndex: mode: {mode}, action: {metaIndex}, meta: {(int)meta}\n{StackTraceUtility.ExtractStackTrace()}");
                    return (int)meta;
                }
            }

            return self.Meta;
        }

        public static void SetMinEventParamsByEntityInventory(EntityAlive entity)
        {
            if (entity != null && entity.IsAlive() && entity.MinEventContext != null)
            {
                entity.MinEventContext.ItemActionData = entity.inventory?.holdingItemData?.actionData[MultiActionManager.GetActionIndexForEntity(entity)];
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

                ItemAction itemAction = itemClass.Actions[indice.GetActionIndexForMode(i)];
                int meta = itemAction.GetInitialMeta(itemValue);
                if (i == 0)
                {
                    ret = meta;
                }
                else if (itemAction.Properties.Contains("ActionUnlocked") && !itemAction.Properties.GetBool("ActionUnlocked"))
                {
                    meta = 0;
                }
                if (indice.GetMetaIndexForMode(i) == indice.GetActionIndexForMode(i))
                    itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[indice.GetMetaIndexForMode(i)], meta, TypedMetadataValue.TypeTag.Integer);
            }
            MinEventParams.CachedEventParam.ItemValue = prevItemValue;
            MinEventParams.CachedEventParam.ItemActionData = prevActionData;
            return ret;
        }

        public static void SetCachedEventParamsDummyAction(ItemValue itemValue)
        {
            ItemClass itemClass = itemValue?.ItemClass;
            if (itemClass != null)
            {
                MinEventParams.CachedEventParam.ItemActionData = MultiActionUtils.DummyActionDatas[itemValue.GetMode()];
                MinEventParams.CachedEventParam.ItemValue = itemValue;
                MinEventParams.CachedEventParam.Seed = itemValue.Seed;
            }
        }

        public static string GetDisplayTypeForAction(ItemStack itemStack)
        {
            return GetDisplayTypeForAction(itemStack?.itemValue);
        }

        public static string GetDisplayTypeForAction(ItemValue itemValue)
        {
            if (itemValue == null || itemValue.IsEmpty())
            {
                return "";
            }
            if (itemValue.ItemClass.Actions[itemValue.GetActionIndexByMetaData()] is IModuleContainerFor<ActionModuleMultiActionFix> module)
            {
                return module.Instance.GetDisplayType(itemValue);
            }
            return itemValue.ItemClass.DisplayType;
        }

        public static bool CanCompare(ItemValue itemValue1, ItemValue itemValue2)
        {
            if (itemValue1 == null || itemValue2 == null || itemValue1.IsEmpty() || itemValue2.IsEmpty())
            {
                return false;
            }

            string displayType1 = itemValue1.ItemClass.IsBlock() ? Block.list[itemValue1.ItemClass.Id].DisplayType : GetDisplayTypeForAction(itemValue1);
            string displayType2 = itemValue2.ItemClass.IsBlock() ? Block.list[itemValue2.ItemClass.Id].DisplayType : GetDisplayTypeForAction(itemValue2);
            ItemDisplayEntry displayStatsForTag = UIDisplayInfoManager.Current.GetDisplayStatsForTag(displayType1);
            ItemDisplayEntry displayStatsForTag2 = UIDisplayInfoManager.Current.GetDisplayStatsForTag(displayType2);
            return displayStatsForTag != null && displayStatsForTag2 != null && displayStatsForTag.DisplayGroup == displayStatsForTag2.DisplayGroup;
        }
    }
}
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.StaticManagers
{
    /// <summary>
    /// only used for item modifier tags.
    /// </summary>
    public static class LocalItemTagsManager
    {
        public static FastTags GetTags(ItemValue itemValue)
        {
            var str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsAppend"));
            FastTags tagsToAdd = string.IsNullOrEmpty(str) ? FastTags.none : FastTags.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsRemove"));
            FastTags tagsToRemove = string.IsNullOrEmpty(str) ? FastTags.none : FastTags.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static FastTags GetTagsAsIfNotInstalled(ItemValue itemValue, ItemValue modValue)
        {
            var str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsAppend"));
            FastTags tagsToAdd = string.IsNullOrEmpty(str) ? FastTags.none : FastTags.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsRemove"));
            FastTags tagsToRemove = string.IsNullOrEmpty(str) ? FastTags.none : FastTags.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static IEnumerable<string> GetPropertyOverrides(this ItemValue self, string _propertyName)
        {
            if (self == null || (self.Modifications.Length == 0 && self.CosmeticMods.Length == 0))
            {
                yield break;
            }

            string _value = "";
            string itemName = self.ItemClass.GetItemName();
            for (int i = 0; i < self.Modifications.Length; i++)
            {
                ItemValue itemValue = self.Modifications[i];
                if (itemValue != null && itemValue.ItemClass is ItemClassModifier itemClassModifier && itemClassModifier.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }

            _value = "";
            for (int j = 0; j < self.CosmeticMods.Length; j++)
            {
                ItemValue itemValue2 = self.CosmeticMods[j];
                if (itemValue2 != null && itemValue2.ItemClass is ItemClassModifier itemClassModifier2 && itemClassModifier2.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }
        }

        public static IEnumerable<string> GetPropertyOverridesWithoutMod(this ItemValue self, ItemValue mod, string _propertyName)
        {
            if (self == null || (self.Modifications.Length == 0 && self.CosmeticMods.Length == 0))
            {
                yield break;
            }

            string _value = "";
            string itemName = self.ItemClass.GetItemName();
            for (int i = 0; i < self.Modifications.Length; i++)
            {
                ItemValue itemValue = self.Modifications[i];
                if (itemValue != null && itemValue != mod && itemValue.ItemClass is ItemClassModifier itemClassModifier && itemClassModifier.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }

            _value = "";
            for (int j = 0; j < self.CosmeticMods.Length; j++)
            {
                ItemValue itemValue2 = self.CosmeticMods[j];
                if (itemValue2 != null && itemValue2 != mod && itemValue2.ItemClass is ItemClassModifier itemClassModifier2 && itemClassModifier2.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }
        }
    }
}

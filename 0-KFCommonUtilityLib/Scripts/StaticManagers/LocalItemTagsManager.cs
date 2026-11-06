using System.Collections.Generic;

namespace KFCommonUtilityLib
{
    /// <summary>
    /// only used for item modifier tags.
    /// </summary>
    public static class LocalItemTagsManager
    {
        public static bool CanInstall(FastTags<TagGroup.Global> itemTags, ItemClassModifier modClass)
        {
            return modClass != null && (modClass.InstallableTags.IsEmpty || itemTags.Test_AnySet(modClass.InstallableTags)) && (modClass.DisallowedTags.IsEmpty || !itemTags.Test_AnySet(modClass.DisallowedTags));
        }

        public static bool CanStay(FastTags<TagGroup.Global> itemTags, ItemClassModifier modClass)
        {
            return modClass == null || CanInstall(itemTags, modClass);
        }

        public static bool HasInstallableTags(FastTags<TagGroup.Global> itemTags, FastTags<TagGroup.Global> requiredTags)
        {
            return requiredTags.IsEmpty || itemTags.Test_AnySet(requiredTags);
        }

        public static bool HasDisallowedTags(FastTags<TagGroup.Global> itemTags, FastTags<TagGroup.Global> disallowedTags)
        {
            return !disallowedTags.IsEmpty && itemTags.Test_AnySet(disallowedTags);
        }

        public static bool CanInstallMod(this ItemValue itemValue, ItemClassModifier modToInstall)
        {
            return modToInstall != null && CanKeepMods(itemValue, null, GetTagsAsIfInstalled(itemValue, modToInstall));
        }

        public static bool CanSwapMod(this ItemValue itemValue, ItemValue modToSwap, ItemClassModifier modToInstall)
        {
            return modToInstall != null && CanKeepMods(itemValue, modToSwap, GetTagsAsIfSwapped(itemValue, modToSwap, modToInstall));
        }

        public static bool CanRemoveMod(this ItemValue itemValue, ItemValue modToRemove)
        {
            return CanKeepMods(itemValue, modToRemove, GetTagsAsIfNotInstalled(itemValue, modToRemove));
        }

        private static bool CanKeepMods(ItemValue itemValue, ItemValue modToSkip, FastTags<TagGroup.Global> itemTags)
        {
            for (int i = 0; i < itemValue.CosmeticModCount; i++)
            {
                var mod = itemValue.GetCosmeticMod(i);
                if (mod == null || mod.IsEmpty() || mod == modToSkip)
                {
                    continue;
                }
                if (!CanStay(itemTags, mod.ItemClass as ItemClassModifier))
                {
                    return false;
                }
            }

            for (int i = 0; i < itemValue.ModificationCount; i++)
            {
                var mod = itemValue.GetModification(i);
                if (mod == null || mod.IsEmpty() || mod == modToSkip)
                {
                    continue;
                }
                if (!CanStay(itemTags, mod.ItemClass as ItemClassModifier))
                {
                    return false;
                }
            }

            return true;
        }

        public static FastTags<TagGroup.Global> GetTags(ItemValue itemValue)
        {
            var str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsAppend"));
            FastTags<TagGroup.Global> tagsToAdd = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsRemove"));
            FastTags<TagGroup.Global> tagsToRemove = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static FastTags<TagGroup.Global> GetTagsAsIfNotInstalled(ItemValue itemValue, ItemValue modValue)
        {
            var str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsAppend"));
            FastTags<TagGroup.Global> tagsToAdd = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsRemove"));
            FastTags<TagGroup.Global> tagsToRemove = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static FastTags<TagGroup.Global> GetTagsAsIfInstalled(ItemValue itemValue, ItemClassModifier modClass)
        {
            string itemName = itemValue.ItemClass.GetItemName();
            string val = "";
            var str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsAppend"));
            if (modClass.GetPropertyOverride("ItemTagsAppend", itemName, ref val))
            {
                str = string.Join(",", str, val);
            }
            FastTags<TagGroup.Global> tagsToAdd = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverrides("ItemTagsRemove"));
            if (modClass.GetPropertyOverride("ItemTagsRemove", itemName, ref val))
            {
                str = string.Join(",", str, val);
            }
            FastTags<TagGroup.Global> tagsToRemove = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static FastTags<TagGroup.Global> GetTagsAsIfSwapped(ItemValue itemValue, ItemValue modValue, ItemClassModifier modClass)
        {
            string itemName = itemValue.ItemClass.GetItemName();
            string val = "";
            var str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsAppend"));
            if (modClass.GetPropertyOverride("ItemTagsAppend", itemName, ref val))
            {
                str = string.Join(",", str, val);
            }
            FastTags<TagGroup.Global> tagsToAdd = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            str = string.Join(",", itemValue.GetPropertyOverridesWithoutMod(modValue, "ItemTagsRemove"));
            if (modClass.GetPropertyOverride("ItemTagsRemove", itemName, ref val))
            {
                str = string.Join(",", str, val);
            }
            FastTags<TagGroup.Global> tagsToRemove = string.IsNullOrEmpty(str) ? FastTags<TagGroup.Global>.none : FastTags<TagGroup.Global>.Parse(str);
            return (itemValue.ItemClass.ItemTags | tagsToAdd).Remove(tagsToRemove);
        }

        public static IEnumerable<string> GetPropertyOverrides(this ItemValue self, string _propertyName)
        {
            if (self == null || (self.ModificationCount == 0 && self.CosmeticModCount == 0))
            {
                yield break;
            }

            string _value = "";
            string itemName = self.ItemClass.GetItemName();
            for (int i = 0; i < self.ModificationCount; i++)
            {
                ItemValue itemValue = self.GetModification(i);
                if (itemValue is { ItemClass: ItemClassModifier itemClassModifier } && itemClassModifier.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }

            _value = "";
            for (int j = 0; j < self.CosmeticModCount; j++)
            {
                ItemValue itemValue2 = self.GetCosmeticMod(j);
                if (itemValue2 is { ItemClass: ItemClassModifier itemClassModifier2 } && itemClassModifier2.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }
        }

        public static IEnumerable<string> GetPropertyOverridesWithoutMod(this ItemValue self, ItemValue mod, string _propertyName)
        {
            if (self == null || (self.ModificationCount == 0 && self.CosmeticModCount == 0))
            {
                yield break;
            }

            string _value = "";
            string itemName = self.ItemClass.GetItemName();
            for (int i = 0; i < self.ModificationCount; i++)
            {
                ItemValue itemValue = self.GetModification(i);
                if (itemValue != null && itemValue != mod && itemValue.ItemClass is ItemClassModifier itemClassModifier && itemClassModifier.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }

            _value = "";
            for (int j = 0; j < self.CosmeticModCount; j++)
            {
                ItemValue itemValue2 = self.GetCosmeticMod(j);
                if (itemValue2 != null && itemValue2 != mod && itemValue2.ItemClass is ItemClassModifier itemClassModifier2 && itemClassModifier2.GetPropertyOverride(_propertyName, itemName, ref _value))
                {
                    yield return _value;
                }
            }
        }
    }
}

using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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
            Log.Out($"mod class is null {modClass is null}");
            if (modClass != null)
            {
                Log.Out($"installable {modClass.InstallableTags.IsEmpty || itemTags.Test_AnySet(modClass.InstallableTags)}, disallowed {modClass.DisallowedTags.IsEmpty || !itemTags.Test_AnySet(modClass.DisallowedTags)}");
            }
            return modClass == null || ((modClass.InstallableTags.IsEmpty || itemTags.Test_AnySet(modClass.InstallableTags)) && (modClass.DisallowedTags.IsEmpty || !itemTags.Test_AnySet(modClass.DisallowedTags)));
        }

        public static bool CanInstallMod(this ItemValue itemValue, ItemClassModifier modToInstall)
        {
            if (modToInstall == null)
            {
                return false;
            }

            FastTags<TagGroup.Global> tags_after_install = GetTagsAsIfInstalled(itemValue, modToInstall);

            if (itemValue.CosmeticMods != null)
            {
                foreach (var cosValue in itemValue.CosmeticMods)
                {
                    if (cosValue == null || cosValue.IsEmpty())
                    {
                        continue;
                    }

                    ItemClassModifier cosClass = cosValue.ItemClass as ItemClassModifier;
                    if (cosClass == null)
                    {
                        continue;
                    }

                    if (!tags_after_install.Test_AnySet(cosClass.InstallableTags) || tags_after_install.Test_AnySet(cosClass.DisallowedTags))
                    {
                        return false;
                    }
                }
            }

            if (itemValue.Modifications != null)
            {
                foreach (var modValue in itemValue.Modifications)
                {
                    if (modValue == null || modValue.IsEmpty())
                    {
                        continue;
                    }

                    ItemClassModifier modClass = modValue.ItemClass as ItemClassModifier;
                    if (modClass == null)
                    {
                        continue;
                    }

                    if (!tags_after_install.Test_AnySet(modClass.InstallableTags) || tags_after_install.Test_AnySet(modClass.DisallowedTags))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool CanSwapMod(this ItemValue itemValue, ItemValue modToSwap, ItemClassModifier modToInstall)
        {
            if (modToInstall == null)
            {
                return false;
            }

            FastTags<TagGroup.Global> tags_after_swap = GetTagsAsIfSwapped(itemValue, modToSwap, modToInstall);

            if (itemValue.CosmeticMods != null)
            {
                foreach (var cosValue in itemValue.CosmeticMods)
                {
                    if (cosValue == null || cosValue.IsEmpty() || cosValue == modToSwap)
                    {
                        continue;
                    }

                    ItemClassModifier cosClass = cosValue.ItemClass as ItemClassModifier;
                    if (cosClass == null)
                    {
                        continue;
                    }

                    if (!tags_after_swap.Test_AnySet(cosClass.InstallableTags) || tags_after_swap.Test_AnySet(cosClass.DisallowedTags))
                    {
                        return false;
                    }
                }
            }

            if (itemValue.Modifications != null)
            {
                foreach (var modValue in itemValue.Modifications)
                {
                    if (modValue == null || modValue.IsEmpty() || modValue == modToSwap)
                    {
                        continue;
                    }

                    ItemClassModifier modClass = modValue.ItemClass as ItemClassModifier;
                    if (modClass == null)
                    {
                        continue;
                    }

                    if (!tags_after_swap.Test_AnySet(modClass.InstallableTags) || tags_after_swap.Test_AnySet(modClass.DisallowedTags))
                    {
                        return false;
                    }
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

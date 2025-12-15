using System;
using System.Collections.Generic;

namespace KFCommonUtilityLib
{
    public static class ItemClassModuleManager
    {
        private static readonly Dictionary<string, string> dict_classtypes = new Dictionary<string, string>();

        internal static void Init()
        {
            ModuleManagers.OnAssemblyCreated += static () => dict_classtypes.Clear();
            ModuleManagers.OnAssemblyLoaded += static () =>
            {
                ModuleManagers.LogOut($"Start replacing ItemClass...");
                foreach (var pair in dict_classtypes)
                {
                    var item = ItemClass.GetItemClass(pair.Key);
                    if (ModuleManagers.TryFindType(pair.Value, out Type classType))
                    {
                        ModuleManagers.LogOut($"Replace ItemClass {item.GetType().FullName} with {classType.FullName} on item {item.GetItemName()}");
                        var itemNew = (ItemClass)Activator.CreateInstance(classType);
                        item.PreInitCopyTo(itemNew);
                        if (item is ItemClassModifier mod)
                        {
                            mod.PreInitCopyToModifier((ItemClassModifier)itemNew);
                        }
                        itemNew.Init();
                        ItemClass.itemNames.RemoveAt(ItemClass.itemNames.Count - 1);
                        ItemClass.list[itemNew.Id] = itemNew;
                    }
                    else
                    {
                        Log.Error($"Failed to replace ItemClass on item {item.GetItemName()} with {pair.Value}!");
                    }
                }
                dict_classtypes.Clear();
            };
        }

        internal static void CheckItem(ItemClass item)
        {
            if (!ModuleManagers.Inited)
            {
                return;
            }

            if (item != null && item.Properties.Values.TryGetValue("ItemClassModules", out string str_modules))
            {
                if (ModuleManagers.PatchType<ItemClassModuleProcessor>(item.GetType(), typeof(ItemClass), str_modules, out string typename))
                {
                    dict_classtypes[item.Name] = typename;
                }
            }
        }

        private static void PreInitCopyTo(this ItemClass from, ItemClass to)
        {
            to.Actions = from.Actions;
            foreach (var action in to.Actions)
            {
                if (action != null)
                {
                    action.item = to;
                }
            }
            to.SetName(from.Name);
            to.pId = from.pId;
            to.Properties = from.Properties;
            to.Effects = from.Effects;
            to.setLocalizedItemName(from.localizedName);
            to.Stacknumber = from.Stacknumber;
            to.SetCanHold(from.bCanHold);
            to.SetCanDrop(from.bCanDrop);
            to.MadeOfMaterial = from.MadeOfMaterial;
            to.MeshFile = from.MeshFile;
            to.StickyOffset = from.StickyOffset;
            to.StickyColliderRadius = from.StickyColliderRadius;
            to.StickyColliderUp = from.StickyColliderUp;
            to.StickyColliderLength = from.StickyColliderLength;
            to.StickyMaterial = from.StickyMaterial;
            to.ImageEffectOnActive = from.ImageEffectOnActive;
            to.Active = from.Active;
            to.IsSticky = from.IsSticky;
            to.DropMeshFile = from.DropMeshFile;
            to.HandMeshFile = from.HandMeshFile;
            to.HoldType = from.HoldType;
            to.RepairTools = from.RepairTools;
            to.RepairAmount = from.RepairAmount;
            to.RepairTime = from.RepairTime;
            to.MaxUseTimes = from.MaxUseTimes;
            to.MaxUseTimesBreaksAfter = from.MaxUseTimesBreaksAfter;
            to.EconomicValue = from.EconomicValue;
            to.Preview = from.Preview;
        }

        private static void PreInitCopyToModifier(this ItemClassModifier from, ItemClassModifier to)
        {
            to.CosmeticInstallChance = from.CosmeticInstallChance;
            to.PropertyOverrides = from.PropertyOverrides;
            to.InstallableTags = from.InstallableTags;
            to.DisallowedTags = from.DisallowedTags;
            to.ItemTags = from.ItemTags;
            to.Type = from.Type;
        }
    }
}

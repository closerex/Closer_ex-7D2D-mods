using System;
using System.Collections.Generic;

namespace KFCommonUtilityLib
{
    public static class ItemActionModuleManager
    {
        private static readonly Dictionary<string, List<(string typename, int indexOfAction)>> dict_replacement_mapping = new Dictionary<string, List<(string typename, int indexOfAction)>>();

        internal static void Init()
        {
            ModuleManagers.OnAssemblyCreated += static () => dict_replacement_mapping.Clear();
            ModuleManagers.OnAssemblyLoaded += static () =>
            {
                ModuleManagers.LogOut("Start replacing ItemAction");
                //replace item actions
                foreach (var pair in dict_replacement_mapping)
                {
                    ItemClass item = ItemClass.GetItemClass(pair.Key, true);
                    foreach ((string typename, int indexOfAction) in pair.Value)
                    {
                        if (ModuleManagers.TryFindType(typename, out Type itemActionType))
                        {
                            ItemAction itemActionPrev = item.Actions[indexOfAction];
                            item.Actions[indexOfAction] = (ItemAction)Activator.CreateInstance(itemActionType);
                            ItemAction newItemAction = item.Actions[indexOfAction];
                            ModuleManagers.LogOut($"Replace ItemAction {newItemAction.GetType().FullName} with {itemActionType.FullName} on item {item.GetItemName()}");
                            newItemAction.ActionIndex = indexOfAction;
                            newItemAction.item = item;
                            VersionPatchManager.CopyRequirements(itemActionPrev, newItemAction);
                            newItemAction.ReadFrom(itemActionPrev.Properties);
                        }
                        else
                        {
                            Log.Error($"Failed to replace ItemAction {indexOfAction} on item {item.GetItemName()} with {typename}!");
                        }
                    }
                }
                dict_replacement_mapping.Clear();
            };
        }

        internal static void CheckItem(ItemClass item)
        {
            if (!ModuleManagers.Inited)
            {
                return;
            }
            for (int i = 0; i < item.Actions.Length; i++)
            {
                ItemAction itemAction = item.Actions[i];
                if (itemAction != null && itemAction.Properties.Values.TryGetValue("ItemActionModules", out string str_modules))
                {
                    try
                    {
                        if (ModuleManagers.PatchType<ItemActionModuleProcessor>(itemAction.GetType(), typeof(ItemAction), str_modules, out string typename))
                        {
                            if (!dict_replacement_mapping.TryGetValue(item.Name, out var list))
                            {
                                list = new List<(string typename, int indexOfAction)>();
                                dict_replacement_mapping.Add(item.Name, list);
                            }
                            list.Add((typename, i));
                        }
                    }
                    catch(Exception e)
                    {
                        Log.Error($"Error parsing ItemActionModules for {item.Name} action{i}:\n{e}");
                        continue;
                    }
                }
            }
        }
    }
}
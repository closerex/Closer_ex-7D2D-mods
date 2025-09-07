using KFCommonUtilityLib.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;

namespace KFCommonUtilityLib
{
    //public static class AssemblyLocator
    //{
    //    private static Dictionary<string, Assembly> assemblies;

    //    public static void Init()
    //    {
    //        assemblies = new Dictionary<string, Assembly>();
    //        foreach (var assembly in ModManager.GetLoadedAssemblies())
    //        {
    //            assemblies.Add(assembly.FullName, assembly);
    //        }
    //        AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(CurrentDomain_AssemblyLoad);
    //        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
    //    }

    //    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    //    {
    //        assemblies.TryGetValue(args.Name, out Assembly assembly);
    //        if (assembly != null)
    //            Log.Out($"RESOLVING ASSEMBLY {assembly.FullName}");
    //        else
    //            Log.Error($"RESOLVING ASSEMBBLY {args.Name} FAILED!");
    //        return assembly;
    //    }

    //    private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
    //    {
    //        Assembly assembly = args.LoadedAssembly;
    //        assemblies[assembly.FullName] = assembly;
    //        Log.Out($"LOADING ASSEMBLY {assembly.FullName}");
    //    }
    //}

    public static class ItemActionModuleManager
    {
        private static readonly Dictionary<string, List<(string typename, int indexOfAction)>> dict_replacement_mapping = new Dictionary<string, List<(string typename, int indexOfAction)>>();

        internal static void Init()
        {
            ModuleManagers.OnAssemblyCreated += static () => dict_replacement_mapping.Clear();
            ModuleManagers.OnAssemblyLoaded += static () =>
            {
                //replace item actions
                foreach (var pair in dict_replacement_mapping)
                {
                    ItemClass item = ItemClass.GetItemClass(pair.Key, true);
                    foreach ((string typename, int indexOfAction) in pair.Value)
                        if (ModuleManagers.TryFindType(typename, out Type itemActionType))
                        {
                            //Log.Out($"Replace ItemAction {item.Actions[indexOfAction].GetType().FullName} with {itemActionType.FullName}");
                            ItemAction itemActionPrev = item.Actions[indexOfAction];
                            item.Actions[indexOfAction] = (ItemAction)Activator.CreateInstance(itemActionType);
                            item.Actions[indexOfAction].ActionIndex = indexOfAction;
                            item.Actions[indexOfAction].item = item;
                            item.Actions[indexOfAction].ExecutionRequirements = itemActionPrev.ExecutionRequirements;
                            item.Actions[indexOfAction].ReadFrom(itemActionPrev.Properties);
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
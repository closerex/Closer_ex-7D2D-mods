using KFCommonUtilityLib.Scripts.Attributes;
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

        public static void Init()
        {
            ModuleManagers.OnAssemblyCreated += (_) => dict_replacement_mapping.Clear();
            ModuleManagers.OnAssemblyLoaded += (_) =>
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
            for (int i = 0; i < item.Actions.Length; i++)
            {
                ItemAction itemAction = item.Actions[i];
                if (itemAction != null && itemAction.Properties.Values.TryGetValue("ItemActionModules", out string str_modules))
                {
                    string[] modules = str_modules.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    Type itemActionType = itemAction.GetType();
                    Type[] moduleTypes = modules.Select(s => ReflectionHelpers.GetTypeWithPrefix("ActionModule", s.Trim()))
                                                .Where(t => t.GetCustomAttribute<TypeTargetAttribute>().BaseType.IsAssignableFrom(itemActionType)).ToArray();
                    string typename = ModuleUtils.CreateTypeName(itemActionType, moduleTypes);
                    //Log.Out(typename);
                    if (!ModuleManagers.TryFindType(typename, out _) && !ModuleManagers.TryFindInCur(typename, out _))
                        _ = new ModuleManipulator(ModuleManagers.WorkingAssembly, new ItemActionModuleProcessor(), itemActionType, typeof(ItemAction), moduleTypes);
                    if (!dict_replacement_mapping.TryGetValue(item.Name, out var list))
                    {
                        list = new List<(string typename, int indexOfAction)>();
                        dict_replacement_mapping.Add(item.Name, list);
                    }
                    list.Add((typename, i));
                }
            }
        }
    }
}
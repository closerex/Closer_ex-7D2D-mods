using HarmonyLib;
using KFCommonUtilityLib.Harmony;
using KFCommonUtilityLib.Scripts.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using UniLinq;
using UnityEngine;
using UnityEngine.Scripting;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace KFCommonUtilityLib.Scripts.StaticManagers
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

    public interface IModuleContainerFor<out T> where T : class
    {
        T Instance { get; }
    }

    public class MethodPatchInfo
    {
        public readonly MethodDefinition Method;
        public Instruction PrefixBegin;
        public Instruction PostfixBegin;
        public Instruction PostfixEnd;

        public MethodPatchInfo(MethodDefinition mtddef, Instruction postfixEnd, Instruction prefixBegin)
        {
            Method = mtddef;
            PostfixEnd = postfixEnd;
            PrefixBegin = prefixBegin;
        }
    }

    public static class ItemActionModuleManager
    {
        private static readonly List<Assembly> list_created = new List<Assembly>();
        private static AssemblyDefinition workingAssembly = null;
        private static DefaultAssemblyResolver resolver;
        private static ModuleAttributes moduleAttributes;
        private static ModuleCharacteristics moduleCharacteristics;
        private static readonly Dictionary<string, List<(string typename, int indexOfAction)>> dict_replacement_mapping = new Dictionary<string, List<(string typename, int indexOfAction)>>();

        internal static void ClearOutputFolder()
        {
            Mod self = ModManager.GetMod("CommonUtilityLib");
            string path = Path.Combine(self.Path, "AssemblyOutput");
            if (Directory.Exists(path))
                Array.ForEach(Directory.GetFiles(path), File.Delete);
            else
                Directory.CreateDirectory(path);
        }

        internal static void InitNew()
        {
            dict_replacement_mapping.Clear();
            workingAssembly?.Dispose();
            if (resolver == null)
            {
                resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.Combine(Application.dataPath, "Managed"));

                foreach (var mod in ModManager.GetLoadedMods())
                {
                    resolver.AddSearchDirectory(mod.Path);
                }

                AssemblyDefinition assdef_main = AssemblyDefinition.ReadAssembly($"{Application.dataPath}/Managed/Assembly-CSharp.dll", new ReaderParameters() { AssemblyResolver = resolver });
                moduleAttributes = assdef_main.MainModule.Attributes;
                moduleCharacteristics = assdef_main.MainModule.Characteristics;
                Log.Out("Reading Attributes from assembly: " + assdef_main.FullName);
            }
            string assname = "ItemActionModule" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            workingAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(assname,
                                                                                           new Version(0, 0, 0, 0)),
                                                                                           assname + ".dll",
                                                                                           new ModuleParameters()
                                                                                           {
                                                                                               Kind = ModuleKind.Dll,
                                                                                               AssemblyResolver = resolver,
                                                                                               Architecture = TargetArchitecture.I386,
                                                                                               Runtime = TargetRuntime.Net_4_0,
                                                                                           });
            workingAssembly.MainModule.Attributes = moduleAttributes;
            workingAssembly.MainModule.Characteristics = moduleCharacteristics;
            //write security attributes so that calling non-public patch methods from this assembly is allowed
            Mono.Cecil.SecurityAttribute sattr_permission = new Mono.Cecil.SecurityAttribute(workingAssembly.MainModule.ImportReference(typeof(SecurityPermissionAttribute)));
            Mono.Cecil.CustomAttributeNamedArgument caarg_SkipVerification = new Mono.Cecil.CustomAttributeNamedArgument(nameof(SecurityPermissionAttribute.SkipVerification), new CustomAttributeArgument(workingAssembly.MainModule.TypeSystem.Boolean, true));
            sattr_permission.Properties.Add(caarg_SkipVerification);
            SecurityDeclaration sdec = new SecurityDeclaration(Mono.Cecil.SecurityAction.RequestMinimum);
            sdec.SecurityAttributes.Add(sattr_permission);
            workingAssembly.SecurityDeclarations.Add(sdec);
            Log.Out("======Init New======");
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
                    string typename = CreateTypeName(itemActionType, moduleTypes);
                    //Log.Out(typename);
                    if (!TryFindType(typename, out _) && !TryFindInCur(typename, out _))
                        PatchType(itemActionType, moduleTypes);
                    if (!dict_replacement_mapping.TryGetValue(item.Name, out var list))
                    {
                        list = new List<(string typename, int indexOfAction)>();
                        dict_replacement_mapping.Add(item.Name, list);
                    }
                    list.Add((typename, i));
                }
            }
        }

        internal static void FinishAndLoad()
        {
            //output assembly
            Log.Out("======Finish and Load======");
            Mod self = ModManager.GetMod("CommonUtilityLib");
            if (self == null)
            {
                Log.Warning("Failed to get mod!");
                self = ModManager.GetModForAssembly(typeof(ItemActionModuleManager).Assembly);
            }
            if (self != null && workingAssembly != null && workingAssembly.MainModule.Types.Count > 1)
            {
                Log.Out("Assembly is valid!");
                using (MemoryStream ms = new MemoryStream())
                {
                    try
                    {
                        workingAssembly.Write(ms);
                    }
                    catch (Exception)
                    {
                        new ConsoleCmdShutdown().Execute(new List<string>(), new CommandSenderInfo());
                    }
                    DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(self.Path, "AssemblyOutput"));
                    string filename = Path.Combine(dirInfo.FullName, workingAssembly.Name.Name + ".dll");
                    Log.Out("Output Assembly: " + filename);
                    using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        ms.WriteTo(fs);
                    }
                    Assembly newAssembly = Assembly.LoadFile(filename);
                    list_created.Add(newAssembly);
                }
            }

            //replace item actions
            if (list_created.Count > 0)
            {
                foreach (var pair in dict_replacement_mapping)
                {
                    ItemClass item = ItemClass.GetItemClass(pair.Key, true);
                    foreach ((string typename, int indexOfAction) in pair.Value)
                    {
                        if (TryFindType(typename, out Type itemActionType))
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
                }
            }

            //cleanup
            workingAssembly?.Dispose();
            workingAssembly = null;
            dict_replacement_mapping.Clear();
        }

        private static void PatchType(Type itemActionType, params Type[] moduleTypes)
        {
            if (workingAssembly == null)
                return;

            //Get assembly module
            ModuleDefinition module = workingAssembly.MainModule;

            TypeReference typeref_interface = module.ImportReference(typeof(IModuleContainerFor<>));
            //Prepare type info
            TypeTargetAttribute[] arr_attr_modules = moduleTypes.Select(t => t.GetCustomAttribute<TypeTargetAttribute>()).ToArray();
            TypeReference[] arr_typeref_modules = moduleTypes.Select(t => module.ImportReference(t)).ToArray();
            Type[] arr_type_data = arr_attr_modules.Select(a => a.DataType).ToArray();
            TypeReference[] arr_typeref_data = arr_type_data.Select(a => a != null ? module.ImportReference(a) : null).ToArray();

            //Find ItemActionData subtype
            MethodInfo mtdinf_create_data = null;
            {
                Type type_itemActionRoot = typeof(ItemAction);
                Type type_itemActionBase = itemActionType;
                while (typeof(ItemAction).IsAssignableFrom(type_itemActionBase))
                {
                    mtdinf_create_data = type_itemActionBase.GetMethod(nameof(ItemAction.CreateModifierData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtdinf_create_data != null)
                        break;
                    mtdinf_create_data = mtdinf_create_data.GetBaseDefinition();
                }
            }

            //Create new ItemAction
            TypeReference typeref_itemAction = module.ImportReference(itemActionType);
            TypeDefinition typedef_newAction = new TypeDefinition(null, CreateTypeName(itemActionType, moduleTypes), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed, typeref_itemAction);
            for (int i = 0; i < arr_typeref_modules.Length; i++)
            {


            }
            typedef_newAction.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(PreserveAttribute).GetConstructor(Array.Empty<Type>()))));
            module.Types.Add(typedef_newAction);

            //Create new ItemActionData
            //Find CreateModifierData
            MethodDefinition mtddef_create_data = module.ImportReference(mtdinf_create_data).Resolve();
            //ItemActionData subtype is the return type of CreateModifierData
            TypeReference typeref_actiondata = ((MethodReference)mtddef_create_data.Body.Instructions[mtddef_create_data.Body.Instructions.Count - 2].Operand).DeclaringType;
            //Get type by assembly qualified name since it might be from mod assembly
            Type type_itemActionData = Type.GetType(Assembly.CreateQualifiedName(typeref_actiondata.Module.Assembly.Name.Name, typeref_actiondata.FullName));
            TypeDefinition typedef_newActionData = new TypeDefinition(null, CreateTypeName(type_itemActionData, arr_type_data), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.NestedPublic | TypeAttributes.Sealed, module.ImportReference(typeref_actiondata));
            typedef_newActionData.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(PreserveAttribute).GetConstructor(Array.Empty<Type>()))));
            typedef_newAction.NestedTypes.Add(typedef_newActionData);

            //Create fields
            FieldDefinition[] arr_flddef_modules = new FieldDefinition[moduleTypes.Length];
            FieldDefinition[] arr_flddef_data = new FieldDefinition[moduleTypes.Length];
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                //Create ItemAction field
                Type type_module = moduleTypes[i];
                FieldDefinition flddef_module = new FieldDefinition(CreateFieldName(type_module), FieldAttributes.Public, arr_typeref_modules[i]);
                typedef_newAction.Fields.Add(flddef_module);
                arr_flddef_modules[i] = flddef_module;

                TypeReference typeref_module = arr_typeref_modules[i];
                MakeContainerFor(module, typeref_interface, typedef_newAction, type_module, flddef_module, typeref_module);

                //Create ItemActionData field
                if (arr_typeref_data[i] != null)
                {
                    TypeReference typeref_data = arr_typeref_data[i];
                    Type type_data = arr_type_data[i];
                    FieldDefinition flddef_data = new FieldDefinition(CreateFieldName(type_data), FieldAttributes.Public, typeref_data);
                    typedef_newActionData.Fields.Add(flddef_data);
                    arr_flddef_data[i] = flddef_data;

                    MakeContainerFor(module, typeref_interface, typedef_newActionData, type_data, flddef_data, typeref_data);
                }
            }

            //Create ItemAction constructor
            MethodDefinition mtddef_ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            var il = mtddef_ctor.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(itemActionType.GetConstructor(Array.Empty<Type>()))));
            il.Append(il.Create(OpCodes.Nop));
            for (int i = 0; i < arr_flddef_modules.Length; i++)
            {
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Newobj, module.ImportReference(moduleTypes[i].GetConstructor(Array.Empty<Type>()))));
                il.Append(il.Create(OpCodes.Stfld, arr_flddef_modules[i]));
                il.Append(il.Create(OpCodes.Nop));
            }
            il.Append(il.Create(OpCodes.Ret));
            typedef_newAction.Methods.Add(mtddef_ctor);

            //Create ItemActionData constructor
            MethodDefinition mtddef_ctor_data = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            mtddef_ctor_data.Parameters.Add(new ParameterDefinition("_inventoryData", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(ItemInventoryData))));
            mtddef_ctor_data.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Int32));
            FieldReference fldref_invdata_item = module.ImportReference(typeof(ItemInventoryData).GetField(nameof(ItemInventoryData.item)));
            FieldReference fldref_item_actions = module.ImportReference(typeof(ItemClass).GetField(nameof(ItemClass.Actions)));
            il = mtddef_ctor_data.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldarg_1));
            il.Append(il.Create(OpCodes.Ldarg_2));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(type_itemActionData.GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) }))));
            il.Append(il.Create(OpCodes.Nop));
            for (int i = 0; i < arr_flddef_data.Length; i++)
            {
                if (arr_type_data[i] == null)
                    continue;
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldarg_1));
                il.Append(il.Create(OpCodes.Ldarg_2));
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldfld, fldref_invdata_item);
                il.Emit(OpCodes.Ldfld, fldref_item_actions);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typedef_newAction);
                il.Emit(OpCodes.Ldfld, arr_flddef_modules[i]);
                il.Append(il.Create(OpCodes.Newobj, module.ImportReference(arr_type_data[i].GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int), moduleTypes[i] }))));
                il.Append(il.Create(OpCodes.Stfld, arr_flddef_data[i]));
                il.Append(il.Create(OpCodes.Nop));
            }
            il.Append(il.Create(OpCodes.Ret));
            typedef_newActionData.Methods.Add(mtddef_ctor_data);

            //Create ItemAction.CreateModifierData override
            MethodDefinition mtddef_create_modifier_data = new MethodDefinition(nameof(ItemAction.CreateModifierData), MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, module.ImportReference(typeof(ItemActionData)));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_invData", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(ItemInventoryData))));
            mtddef_create_modifier_data.Parameters.Add(new ParameterDefinition("_indexInEntityOfAction", Mono.Cecil.ParameterAttributes.None, module.TypeSystem.Int32));
            il = mtddef_create_modifier_data.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, mtddef_ctor_data);
            il.Emit(OpCodes.Ret);
            typedef_newAction.Methods.Add(mtddef_create_modifier_data);

            //<derived method name, method patch info>
            Dictionary<string, MethodPatchInfo> dict_overrides = new Dictionary<string, MethodPatchInfo>();
            //<derived method name, transpiler stub methods in inheritance order>
            Dictionary<string, List<(MethodDefinition mtddef_target, MethodReference mtdref_original, MethodReference mtdref_harmony, List<MethodInfo> list_mtdinf_patches)>> dict_transpilers = new Dictionary<string, List<(MethodDefinition mtddef_target, MethodReference mtdref_original, MethodReference mtdref_harmony, List<MethodInfo> list_mtdinf_patches)>>();
            //<derived method name, <module type name, local variable>>
            Dictionary<string, Dictionary<string, VariableDefinition>> dict_all_states = new Dictionary<string, Dictionary<string, VariableDefinition>>();

            //Get all transpilers and clone original methods
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Type moduleType = moduleTypes[i];
                const BindingFlags searchFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var mtd in moduleType.GetMethods(searchFlags))
                {
                    var attr = mtd.GetCustomAttribute<MethodTargetTranspilerAttribute>();
                    if (attr != null)
                    {
                        string id = attr.GetTargetMethodIdentifier();
                        if (!dict_transpilers.TryGetValue(id, out var list_transpilers))
                        {
                            list_transpilers = new List<(MethodDefinition mtddef_target, MethodReference mtdref_original, MethodReference mtdref_harmony, List<MethodInfo> list_mtdinf_patches)> { };
                            dict_transpilers[id] = list_transpilers;
                        }
                        int maxLevels = 0;
                        Type nextType = itemActionType;
                        while (attr.PreferredType.IsAssignableFrom(nextType))
                        {
                            maxLevels++;
                            if (list_transpilers.Count < maxLevels)
                            {
                                var mtdinfo_cur = AccessTools.Method(nextType, attr.TargetMethod, attr.Params);
                                var mtdinfo_harmony = ItemActionModulePatch.GetRealMethod(mtdinfo_cur, true);
                                //transpilers on undeclared methods are invalid
                                var mtdref_original = mtdinfo_cur.DeclaringType.Equals(nextType) ? module.ImportReference(mtdinfo_cur) : null;
                                var mtdref_harmony = mtdref_original != null ? module.ImportReference(mtdinfo_harmony) : null;
                                var mtddef_copy = mtdref_harmony?.Resolve()?.CloneToModuleAsStatic(module.ImportReference(mtdinfo_cur.DeclaringType), module) ?? null;
                                list_transpilers.Add((mtddef_copy, mtdref_original, mtdref_harmony, new List<MethodInfo>()));
                            }
                            nextType = nextType.BaseType;
                        }
                        list_transpilers[maxLevels - 1].list_mtdinf_patches.Add(mtd);
                    }
                }
            }

            //apply transpilers and replace method calls on base methods with patched ones
            Dictionary<string, MethodDefinition> dict_replacers = new Dictionary<string, MethodDefinition>();
            foreach (var pair in dict_transpilers)
            {
                //the top copy to call in the override method
                MethodDefinition mtddef_override_copy = null;
                MethodReference mtdref_override_base = null;
                for (int i = pair.Value.Count - 1; i >= 0; i--)
                {
                    var mtddef_target = pair.Value[i].mtddef_target;
                    var mtdref_original = pair.Value[i].mtdref_harmony;
                    if (mtddef_target != null)
                    {
                        foreach (var patch in pair.Value[i].list_mtdinf_patches)
                        {
                            mtddef_target.Body.SimplifyMacros();
                            patch.Invoke(null, new object[] { mtddef_target.Body, module });
                            mtddef_target.Body.OptimizeMacros();
                        }

                        if (i < pair.Value.Count - 1)
                        {
                            //find first available base method
                            MethodDefinition mtddef_base_target = null;
                            MethodReference mtdref_base_original = null;
                            for (int j = i + 1; j < pair.Value.Count; j++)
                            {
                                mtddef_base_target = pair.Value[j].mtddef_target;
                                mtdref_base_original = pair.Value[j].mtdref_original;
                                if (mtddef_base_target != null)
                                {
                                    break;
                                }
                            }
                            //replace calls to the base
                            if (mtddef_base_target != null)
                            {
                                foreach (var ins in mtddef_target.Body.Instructions)
                                {
                                    if (ins.OpCode == OpCodes.Call && ((MethodReference)ins.Operand).FullName.Equals(mtdref_base_original.FullName))
                                    {
                                        Log.Out($"replacing call to {mtdref_base_original.FullName} to {mtddef_base_target.FullName}");
                                        ins.Operand = mtddef_base_target;
                                    }
                                }
                            }
                        }
                        //the iteration is reversed so make sure we grab the latest method
                        if (mtddef_target != null)
                        {
                            mtddef_override_copy = mtddef_target;
                            mtdref_override_base = mtdref_original;
                        }
                        //add patched copy to the class
                        typedef_newAction.Methods.Add(mtddef_target);
                    }
                }
                //create the method override that calls the patched copy
                GetOrCreateOverride(dict_overrides, pair.Key, mtdref_override_base, module, mtddef_override_copy);
            }

            //Apply Postfixes first so that Prefixes can jump to the right instruction
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPostfixAttribute>(itemActionType, moduleType, module);
                string moduleID = CreateFieldName(moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodDefinition mtddef_root = module.ImportReference(pair.Value.mtdinf_base.GetBaseDefinition()).Resolve();
                    MethodDefinition mtddef_target = module.ImportReference(pair.Value.mtdinf_target).Resolve();
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, pair.Value.mtdref_base, module);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;

                    if (!dict_all_states.TryGetValue(pair.Key, out var dict_states))
                    {
                        dict_states = new Dictionary<string, VariableDefinition>();
                        dict_all_states.Add(pair.Key, dict_states);
                    }
                    var list_inst_pars = MatchArguments(mtddef_root, mtdpinf_derived, mtddef_target, arr_flddef_modules[i], arr_flddef_data[i], module, itemActionType, typedef_newActionData, true, dict_states, moduleID);
                    //insert invocation
                    il = mtddef_derived.Body.GetILProcessor();
                    foreach (var ins in list_inst_pars)
                    {
                        il.InsertBefore(mtdpinf_derived.PostfixEnd, ins);
                    }
                    il.InsertBefore(mtdpinf_derived.PostfixEnd, il.Create(OpCodes.Call, module.ImportReference(mtddef_target)));
                    if (mtdpinf_derived.PostfixBegin == null)
                        mtdpinf_derived.PostfixBegin = list_inst_pars[0];
                }
            }

            //Apply Prefixes
            for (int i = moduleTypes.Length - 1; i >= 0; i--)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPrefixAttribute>(itemActionType, moduleType, module);
                string moduleID = CreateFieldName(moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodDefinition mtddef_root = module.ImportReference(pair.Value.mtdinf_base.GetBaseDefinition()).Resolve();
                    MethodDefinition mtddef_target = module.ImportReference(pair.Value.mtdinf_target).Resolve();
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, pair.Value.mtdref_base, module);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;
                    dict_all_states.TryGetValue(pair.Key, out var dict_states);
                    var list_inst_pars = MatchArguments(mtddef_root, mtdpinf_derived, mtddef_target, arr_flddef_modules[i], arr_flddef_data[i], module, itemActionType, typedef_newActionData, false, dict_states, moduleID);
                    //insert invocation
                    il = mtdpinf_derived.Method.Body.GetILProcessor();
                    Instruction ins_insert = mtdpinf_derived.PrefixBegin;
                    foreach (var ins in list_inst_pars)
                    {
                        il.InsertBefore(ins_insert, ins);
                    }
                    il.InsertBefore(ins_insert, il.Create(OpCodes.Call, module.ImportReference(mtddef_target)));
                    il.InsertBefore(ins_insert, il.Create(OpCodes.Brfalse_S, mtdpinf_derived.PostfixBegin ?? mtdpinf_derived.PostfixEnd));
                }
            }

            foreach (var pair in dict_all_states)
            {
                var dict_states = pair.Value;
                if (dict_states.Count > 0)
                {
                    Log.Error($"__state variable count does not match in prefixes and postfixes for {pair.Key}! check following modules:\n" + string.Join("\n", dict_states.Keys));
                    throw new Exception();
                }
            }

            //Add all overrides to new type
            foreach (var mtd in dict_overrides.Values)
            {
                typedef_newAction.Methods.Add(mtd.Method);

                //Log.Out($"Add method override to new action: {mtd.Method.Name}");
            }
        }

        private static void MakeContainerFor(ModuleDefinition module, TypeReference typeref_interface, TypeDefinition typedef_container, Type type_module, FieldDefinition flddef_module, TypeReference typeref_module)
        {
            typedef_container.Interfaces.Add(new InterfaceImplementation(typeref_interface.MakeGenericInstanceType(typeref_module)));
            PropertyDefinition propdef_instance = new PropertyDefinition("Instance", Mono.Cecil.PropertyAttributes.None, typeref_module);
            MethodDefinition mtddef_instance_getter = new MethodDefinition("get_Instance", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, typeref_module);
            mtddef_instance_getter.Overrides.Add(module.ImportReference(AccessTools.Method(typeof(IModuleContainerFor<>).MakeGenericType(type_module), "get_Instance")));
            typedef_container.Methods.Add(mtddef_instance_getter);
            mtddef_instance_getter.Body = new Mono.Cecil.Cil.MethodBody(mtddef_instance_getter);
            var generator = mtddef_instance_getter.Body.GetILProcessor();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, flddef_module);
            generator.Emit(OpCodes.Ret);
            propdef_instance.GetMethod = mtddef_instance_getter;
            typedef_container.Properties.Add(propdef_instance);
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="itemActionType"></param>
        /// <param name="moduleType"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        private static Dictionary<string, MethodOverrideInfo> GetMethodOverrideTargets<T>(Type itemActionType, Type moduleType, ModuleDefinition module) where T : Attribute, IMethodTarget
        {
            Dictionary<string, MethodOverrideInfo> dict_overrides = new Dictionary<string, MethodOverrideInfo>();
            const BindingFlags searchFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var mtd in moduleType.GetMethods(searchFlags))
            {
                IMethodTarget attr = mtd.GetCustomAttribute<T>();
                if (attr != null && (attr.PreferredType == null || attr.PreferredType.IsAssignableFrom(itemActionType)))
                {
                    string id = attr.GetTargetMethodIdentifier();
                    MethodInfo mtdinf_base = AccessTools.Method(itemActionType, attr.TargetMethod, attr.Params);
                    if (mtdinf_base == null || !mtdinf_base.IsVirtual || mtdinf_base.IsFinal)
                    {
                        Log.Error($"Method not found: {attr.TargetMethod}");
                        continue;
                    }

                    MethodReference mtdref_base = module.ImportReference(mtdinf_base);
                    //Find preferred patch
                    if (dict_overrides.TryGetValue(id, out var pair))
                    {
                        if (attr.PreferredType == null)
                            continue;
                        //cur action type is sub or same class of cur preferred type
                        //cur preferred type is sub class of previous preferred type
                        //means cur preferred type is closer to the action type in inheritance hierachy than the previous one
                        if (attr.PreferredType.IsAssignableFrom(itemActionType) && (pair.prefType == null || attr.PreferredType.IsSubclassOf(pair.prefType)))
                        {
                            dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, mtdref_base, attr.PreferredType);
                        }
                    }
                    else
                    {
                        dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, mtdref_base, attr.PreferredType);
                    }
                    //Log.Out($"Add method override: {id} for {mtdref_base.FullName}/{mtdinf_base.Name}, action type: {itemActionType.Name}");
                }
                else
                {
                    //Log.Out($"No override target found or preferred type not match on {mtd.Name}");
                }
            }
            return dict_overrides;
        }

        /// <summary>
        /// Get or create override MethodDefinition of mtdref_base.
        /// </summary>
        /// <param name="dict_overrides"></param>
        /// <param name="id"></param>
        /// <param name="mtdref_base"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        private static MethodPatchInfo GetOrCreateOverride(Dictionary<string, MethodPatchInfo> dict_overrides, string id, MethodReference mtdref_base, ModuleDefinition module, MethodDefinition mtddef_base_override = null)
        {
            //if (mtddef_base.FullName == "CreateModifierData")
            //    throw new MethodAccessException($"YOU SHOULD NOT MANUALLY MODIFY CreateModifierData!");
            if (dict_overrides.TryGetValue(id, out var mtdpinf_derived))
            {
                return mtdpinf_derived;
            }
            //when overriding, retain attributes of base but make sure to remove the 'new' keyword which presents if you are overriding the root method
            MethodDefinition mtddef_derived = new MethodDefinition(mtdref_base.Name, (mtdref_base.Resolve().Attributes | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot) & ~MethodAttributes.NewSlot, module.ImportReference(mtdref_base.ReturnType));

            //Log.Out($"Create method override: {id} for {mtdref_base.FullName}");
            foreach (var par in mtdref_base.Parameters)
            {
                ParameterDefinition pardef = new ParameterDefinition(par.Name, par.Attributes, module.ImportReference(par.ParameterType));
                if (par.HasConstant)
                    pardef.Constant = par.Constant;
                mtddef_derived.Parameters.Add(pardef);
            }
            mtddef_derived.Body.Variables.Clear();
            mtddef_derived.Body.InitLocals = true;
            mtddef_derived.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Boolean));
            bool hasReturnVal = mtddef_derived.ReturnType.MetadataType != MetadataType.Void;
            if (hasReturnVal)
            {
                mtddef_derived.Body.Variables.Add(new VariableDefinition(module.ImportReference(mtdref_base.ReturnType)));
            }
            var il = mtddef_derived.Body.GetILProcessor();
            if (hasReturnVal)
            {
                il.Emit(OpCodes.Ldloca_S, mtddef_derived.Body.Variables[1]);
                il.Emit(OpCodes.Initobj, module.ImportReference(mtddef_derived.ReturnType));
            }
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc_S, mtddef_derived.Body.Variables[0]);
            Instruction prefixBegin = il.Create(OpCodes.Ldc_I4_1);
            il.Append(prefixBegin);
            il.Emit(OpCodes.Stloc_S, mtddef_derived.Body.Variables[0]);
            il.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < mtddef_derived.Parameters.Count; i++)
            {
                var par = mtddef_derived.Parameters[i];
                il.Emit(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, par);
            }
            il.Emit(OpCodes.Call, mtddef_base_override ?? module.ImportReference(mtdref_base));
            if (hasReturnVal)
            {
                il.Emit(OpCodes.Stloc_S, mtddef_derived.Body.Variables[1]);
                il.Emit(OpCodes.Ldloc_S, mtddef_derived.Body.Variables[1]);
            }
            il.Emit(OpCodes.Ret);
            mtdpinf_derived = new MethodPatchInfo(mtddef_derived, mtddef_derived.Body.Instructions[mtddef_derived.Body.Instructions.Count - (hasReturnVal ? 2 : 1)], prefixBegin);
            dict_overrides.Add(id, mtdpinf_derived);
            return mtdpinf_derived;
        }

        /// <summary>
        /// Create a List<Instruction> that loads all arguments required to call the method onto stack.
        /// </summary>
        /// <param name="mtddef_derived">The override method.</param>
        /// <param name="mtddef_target">The patch method to be called.</param>
        /// <param name="flddef_module">The injected module field.</param>
        /// <param name="flddef_data">The injected data field.</param>
        /// <param name="module">The assembly's main module.</param>
        /// <param name="itemActionType">The base ItemAction type.</param>
        /// <returns></returns>
        /// <exception cref="MissingFieldException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        private static List<Instruction> MatchArguments(MethodDefinition mtddef_root, MethodPatchInfo mtdpinf_derived, MethodDefinition mtddef_target, FieldDefinition flddef_module, FieldDefinition flddef_data, ModuleDefinition module, Type itemActionType, TypeDefinition typedef_newactiondata, bool isPostfix, Dictionary<string, VariableDefinition> dict_states, string moduleID)
        {
            var mtddef_derived = mtdpinf_derived.Method;
            var il = mtddef_derived.Body.GetILProcessor();
            //Match parameters
            List<Instruction> list_inst_pars = new List<Instruction>();
            list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
            list_inst_pars.Add(il.Create(OpCodes.Ldfld, flddef_module));
            foreach (var par in mtddef_target.Parameters)
            {
                if (par.Name.StartsWith("___"))
                {
                    //___ means non public fields
                    string str_fldname = par.Name.Substring(3);
                    FieldDefinition flddef_target = module.ImportReference(itemActionType.GetField(str_fldname, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)).Resolve();
                    if (flddef_target == null)
                        throw new MissingFieldException($"Field with name \"{str_fldname}\" not found! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    if (flddef_target.IsStatic)
                    {
                        list_inst_pars.Add(il.Create((par.ParameterType.IsByReference) ? OpCodes.Ldsflda : OpCodes.Ldsfld, module.ImportReference(flddef_target)));
                    }
                    else
                    {
                        list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
                        list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, module.ImportReference(flddef_target)));
                    }
                }
                else if (!MatchSpecialParameters(par, flddef_data, mtddef_target, mtdpinf_derived, typedef_newactiondata, list_inst_pars, il, module, isPostfix, dict_states, moduleID))
                {
                    //match param by name
                    int index = -1;
                    for (int j = 0; j < mtddef_root.Parameters.Count; j++)
                    {
                        if (mtddef_root.Parameters[j].Name == par.Name)
                        {
                            index = mtddef_root.Parameters[j].Index;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"Parameter \"{par.Name}\" not found! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    try
                    {
                        //Log.Out($"Match Parameter {par.Name} to {mtddef_derived.Parameters[index].Name}/{mtddef_root.Parameters[index].Name} index: {index}");

                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Log.Error($"index {index} parameter {par.Name}" +
                                  $"root pars: {{{string.Join(",", mtddef_root.Parameters.Select(p => p.Name + "/" + p.Index).ToArray())}}}" +
                                  $"derived pars: {{{string.Join(",", mtddef_derived.Parameters.Select(p => p.Name + "/" + p.Index).ToArray())}}}");
                        throw e;
                    }
                    if (!mtddef_derived.Parameters[index].ParameterType.IsByReference)
                    {
                        list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, mtddef_derived.Parameters[index]));
                    }
                    else
                    {
                        list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, mtddef_derived.Parameters[index]));
                        if (!par.ParameterType.IsByReference)
                        {
                            list_inst_pars.Add(il.Create(OpCodes.Ldind_Ref));
                        }
                    }
                }
            }
            return list_inst_pars;
        }

        private static bool MatchSpecialParameters(ParameterDefinition par, FieldDefinition flddef_data, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, TypeDefinition typedef_newactiondata, List<Instruction> list_inst_pars, ILProcessor il, ModuleDefinition module, bool isPostfix, Dictionary<string, VariableDefinition> dict_states, string moduleID)
        {
            MethodDefinition mtddef_derived = mtdpinf_derived.Method;
            switch (par.Name)
            {
                //load injected data instance
                case "__customData":
                    if (flddef_data == null)
                        throw new ArgumentNullException($"No Injected ItemActionData in {mtddef_target.DeclaringType.FullName}!");
                    int index = -1;
                    for (int j = 0; j < mtddef_derived.Parameters.Count; j++)
                    {
                        if (mtddef_derived.Parameters[j].ParameterType.Name == "ItemActionData")
                        {
                            index = j;
                            break;
                        }
                    }
                    if (index < 0)
                        throw new ArgumentException($"ItemActionData is not present in target method! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, mtddef_derived.Parameters[index]));
                    list_inst_pars.Add(il.Create(OpCodes.Castclass, typedef_newactiondata));
                    list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_data));
                    break;
                //load ItemAction instance
                case "__instance":
                    list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
                    break;
                //load return value
                case "__result":
                    list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldloca_S : OpCodes.Ldloc_S, mtddef_derived.Body.Variables[1]));
                    break;
                //for postfix only, indicates whether original method is executed
                case "__runOriginal":
                    if (par.ParameterType.IsByReference)
                        throw new ArgumentException($"__runOriginal is readonly! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    list_inst_pars.Add(il.Create(OpCodes.Ldloc_S, mtddef_derived.Body.Variables[0]));
                    break;
                case "__state":
                    if (dict_states == null)
                    {
                        throw new ArgumentNullException($"__state is found in prefix but no matching postfix exists! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    }
                    if (!isPostfix && !dict_states.TryGetValue(moduleID, out var vardef))
                    {
                        throw new KeyNotFoundException($"__state is found in prefix but not found in corresponding postfix! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    }
                    if (par.IsOut && isPostfix)
                    {
                        throw new ArgumentException($"__state is marked as out parameter in postfix! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    }
                    if (!par.IsOut && !isPostfix)
                    {
                        throw new ArgumentException($"__state is not marked as out in prefix! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    }
                    if (isPostfix)
                    {
                        vardef = new VariableDefinition(module.ImportReference(par.ParameterType));
                        mtddef_derived.Body.Variables.Add(vardef);
                        dict_states.Add(moduleID, vardef);
                        var ins = mtddef_derived.Body.Instructions[0];
                        il.InsertBefore(ins, il.Create(OpCodes.Ldloca_S, vardef));
                        il.InsertBefore(ins, il.Create(OpCodes.Initobj, module.ImportReference(par.ParameterType)));
                        list_inst_pars.Add(il.Create(OpCodes.Ldloc_S, vardef));
                    }
                    else
                    {
                        vardef = dict_states[moduleID];
                        dict_states.Remove(moduleID);
                        list_inst_pars.Add(il.Create(OpCodes.Ldloca_S, vardef));
                    }
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if type is already generated in previous assemblies.
        /// </summary>
        /// <param name="name">Full type name.</param>
        /// <param name="type">The retrieved type, null if not found.</param>
        /// <returns>true if found.</returns>
        private static bool TryFindType(string name, out Type type)
        {
            type = null;
            foreach (var assembly in list_created)
            {
                type = assembly.GetType(name, false);
                if (type != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if type is already generated in current working assembly definition.
        /// </summary>
        /// <param name="name">Full type name.</param>
        /// <param name="typedef">The retrieved type definition, null if not found.</param>
        /// <returns>true if found.</returns>
        private static bool TryFindInCur(string name, out TypeDefinition typedef)
        {
            typedef = workingAssembly?.MainModule.GetType(name);
            return typedef != null;
        }

        public static string CreateFieldName(Type moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Assembly.GetName().Name).ReplaceInvalidChar();
        }

        public static string CreateFieldName(TypeReference moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Module.Assembly.Name.Name).ReplaceInvalidChar();
        }

        public static string CreateTypeName(Type itemActionType, params Type[] moduleTypes)
        {
            string typeName = itemActionType.FullName + "_" + itemActionType.Assembly.GetName().Name;
            foreach (Type type in moduleTypes)
            {
                if (type != null)
                    typeName += "__" + type.FullName + "_" + type.Assembly.GetName().Name;
            }
            typeName = typeName.ReplaceInvalidChar();
            return typeName;
        }

        public static string CreateTypeName(TypeReference itemActionType, params TypeReference[] moduleTypes)
        {
            string typeName = itemActionType.FullName + "_" + itemActionType.Module.Assembly.Name.Name;
            foreach (TypeReference type in moduleTypes)
            {
                if (type != null)
                    typeName += "__" + type.FullName + "_" + type.Module.Assembly.Name.Name;
            }
            typeName = typeName.ReplaceInvalidChar();
            return typeName;
        }

        private static string ReplaceInvalidChar(this string self)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < self.Length; i++)
            {
                char c = self[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    internal struct MethodOverrideInfo
    {
        public MethodInfo mtdinf_target;
        public MethodInfo mtdinf_base;
        public MethodReference mtdref_base;
        public Type prefType;

        public MethodOverrideInfo(MethodInfo mtdinf_target, MethodInfo mtdinf_base, MethodReference mtddef_base, Type prefType)
        {
            this.mtdinf_target = mtdinf_target;
            this.mtdinf_base = mtdinf_base;
            this.mtdref_base = mtddef_base;
            this.prefType = prefType;
        }
    }
}
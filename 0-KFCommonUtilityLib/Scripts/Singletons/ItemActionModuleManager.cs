using KFCommonUtilityLib.Scripts.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MusicUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public class MethodPatchInfo
    {
        public readonly MethodDefinition Method;
        public Instruction PostfixBegin;
        public Instruction PostfixEnd;

        public MethodPatchInfo(MethodDefinition mtddef, Instruction postfixEnd)
        {
            Method = mtddef;
            PostfixEnd = postfixEnd;
        }
    }

    public static class ItemActionModuleManager
    {
        private static readonly List<Assembly> list_created = new List<Assembly>();
        private static AssemblyDefinition workingAssembly = null;

        internal static void InitNew()
        {
            workingAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("ItemActionModule" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), new Version(0, 0, 0, 0)), "Main", ModuleKind.Dll);
        }

        internal static void FinishAndLoad()
        {
            if (workingAssembly != null && workingAssembly.MainModule.Types.Count > 0)
            {
                Mod self = ModManager.GetMod("CommonUtilityLib");
                if (self != null)
                {
                    DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(self.Path, "AssemblyOutput"));
                    string filename = Path.Combine(dirInfo.FullName, workingAssembly.Name.Name);
                    workingAssembly.Write(filename);
                    Assembly newAssembly = Assembly.LoadFrom(filename);
                    list_created.Add(newAssembly);
                }
                //replace item actions

            }
            //cleanup
            workingAssembly?.Dispose();
            workingAssembly = null;
        }

        private static void PatchType(Type itemActionType, params Type[] moduleTypes)
        {
            if (workingAssembly == null)
                return;

            //Get assembly module
            ModuleDefinition module = workingAssembly.MainModule;

            //Prepare type info
            TypeTargetAttribute[] arr_attr_modules = moduleTypes.Select(t => t.GetCustomAttribute<TypeTargetAttribute>()).ToArray();
            TypeReference[] arr_typeref_modules = moduleTypes.Select(t => module.ImportReference(t)).ToArray();
            Type[] arr_type_data = arr_attr_modules.Select(a => a.DataType).ToArray();
            TypeReference[] arr_typeref_data = arr_type_data.Select(a => a != null ? module.ImportReference(a) : null).ToArray();

            //Find ItemActionData subtype
            MethodInfo mtd_create_data = null;
            {
                Type type_itemActionRoot = typeof(ItemAction);
                Type type_itemActionBase = itemActionType;
                while (typeof(ItemAction).IsAssignableFrom(type_itemActionBase))
                {
                    mtd_create_data = type_itemActionBase.GetMethod(nameof(ItemAction.CreateModifierData), BindingFlags.Public | BindingFlags.Instance);
                    if (mtd_create_data != null)
                        break;
                }
            }

            //Create new ItemAction
            TypeReference typeref_itemAction = module.ImportReference(itemActionType);
            TypeDefinition typedef_newAction = new TypeDefinition(null, CreateTypeName(itemActionType, moduleTypes), TypeAttributes.Public, typeref_itemAction);
            module.Types.Add(typedef_newAction);

            //Create new ItemActionData
            //Find CreateModifierData
            MethodDefinition mtddef_create_data = module.ImportReference(mtd_create_data).Resolve();
            //ItemActionData subtype is the return type of CreateModifierData
            TypeReference typeref_data = ((MethodReference)mtddef_create_data.Body.Instructions[mtddef_create_data.Body.Instructions.Count - 2].Operand).DeclaringType;
            //Get type by assembly qualified name since it might be from mod assembly
            Type type_itemActionData = Type.GetType(Assembly.CreateQualifiedName(typeref_data.Module.Assembly.Name.Name, typeref_data.FullName));
            TypeDefinition typedef_newactiondata = new TypeDefinition(null, CreateTypeName(typeref_data, arr_typeref_data), TypeAttributes.Public, typeref_data);
            module.Types.Add(typedef_newactiondata);

            //Create fields
            FieldDefinition[] arr_flddef_modules = new FieldDefinition[moduleTypes.Length];
            FieldDefinition[] arr_flddef_data = new FieldDefinition[moduleTypes.Length];
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                //Create ItemAction field
                FieldDefinition flddef_module = new FieldDefinition(CreateFieldName(moduleTypes[i]), FieldAttributes.Public, arr_typeref_modules[i]);
                typedef_newAction.Fields.Add(flddef_module);
                arr_flddef_modules[i] = flddef_module;

                //Create ItemActionData field
                if (arr_typeref_data[i] != null)
                {
                    FieldDefinition flddef_data = new FieldDefinition(CreateFieldName(arr_attr_modules[i].DataType), FieldAttributes.Public, arr_typeref_data[i]);
                    typedef_newactiondata.Fields.Add(flddef_data);
                    arr_flddef_data[i] = flddef_data;
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
            il = mtddef_ctor_data.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(type_itemActionData.GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) }))));
            il.Append(il.Create(OpCodes.Nop));
            for (int i = 0; i < arr_flddef_data.Length; i++)
            {
                if (arr_type_data[i] == null)
                    continue;
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Newobj, module.ImportReference(arr_type_data[i].GetConstructor(new Type[] { typeof(ItemInventoryData), typeof(int) }))));
                il.Append(il.Create(OpCodes.Stfld, arr_flddef_data[i]));
                il.Append(il.Create(OpCodes.Nop));
            }
            il.Append(il.Create(OpCodes.Ret));
            typedef_newAction.Methods.Add(mtddef_ctor_data);

            Dictionary<string, MethodPatchInfo> dict_overrides = new Dictionary<string, MethodPatchInfo>();

            //Apply Postfixes
            for (int i = 0; i < moduleTypes.Length - 1; i++)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, (MethodInfo mtdInfo, Type prefType)> dict_targets = GetMethodOverrideTargets<MethodTargetPostfixAttribute>(itemActionType, moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodReference mtdref_target = module.ImportReference(pair.Value.mtdInfo);
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, mtdref_target, module);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;
                    var list_inst_pars = MatchArguments(mtddef_derived, mtdref_target, arr_flddef_modules[i], arr_flddef_data[i], module, itemActionType, pair.Value.mtdInfo);
                    //insert invocation
                }
            }


            //Apply Prefixes
            for (int i = 0; i < moduleTypes.Length - 1; i++)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, (MethodInfo mtdInfo, Type prefType)> dict_targets = GetMethodOverrideTargets<MethodTargetPrefixAttribute>(itemActionType, moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodReference mtdref_target = module.ImportReference(pair.Value.mtdInfo);
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, mtdref_target, module);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;
                    var list_inst_pars = MatchArguments(mtddef_derived, mtdref_target, arr_flddef_modules[i], arr_flddef_data[i], module, itemActionType, pair.Value.mtdInfo);
                    //insert invocation
                }
            }
        }

        private static Dictionary<string, (MethodInfo mtdInfo, Type prefType)> GetMethodOverrideTargets<T>(Type itemActionType, Type moduleType) where T: Attribute, IMethodTarget
        {
            Dictionary<string, (MethodInfo mtdInfo, Type prefType)> dict_overrides = new Dictionary<string, (MethodInfo mtdInfo, Type prefType)>();
            foreach (var mtd in moduleType.GetMethods())
            {
                IMethodTarget attr = mtd.GetCustomAttribute<T>();
                if (attr != null && (attr.PreferredType == null || itemActionType.IsAssignableFrom(attr.PreferredType)))
                {
                    string id = attr.GetTargetMethodIdentifier();
                    MethodInfo mtdTarget = itemActionType.GetMethod(attr.TargetMethod, attr.Params);
                    if (mtdTarget == null || !mtdTarget.IsVirtual || mtdTarget.IsFinal)
                        continue;

                    if (dict_overrides.TryGetValue(id, out var pair))
                    {
                        if (attr.PreferredType == null)
                            continue;
                        if (itemActionType.IsAssignableFrom(attr.PreferredType) && (pair.prefType == null || attr.PreferredType.IsSubclassOf(pair.prefType)))
                        {
                            dict_overrides[id] = (mtd, attr.PreferredType);
                        }
                    }
                    else
                    {
                        dict_overrides[id] = (mtd, attr.PreferredType);
                    }
                }
            }
            return dict_overrides;
        }

        private static MethodPatchInfo GetOrCreateOverride(Dictionary<string, MethodPatchInfo> dict_overrides, string id, MethodReference mtdref_target, ModuleDefinition module)
        {
            if (dict_overrides.TryGetValue(id, out var mtdpinf_derived))
            {
                return mtdpinf_derived;
            }
            MethodDefinition mtddef_derived = new MethodDefinition(mtdref_target.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, mtdref_target.ReturnType);
            foreach (var par in mtdref_target.Parameters)
            {
                mtddef_derived.Parameters.Add(new ParameterDefinition(par.Name, par.Attributes, par.ParameterType));
            }
            mtddef_derived.Body.Variables.Clear();
            mtddef_derived.Body.InitLocals = true;
            mtddef_derived.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Boolean));
            bool hasReturnVal = mtddef_derived.ReturnType != module.TypeSystem.Void;
            if (hasReturnVal)
            {
                mtddef_derived.Body.Variables.Add(new VariableDefinition(mtdref_target.ReturnType));
            }
            var il = mtddef_derived.Body.GetILProcessor();
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stloc_S, mtddef_derived.Body.Variables[0]);
            il.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < mtddef_derived.Parameters.Count; i++)
            {
                var par = mtddef_derived.Parameters[i];
                il.Emit(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S , i + 1);
            }
            il.Emit(OpCodes.Call, mtdref_target);
            if (hasReturnVal)
            {
                il.Emit(OpCodes.Stloc_S, mtddef_derived.Body.Variables[1]);
                il.Emit(OpCodes.Ldloc_S, mtddef_derived.Body.Variables[1]);
            }
            il.Emit(OpCodes.Ret);
            mtdpinf_derived = new MethodPatchInfo(mtddef_derived, mtddef_derived.Body.Instructions[mtddef_derived.Body.Instructions.Count - (hasReturnVal ? 2 : 1)]);
            dict_overrides.Add(id, mtdpinf_derived);
            return mtdpinf_derived;
        }

        private static List<Instruction> MatchArguments(MethodDefinition mtddef_derived, MethodReference mtdref_target, FieldDefinition flddef_module, FieldDefinition flddef_data, ModuleDefinition module, Type itemActionType, MethodInfo mtdInfo)
        {
            var il = mtddef_derived.Body.GetILProcessor();
            //Match parameters
            List<Instruction> list_inst_pars = new List<Instruction>();
            foreach (var par in mtdref_target.Parameters)
            {
                if (par.Name.StartsWith("__"))
                {
                    if (par.Name.StartsWith("___"))
                    {
                        //___ means non public fields
                        string str_fldname = par.Name.Substring(3);
                        FieldDefinition flddef_target = module.ImportReference(itemActionType.GetField(str_fldname, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)).Resolve();
                        if (flddef_target == null)
                            throw new MissingFieldException($"Field with name \"{str_fldname}\" not found! Patch method: {mtdInfo.ReflectedType.FullName}.{mtdref_target.Name}");
                        if (flddef_target.IsStatic)
                        {
                            list_inst_pars.Add(il.Create((par.ParameterType.IsByReference) ? OpCodes.Ldsflda : OpCodes.Ldsfld, flddef_target));
                        }
                        else
                        {
                            list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
                            list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_target));
                        }
                    }
                    else
                    {
                        //__ means special data
                        switch (par.Name)
                        {
                            //load module instance
                            case "__self":
                                list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
                                list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, flddef_module));
                                break;
                            //load injected data instance
                            case "__data":
                                if (flddef_data == null)
                                    throw new ArgumentNullException($"No Injected ItemActionData in {mtdInfo.ReflectedType.FullName}!");
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
                                    throw new ArgumentException($"ItemActionData is not present in target method! Patch method: {mtdInfo.ReflectedType.FullName}.{mtdref_target.Name}");
                                list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, index + 1));
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
                            case "__runOriginal":
                                if (par.ParameterType.IsByReference)
                                    throw new ArgumentException($"__runOriginal is readonly! Patch method: {mtdInfo.ReflectedType.FullName}.{mtdref_target.Name}");
                                list_inst_pars.Add(il.Create(OpCodes.Ldloc_S, mtddef_derived.Body.Variables[0]));
                                break;
                            default:
                                throw new ArgumentException($"Invalid Parameter Name in {mtdInfo.ReflectedType.FullName}.{mtdref_target.Name}: {par.Name}");
                        }
                    }
                }
                else
                {
                    //match param by name
                    int index = -1;
                    for (int j = 0; j < mtddef_derived.Parameters.Count; j++)
                    {
                        if (mtddef_derived.Parameters[j].Name == par.Name)
                        {
                            index = j;
                            break;
                        }
                        if (index < 0)
                            throw new ArgumentException($"Parameter \"{par.Name}\" not found! Patch method: {mtdInfo.ReflectedType.FullName}.{mtdref_target.Name}");
                        list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, index + 1));
                    }
                }
            }
            return list_inst_pars;
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
            return (moduleType.FullName + "_" + moduleType.Assembly.GetName().Name).Replace(".", "_");
        }

        public static string CreateFieldName(TypeReference moduleType)
        {
            return (moduleType.FullName + "_" + moduleType.Module.Assembly.Name.Name).Replace(".", "_");
        }

        public static string CreateTypeName(Type itemActionType, params Type[] moduleTypes)
        {
            string typeName = itemActionType.FullName + "_" + itemActionType.Assembly.GetName().Name;
            foreach (Type type in moduleTypes)
            {
                if (type != null)
                    typeName += "__" + type.FullName + "_" + type.Assembly.GetName().Name;
            }
            typeName = typeName.Replace('.', '_');
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
            typeName = typeName.Replace('.', '_');
            return typeName;
        }
    }
}

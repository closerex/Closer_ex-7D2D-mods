using KFCommonUtilityLib.Scripts.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
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

            Dictionary<string, MethodDefinition> dict_overrides = new Dictionary<string, MethodDefinition>();
            //Apply Prefixes
            for (int i = moduleTypes.Length - 1; i >= 0; i++)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, (MethodInfo mtdInfo, Type prefType)> dict_targets = GetMethodOverrideTargets(itemActionType, moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodInfo mtdinf_target = pair.Value.mtdInfo;
                    MethodDefinition mtddef_derived = GetOrCreateOverride(dict_overrides, pair.Key, mtdinf_target, module);
                    //insert prefix code
                }
            }

            //Apply Postfixes

        }

        private static Dictionary<string, (MethodInfo mtdInfo, Type prefType)> GetMethodOverrideTargets(Type itemActionType, Type moduleType)
        {
            Dictionary<string, (MethodInfo mtdInfo, Type prefType)> dict_overrides = new Dictionary<string, (MethodInfo mtdInfo, Type prefType)>();
            foreach (var mtd in moduleType.GetMethods())
            {
                MethodTargetPrefixAttribute attr = mtd.GetCustomAttribute<MethodTargetPrefixAttribute>();
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

        private static MethodDefinition GetOrCreateOverride(Dictionary<string, MethodDefinition> dict_overrides, string id, MethodInfo mtdinf_target, ModuleDefinition module)
        {
            if (dict_overrides.TryGetValue(id, out var mtddef_derived))
            {
                return mtddef_derived;
            }
            MethodReference mtdref_target = module.ImportReference(mtdinf_target);
            mtddef_derived = new MethodDefinition(mtdinf_target.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot, mtdref_target.ReturnType);
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
            for (int i = 0; i < mtddef_derived.Parameters.Count; i++)
            {
                var par = mtddef_derived.Parameters[i];
                il.Append(il.Create((par.IsIn || par.IsOut) ? OpCodes.Ldarga_S : OpCodes.Ldarg_S , i));
            }
            il.Append(il.Create(OpCodes.Call, mtdref_target));
            if (hasReturnVal)
            {
                il.Append(il.Create(OpCodes.Stloc_1));
                il.Append(il.Create(OpCodes.Ldloc_1));
            }
            il.Append(il.Create(OpCodes.Ret));
            return mtddef_derived;
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

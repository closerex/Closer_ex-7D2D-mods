using HarmonyLib.Public.Patching;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using KFCommonUtilityLib.Harmony;
using UnityEngine.Scripting;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using Mono.Cecil.Rocks;
using KFCommonUtilityLib.Attributes;

namespace KFCommonUtilityLib
{
    public interface IModuleContainerFor<out T> where T : class
    {
        T Instance { get; }
    }

    public class TranspilerTarget
    {
        public TranspilerTarget(Type type_action, MethodInfo mtdinf_original, PatchInfo patchinf_harmony)
        {
            this.type_action = type_action;
            this.mtdinf_original = mtdinf_original;
            this.patchinf_harmony = patchinf_harmony;
        }

        public Type type_action;
        public MethodInfo mtdinf_original;
        public PatchInfo patchinf_harmony;
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

    public class ModuleManipulator
    {
        public ModuleDefinition module;
        public IModuleProcessor processor;
        public Type targetType;
        public Type baseType;
        public Type[] moduleTypes;
        public Type[][] moduleExtensionTypes;
        public TypeDefinition typedef_newTarget;
        public TypeReference typeref_interface;
        public FieldDefinition[] arr_flddef_modules;
        private static MethodInfo mtdinf = AccessTools.Method(typeof(ModuleManagers), nameof(ModuleManagers.GetModuleExtensions));

        public ModuleManipulator(AssemblyDefinition workingAssembly, IModuleProcessor processor, Type targetType, Type baseType, params Type[] moduleTypes)
        {
            module = workingAssembly.MainModule;
            this.processor = processor;
            this.targetType = targetType;
            this.baseType = baseType;
            this.moduleTypes = moduleTypes;
            moduleExtensionTypes = moduleTypes.Select(static t => (Type[])mtdinf.MakeGenericMethod(t).Invoke(null, null)).ToArray();
            Patch();
        }

        private void Patch()
        {
            typeref_interface = module.ImportReference(typeof(IModuleContainerFor<>));
            //Create new override type
            TypeReference typeref_target = module.ImportReference(targetType);
            typedef_newTarget = new TypeDefinition(null, ModuleUtils.CreateTypeName(targetType, moduleTypes), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed, typeref_target);
            typedef_newTarget.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(PreserveAttribute).GetConstructor(Array.Empty<Type>()))));
            module.Types.Add(typedef_newTarget);

            //Create fields
            arr_flddef_modules = new FieldDefinition[moduleTypes.Length];
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                //Create ItemAction field
                Type type_module = moduleTypes[i];
                MakeContainerFor(typedef_newTarget, type_module, out var flddef_module);
                arr_flddef_modules[i] = flddef_module;
            }

            //Create ItemAction constructor
            MethodDefinition mtddef_ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            if (processor == null || !processor.BuildConstructor(this, mtddef_ctor))
            {
                var il = mtddef_ctor.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Call, module.ImportReference(targetType.GetConstructor(Array.Empty<Type>()))));
                il.Append(il.Create(OpCodes.Nop));
                for (int i = 0; i < arr_flddef_modules.Length; i++)
                {
                    il.Append(il.Create(OpCodes.Ldarg_0));
                    il.Append(il.Create(OpCodes.Newobj, module.ImportReference(moduleTypes[i].GetConstructor(Array.Empty<Type>()))));
                    il.Append(il.Create(OpCodes.Stfld, arr_flddef_modules[i]));
                    il.Append(il.Create(OpCodes.Nop));
                }
                il.Append(il.Create(OpCodes.Ret));
            }
            typedef_newTarget.Methods.Add(mtddef_ctor);

            processor?.InitModules(this, targetType, baseType, moduleTypes);

            //<derived method name, method patch info>
            Dictionary<string, MethodPatchInfo> dict_overrides = new Dictionary<string, MethodPatchInfo>();
            //<derived method name, transpiler stub methods in inheritance order>
            Dictionary<string, List<TranspilerTarget>> dict_transpilers = new Dictionary<string, List<TranspilerTarget>>();
            //<derived method name, <module type name, local variable>>
            Dictionary<string, Dictionary<string, VariableDefinition>> dict_all_states = new Dictionary<string, Dictionary<string, VariableDefinition>>();

            //Get all transpilers
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Type moduleType = moduleTypes[i];
                const BindingFlags searchFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                //search all methods from module and extension
                foreach (var mtd in moduleType.GetMethods(searchFlags).Concat(moduleExtensionTypes[i].SelectMany(t => t.GetMethods(searchFlags))))
                {
                    var attr = mtd.GetCustomAttribute<MethodTargetTranspilerAttribute>();
                    foreach (var hp in mtd.GetCustomAttributes<HarmonyPatch>())
                    {
                        //make sure the transpiler has a target method to apply, otherwise skip it
                        if (attr != null && hp != null && hp.info.declaringType != null && hp.info.declaringType.IsAssignableFrom(targetType))
                        {
                            var hm = hp.info;
                            hm.methodType = hm.methodType ?? MethodType.Normal;
                            var mtdinf_target = hm.GetOriginalMethod() as MethodInfo;
                            if (mtdinf_target == null || mtdinf_target.IsAbstract || !mtdinf_target.IsVirtual)
                            {
                                continue;
                            }
                            string id = hm.GetTargetMethodIdentifier();
                            if (!dict_transpilers.TryGetValue(id, out var list))
                            {
                                dict_transpilers[id] = (list = new List<TranspilerTarget>());
                                Type nextType = targetType;
                                TranspilerTarget curNode = null;
                                var hm_next = hm.Clone();
                                while (hm.declaringType.IsAssignableFrom(nextType))
                                {
                                    hm_next.declaringType = nextType;
                                    var mtdinfo_cur = hm_next.GetOriginalMethod() as MethodInfo;
                                    if (mtdinfo_cur != null)
                                    {
                                        var patchinf_harmony = mtdinfo_cur.ToPatchInfoDontAdd().Copy();
                                        curNode = new TranspilerTarget(mtdinfo_cur.DeclaringType, mtdinfo_cur, patchinf_harmony);
                                        list.Add(curNode);
                                    }
                                    nextType = nextType.BaseType;
                                }

                                if (curNode != null)
                                {
                                    curNode.patchinf_harmony.AddTranspilers(CommonUtilityLibInit.HarmonyInstance.Id, new HarmonyMethod(mtd));
                                    Log.Out($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', curNode.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                }
                            }
                            else
                            {
                                bool childFound = false;
                                foreach (var node in ((IEnumerable<TranspilerTarget>)list).Reverse())
                                {
                                    if (node.type_action.Equals(hm.declaringType))
                                    {
                                        childFound = true;
                                        node.patchinf_harmony.AddTranspilers(CommonUtilityLibInit.HarmonyInstance.Id, mtd);
                                        Log.Out($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', node.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                        break;
                                    }
                                }

                                if (!childFound)
                                {
                                    Type nextType = list[list.Count - 1].type_action.BaseType;
                                    TranspilerTarget curNode = null;
                                    var hm_next = hm.Clone();
                                    while (hm.declaringType.IsAssignableFrom(nextType))
                                    {
                                        hm_next.declaringType = nextType;
                                        var mtdinfo_cur = hm_next.GetOriginalMethod() as MethodInfo;
                                        if (mtdinfo_cur != null)
                                        {
                                            var patchinf_harmony = mtdinfo_cur.ToPatchInfoDontAdd().Copy();
                                            curNode = new TranspilerTarget(mtdinfo_cur.DeclaringType, mtdinfo_cur, patchinf_harmony);
                                            list.Add(curNode);
                                        }
                                        nextType = nextType.BaseType;
                                    }

                                    if (curNode != null)
                                    {
                                        curNode.patchinf_harmony.AddTranspilers(CommonUtilityLibInit.HarmonyInstance.Id, new HarmonyMethod(mtd));
                                        Log.Out($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', curNode.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //apply transpilers and replace method calls on base methods with patched ones
            Dictionary<string, MethodDefinition> dict_replacers = new Dictionary<string, MethodDefinition>();
            foreach (var pair in dict_transpilers)
            {
                List<TranspilerTarget> list = pair.Value;

                //the top copy to call in the override method
                MethodDefinition mtddef_override_copy = null;
                MethodReference mtdref_override_base = null;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    TranspilerTarget curNode = list[i];
                    MethodPatcher patcher = curNode.mtdinf_original.GetMethodPatcher();
                    DynamicMethodDefinition dmd = patcher.CopyOriginal();
                    ILContext context = new ILContext(dmd.Definition);
                    HarmonyManipulator.Manipulate(curNode.mtdinf_original, curNode.patchinf_harmony, context);
                    var mtdref_original = module.ImportReference(curNode.mtdinf_original);
                    var mtddef_copy = mtdref_original.Resolve().CloneToModuleAsStatic(context.Body, module.ImportReference(curNode.type_action), module);
                    dmd.Dispose();
                    context.Dispose();
                    if (mtddef_override_copy != null && mtdref_override_base != null)
                    {
                        //replace calls to the base
                        foreach (var ins in mtddef_copy.Body.Instructions)
                        {
                            if (ins.OpCode == OpCodes.Call && ((MethodReference)ins.Operand).FullName.Equals(mtdref_override_base.FullName))
                            {
                                Log.Out($"replacing call to {mtdref_override_base.FullName} to {mtddef_override_copy.FullName}");
                                ins.Operand = mtddef_override_copy;
                            }
                        }
                    }
                    //add patched copy to the class
                    typedef_newTarget.Methods.Add(mtddef_copy);
                    //the iteration is reversed so make sure we grab the latest method
                    mtddef_override_copy = mtddef_copy;
                    mtdref_override_base = mtdref_original;
                }
                //create the method override that calls the patched copy
                if (mtddef_override_copy != null && mtdref_override_base != null)
                {
                    GetOrCreateOverride(dict_overrides, pair.Key, mtdref_override_base, mtddef_override_copy);
                }
            }

            //Apply Postfixes first so that Prefixes can jump to the right instruction
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPostfixAttribute>(i);
                string moduleID = ModuleUtils.CreateFieldName(moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodDefinition mtddef_root = module.ImportReference(pair.Value.mtdinf_base.GetBaseDefinition()).Resolve();
                    MethodDefinition mtddef_target = module.ImportReference(pair.Value.mtdinf_target).Resolve();
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, pair.Value.mtdref_base);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;

                    if (!dict_all_states.TryGetValue(pair.Key, out var dict_states))
                    {
                        dict_states = new Dictionary<string, VariableDefinition>();
                        dict_all_states.Add(pair.Key, dict_states);
                    }
                    var list_inst_pars = MatchPatchArguments(mtddef_root, mtdpinf_derived, mtddef_target, i, true, dict_states, moduleID);
                    //insert invocation
                    var il = mtddef_derived.Body.GetILProcessor();
                    foreach (var ins in list_inst_pars)
                    {
                        il.InsertBefore(mtdpinf_derived.PostfixEnd, ins);
                    }
                    il.InsertBefore(mtdpinf_derived.PostfixEnd, il.Create(mtddef_target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, module.ImportReference(mtddef_target)));
                    if (mtdpinf_derived.PostfixBegin == null)
                        mtdpinf_derived.PostfixBegin = list_inst_pars[0];
                }
            }

            //Apply Prefixes
            for (int i = moduleTypes.Length - 1; i >= 0; i--)
            {
                Type moduleType = moduleTypes[i];
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPrefixAttribute>(i);
                string moduleID = ModuleUtils.CreateFieldName(moduleType);
                foreach (var pair in dict_targets)
                {
                    MethodDefinition mtddef_root = module.ImportReference(pair.Value.mtdinf_base.GetBaseDefinition()).Resolve();
                    MethodDefinition mtddef_target = module.ImportReference(pair.Value.mtdinf_target).Resolve();
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, pair.Value.mtdref_base);
                    MethodDefinition mtddef_derived = mtdpinf_derived.Method;
                    dict_all_states.TryGetValue(pair.Key, out var dict_states);
                    var list_inst_pars = MatchPatchArguments(mtddef_root, mtdpinf_derived, mtddef_target, i, false, dict_states, moduleID);
                    //insert invocation
                    var il = mtdpinf_derived.Method.Body.GetILProcessor();
                    Instruction ins_insert = mtdpinf_derived.PrefixBegin;
                    foreach (var ins in list_inst_pars)
                    {
                        il.InsertBefore(ins_insert, ins);
                    }
                    il.InsertBefore(ins_insert, il.Create(mtddef_target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, module.ImportReference(mtddef_target)));
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
                typedef_newTarget.Methods.Add(mtd.Method);

                //Log.Out($"Add method override to new action: {mtd.Method.Name}");
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="targetType"></param>
        /// <param name="moduleType"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        private Dictionary<string, MethodOverrideInfo> GetMethodOverrideTargets<T>(int moduleIndex) where T : Attribute, IMethodTarget
        {
            Type moduleType = moduleTypes[moduleIndex];
            Dictionary<string, MethodOverrideInfo> dict_overrides = new Dictionary<string, MethodOverrideInfo>();
            const BindingFlags searchFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags extensionFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var mtd in moduleType.GetMethods(searchFlags).Concat(moduleExtensionTypes[moduleIndex].SelectMany(t => t.GetMethods(extensionFlags))))
            {
                if (mtd.GetCustomAttribute<T>() != null)
                {
                    foreach (HarmonyPatch hp in mtd.GetCustomAttributes<HarmonyPatch>())
                    {
                        if (hp != null && (hp.info.declaringType == null || hp.info.declaringType.IsAssignableFrom(targetType)))
                        {
                            var hm = hp.info;
                            hm.methodType = hm.methodType ?? MethodType.Normal;
                            var hmclone = hm.Clone();
                            hmclone.declaringType = targetType;
                            string id = hm.GetTargetMethodIdentifier();
                            MethodInfo mtdinf_base = hmclone.GetBaseMethod() as MethodInfo;
                            if (mtdinf_base == null || !mtdinf_base.IsVirtual || mtdinf_base.IsFinal)
                            {
                                Log.Error($"Method not found: {moduleType.FullName} on {hmclone.methodName}\n{hmclone.ToString()}");
                                continue;
                            }

                            MethodReference mtdref_base = module.ImportReference(mtdinf_base);
                            //Find preferred patch
                            if (dict_overrides.TryGetValue(id, out var info))
                            {
                                if (hm.declaringType == null)
                                    continue;
                                //cur action type is sub or same class of cur preferred type
                                //cur preferred type is sub class of previous preferred type
                                //means cur preferred type is closer to the action type in inheritance hierachy than the previous one
                                if (hm.declaringType.IsAssignableFrom(targetType) && (info.prefType == null || hm.declaringType.IsSubclassOf(info.prefType)))
                                {
                                    dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, mtdref_base, hm.declaringType);
                                }
                            }
                            else
                            {
                                dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, mtdref_base, hm.declaringType);
                            }
                            //Log.Out($"Add method override: {id} for {mtdref_base.FullName}/{mtdinf_base.Name}, action type: {itemActionType.Name}");
                        }
                        else
                        {
                            //Log.Out($"No override target found or preferred type not match on {mtd.Name}");
                        }
                    }
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
        private MethodPatchInfo GetOrCreateOverride(Dictionary<string, MethodPatchInfo> dict_overrides, string id, MethodReference mtdref_base, MethodDefinition mtddef_base_override = null)
        {
            //if (mtddef_base.FullName == "CreateModifierData")
            //    throw new MethodAccessException($"YOU SHOULD NOT MANUALLY MODIFY CreateModifierData!");
            if (dict_overrides.TryGetValue(id, out var mtdpinf_derived))
            {
                return mtdpinf_derived;
            }
            //when overriding, retain attributes of base but make sure to remove the 'new' keyword which presents if you are overriding the root method
            MethodDefinition mtddef_base = mtdref_base.Resolve();
            MethodDefinition mtddef_derived = new MethodDefinition(mtddef_base.Name, (mtddef_base.Attributes | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot) & ~MethodAttributes.NewSlot, module.ImportReference(mtddef_base.ReturnType));

            //Log.Out($"Create method override: {id} for {mtdref_base.FullName}");
            foreach (var par in mtddef_base_override?.Parameters?.Skip(1) ?? mtddef_base.Parameters)
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
                mtddef_derived.Body.Variables.Add(new VariableDefinition(module.ImportReference(mtddef_base.ReturnType)));
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
                il.Emit(OpCodes.Ldarg_S, par);
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

        public IEnumerable<Instruction> MatchConstructorArguments(MethodDefinition mtddef_original, MethodDefinition mtddef_target, int moduleIndex, Func<ParameterDefinition, MethodDefinition, MethodDefinition, int, List<Instruction>, bool> matchSpecialParDelegate)
        {
            var il = mtddef_original.Body.GetILProcessor();
            yield return il.Create(OpCodes.Ldarg_0);
            var list_special_args = new List<Instruction>();
            foreach (var par in mtddef_target.Parameters)
            {
                list_special_args.Clear();
                if (par.Name.StartsWith("___"))
                {
                    //___ means non public fields
                    string str_fldname = par.Name.Substring(3);
                    FieldDefinition flddef_target = module.ImportReference(targetType.GetField(str_fldname, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).Resolve();
                    if (flddef_target == null)
                        throw new MissingFieldException($"Field with name \"{str_fldname}\" not found! Patch method: {mtddef_target.DeclaringType.FullName}.{mtddef_target.Name}");
                    if (flddef_target.IsStatic)
                    {
                        yield return il.Create((par.ParameterType.IsByReference) ? OpCodes.Ldsflda : OpCodes.Ldsfld, module.ImportReference(flddef_target));
                    }
                    else
                    {
                        yield return il.Create(OpCodes.Ldarg_0);
                        yield return il.Create(par.ParameterType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, module.ImportReference(flddef_target));
                    }
                }
                else if (matchSpecialParDelegate == null || !matchSpecialParDelegate(par, mtddef_original, mtddef_target, moduleIndex, list_special_args))
                {
                    //match param by name
                    int index = -1;
                    if (!par.Name.StartsWith("__") || !int.TryParse(par.Name.Substring(2), out index))
                    {
                        for (int j = 0; j < mtddef_original.Parameters.Count; j++)
                        {
                            if (mtddef_original.Parameters[j].Name == par.Name)
                            {
                                index = mtddef_original.Parameters[j].Index;
                                break;
                            }
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
                                  $"root pars: {{{string.Join(",", mtddef_original.Parameters.Select(p => p.Name + "/" + p.Index).ToArray())}}}" +
                                  $"derived pars: {{{string.Join(",", mtddef_target.Parameters.Select(p => p.Name + "/" + p.Index).ToArray())}}}");
                        throw e;
                    }
                    if (!mtddef_original.Parameters[index].ParameterType.IsByReference)
                    {
                        yield return MonoCecilExtensions.LoadArgAtIndex(index, par.ParameterType.IsByReference, false, mtddef_original.Parameters, il);
                        //yield return il.Create(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, mtddef_original.Parameters[index]);
                    }
                    else
                    {
                        yield return MonoCecilExtensions.LoadArgAtIndex(index, true, false, mtddef_original.Parameters, il);
                        //yield return il.Create(OpCodes.Ldarg_S, mtddef_original.Parameters[index]);
                        if (!par.ParameterType.IsByReference)
                        {
                            var opcode = par.ParameterType.LoadRefAsValue(out bool isStruct);
                            yield return isStruct ? il.Create(opcode, par.ParameterType) : il.Create(opcode);
                        }
                    }
                }
                else
                {
                    foreach (var ins in list_special_args)
                    {
                        yield return ins;
                    }
                }
            }
            yield return il.Create(OpCodes.Newobj, module.ImportReference(mtddef_target));
            yield return il.Create(OpCodes.Stfld, arr_flddef_modules[moduleIndex]);
        }

        /// <summary>
        /// Create a List<Instruction> that loads all arguments required to call the method onto stack.
        /// </summary>
        /// <param name="mtddef_root">The root definition of this method.</param>
        /// <param name="mtdpinf_derived">The override method.</param>
        /// <param name="mtddef_target">The patch method to be called.</param>
        /// <param name="flddef_module">The injected module field.</param>
        /// <param name="flddef_data">The injected data field.</param>
        /// <param name="module">The assembly's main module.</param>
        /// <param name="itemActionType">The base ItemAction type.</param>
        /// <returns></returns>
        /// <exception cref="MissingFieldException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        private List<Instruction> MatchPatchArguments(MethodDefinition mtddef_root, MethodPatchInfo mtdpinf_derived, MethodDefinition mtddef_target, int moduleIndex, bool isPostfix, Dictionary<string, VariableDefinition> dict_states, string moduleID)
        {
            FieldDefinition flddef_module = arr_flddef_modules[moduleIndex];
            var mtddef_derived = mtdpinf_derived.Method;
            var il = mtddef_derived.Body.GetILProcessor();
            //Match parameters
            List<Instruction> list_inst_pars = new List<Instruction>();
            list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
            list_inst_pars.Add(il.Create(OpCodes.Ldfld, flddef_module));
            foreach (var par in mtddef_target.IsStatic ? mtddef_target.Parameters.Skip(1) : mtddef_target.Parameters)
            {
                if (par.Name.StartsWith("___"))
                {
                    //___ means non public fields
                    string str_fldname = par.Name.Substring(3);
                    FieldDefinition flddef_target = module.ImportReference(targetType.GetField(str_fldname, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).Resolve();
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
                else if (!MatchPatchSpecialParameters(par, mtddef_target, mtdpinf_derived, moduleIndex, list_inst_pars, il, isPostfix, dict_states, moduleID))
                {
                    //match param by name
                    int index = -1;
                    if (!par.Name.StartsWith("__") || !int.TryParse(par.Name.Substring(2), out index))
                    {
                        for (int j = 0; j < mtddef_root.Parameters.Count; j++)
                        {
                            if (mtddef_root.Parameters[j].Name == par.Name)
                            {
                                index = mtddef_root.Parameters[j].Index;
                                break;
                            }
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
                        list_inst_pars.Add(MonoCecilExtensions.LoadArgAtIndex(index, par.ParameterType.IsByReference, false, mtddef_derived.Parameters, il));
                        //list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldarga_S : OpCodes.Ldarg_S, mtddef_derived.Parameters[index]));
                    }
                    else
                    {
                        list_inst_pars.Add(MonoCecilExtensions.LoadArgAtIndex(index, true, false, mtddef_derived.Parameters, il));
                        //list_inst_pars.Add(il.Create(OpCodes.Ldarg_S, mtddef_derived.Parameters[index]));
                        if (!par.ParameterType.IsByReference)
                        {
                            var opcode = par.ParameterType.LoadRefAsValue(out bool isStruct);
                            list_inst_pars.Add(isStruct ? il.Create(opcode, par.ParameterType) : il.Create(opcode));
                        }
                    }
                }
            }
            return list_inst_pars;
        }

        private bool MatchPatchSpecialParameters(ParameterDefinition par, MethodDefinition mtddef_target, MethodPatchInfo mtdpinf_derived, int moduleIndex, List<Instruction> list_inst_pars, ILProcessor il, bool isPostfix, Dictionary<string, VariableDefinition> dict_states, string moduleID)
        {
            MethodDefinition mtddef_derived = mtdpinf_derived.Method;
            switch (par.Name)
            {
                //load ItemAction instance
                case "__instance":
                    list_inst_pars.Add(il.Create(OpCodes.Ldarg_0));
                    break;
                //load return value
                case "__result":
                    if (mtddef_derived.ReturnType.MetadataType != MetadataType.Void)
                    {
                        list_inst_pars.Add(il.Create(par.ParameterType.IsByReference ? OpCodes.Ldloca_S : OpCodes.Ldloc_S, mtddef_derived.Body.Variables[1]));
                    }
                    else
                    {
                        throw new ArgumentException($"{mtddef_target.DeclaringType.FullName}.{mtddef_target.Name} does not have a return value!");
                    }
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
                    return processor != null ? processor.MatchSpecialArgs(par, mtddef_target, mtdpinf_derived, moduleIndex, list_inst_pars, il) : false;
            }
            return true;
        }

        public void MakeContainerFor(TypeDefinition typedef_container, Type type_module, out FieldDefinition flddef_module)
        {
            var typeref_module = module.ImportReference(type_module);
            flddef_module = new FieldDefinition(ModuleUtils.CreateFieldName(type_module), FieldAttributes.Public, typeref_module);
            typedef_container.Fields.Add(flddef_module);
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
    }
}

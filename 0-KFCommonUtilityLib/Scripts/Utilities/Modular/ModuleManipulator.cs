using HarmonyLib;
using HarmonyLib.Public.Patching;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Harmony;
using MonoMod.Cil;
using MonoMod.Utils;
using MonoMod.Utils.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine.Scripting;

namespace KFCommonUtilityLib
{
    public interface IModuleContainerFor<out T> where T : class
    {
        T Instance { get; }
    }

    public class TranspilerTarget
    {
        public TranspilerTarget(Type type_target, MethodInfo mtdinf_original, PatchInfo patchinf_harmony)
        {
            this.type_target = type_target;
            this.mtdinf_original = mtdinf_original;
            this.patchinf_harmony = patchinf_harmony;
        }

        public Type type_target;
        public MethodInfo mtdinf_original;
        public PatchInfo patchinf_harmony;
    }

    public class MethodPatchInfo
    {
        public readonly string ID;
        public readonly MethodBuilder Method;
        public readonly MethodInfo OriginalMethod;
        public readonly MethodBuilder TranspilerBuilder;
        public readonly bool HasReturnVal;
        public int PrefixBegin, PostfixBegin, PostfixEnd;
        internal readonly List<MethodOverrideInfo> Prefixes = new();
        internal readonly List<MethodOverrideInfo> Postfixes = new();
        internal readonly Dictionary<string, LocalBuilder> States = new();
        internal bool HasRunOriginalCondition;

        public MethodPatchInfo(MethodBuilder mtdbd, MethodInfo originalMethod, MethodBuilder transpilerBuilder, string id)
        {
            Method = mtdbd;
            OriginalMethod = originalMethod;
            ID = id;
            HasReturnVal = !mtdbd.ReturnType.IsVoid();
            TranspilerBuilder = transpilerBuilder;
        }
    }

    public class MethodOverrideInfo
    {
        public readonly MethodInfo mtdinf_target;
        public readonly MethodInfo mtdinf_base;
        public readonly Type prefType;
        public readonly string moduleName;
        public readonly int moduleIndex;

        public MethodOverrideInfo(MethodInfo mtdinf_target, MethodInfo mtdinf_base, Type prefType, string moduleName, int moduleIndex)
        {
            this.mtdinf_target = mtdinf_target;
            this.mtdinf_base = mtdinf_base;
            this.prefType = prefType;
            this.moduleName = moduleName;
            this.moduleIndex = moduleIndex;
        }
    }

    public class ModuleManipulator
    {
        private static class TranspilerReplacer
        {
            internal static Mono.Cecil.MethodReference mtdref_override_base { get; private set; } = null;
            internal static DynamicMethodReference dmtdref_override_copy { get; private set; } = null;

            public static void ILManipulator(ILContext il)
            {
                if (IsStateValid())
                {
                    foreach (var ins in il.Body.Instructions)
                    {
                        if (ins.OpCode == Mono.Cecil.Cil.OpCodes.Call && MonoModPatches.Equals(mtdref_override_base, ins.Operand as Mono.Cecil.MethodReference))
                        {
                            ModuleManagers.LogOut($"Adding Transpiler Replacer\nfrom {mtdref_override_base.FullName}\nto {dmtdref_override_copy.FullName}");
                            ins.Operand = dmtdref_override_copy;
                        }
                    }
                }
            }

            public static void UpdateState(Mono.Cecil.MethodReference mtdinf_base, DynamicMethodReference mtdinf_copy)
            {
                mtdref_override_base = mtdinf_base;
                dmtdref_override_copy = mtdinf_copy;
            }

            public static bool IsStateValid()
            {
                return dmtdref_override_copy != null && mtdref_override_base != null;
            }
        }

        public IModuleProcessor processor;
        public Type targetType;
        public Type baseType;
        public Type[] moduleTypes;
        public Type[][] moduleExtensionTypes;
        public TypeBuilder typebd_newTarget;
        public FieldBuilder[] arr_fldbd_modules;
        private static MethodInfo mtdinf_getext = AccessTools.Method(typeof(ModuleManagers), nameof(ModuleManagers.GetModuleExtensions));
        private const string HARMONY_INSTANCE_ID = "kflib.modular.manipulator";

        public ModuleManipulator(AssemblyBuilder workingAssembly, IModuleProcessor processor, Type targetType, Type baseType, TypeBuilder parentTB, out Type resultTB, params Type[] moduleTypes)
        {
            this.processor = processor;
            this.targetType = targetType;
            this.baseType = baseType;
            this.moduleTypes = moduleTypes;
            moduleExtensionTypes = moduleTypes.Select(static t => (Type[])mtdinf_getext.MakeGenericMethod(t).Invoke(null, null)).ToArray();
            resultTB = Patch(parentTB);
        }

        private Type Patch(TypeBuilder parentTB)
        {
            var type_interface = typeof(IModuleContainerFor<>);
            //Create new override type
            typebd_newTarget = parentTB != null ?
                               parentTB.DefineNestedType(ModuleUtils.CreateTypeName(targetType, moduleTypes), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.NestedPublic | TypeAttributes.Sealed, targetType) :
                               ModuleManagers.WorkingModule.DefineType(ModuleUtils.CreateTypeName(targetType, moduleTypes), TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Public | TypeAttributes.Sealed, targetType);
            //typebd_newTarget.SetCustomAttribute(new CustomAttributeBuilder(AccessTools.DeclaredConstructor(typeof(PreserveAttribute)), new object[0]));

            //Create fields
            arr_fldbd_modules = new FieldBuilder[moduleTypes.Length];
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Type type_module = moduleTypes[i];
                MakeContainerFor(typebd_newTarget, type_interface, type_module, out var flddef_module);
                arr_fldbd_modules[i] = flddef_module;
            }

            processor.InitModules(this);
            
            //Create constructor
            BuildConstructor();

            //<derived method name, method patch info>
            Dictionary<string, MethodPatchInfo> dict_overrides = new Dictionary<string, MethodPatchInfo>();
            //<derived method name, transpiler stub methods in inheritance order>
            Dictionary<string, List<TranspilerTarget>> dict_transpilers = new Dictionary<string, List<TranspilerTarget>>();

            //List<(MethodInfo target, MethodInfo from, MethodInfo to)> list_replacers = new();
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
                                    curNode.patchinf_harmony.AddTranspilers(HARMONY_INSTANCE_ID, new HarmonyMethod(mtd));
                                    ModuleManagers.LogOut($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', curNode.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                }
                            }
                            else
                            {
                                bool childFound = false;
                                foreach (var node in ((IEnumerable<TranspilerTarget>)list).Reverse())
                                {
                                    if (node.type_target.Equals(hm.declaringType))
                                    {
                                        childFound = true;
                                        node.patchinf_harmony.AddTranspilers(HARMONY_INSTANCE_ID, mtd);
                                        ModuleManagers.LogOut($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', node.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                        break;
                                    }
                                }

                                if (!childFound)
                                {
                                    Type nextType = list[list.Count - 1].type_target.BaseType;
                                    TranspilerTarget curNode = null;
                                    var hm_next = hm.Clone();
                                    while (hm.declaringType.IsAssignableFrom(nextType))
                                    {
                                        hm_next.declaringType = nextType;
                                        var mtdinfo_cur = hm_next.GetOriginalMethod() as MethodInfo;
                                        if (mtdinfo_cur != null)
                                        {
                                            curNode = new TranspilerTarget(mtdinfo_cur.DeclaringType, mtdinfo_cur, new());
                                            list.Add(curNode);
                                        }
                                        nextType = nextType.BaseType;
                                    }

                                    if (curNode != null)
                                    {
                                        curNode.patchinf_harmony.AddTranspilers(HARMONY_INSTANCE_ID, new HarmonyMethod(mtd));
                                        ModuleManagers.LogOut($"Adding transpiler {mtd.FullDescription()}\nCurrent transpilers:\n{string.Join('\n', curNode.patchinf_harmony.transpilers.Select(p => p.PatchMethod.FullDescription()))}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            List<(DynamicMethodDefinition, ILContext)> list_temp = new();
            //apply transpilers and replace method calls on base methods with patched ones
            foreach (var pair in dict_transpilers)
            {
                List<TranspilerTarget> list = pair.Value;

                //the top copy to call in the override method
                MethodInfo mtdinf_prev_original = null;
                MethodBuilder mtdbd_prev_replace = null;
                TranspilerReplacer.UpdateState(null, null);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    TranspilerTarget curNode = list[i];
                    curNode.patchinf_harmony.AddILManipulators(HARMONY_INSTANCE_ID, new HarmonyMethod(AccessTools.Method(typeof(TranspilerReplacer), nameof(TranspilerReplacer.ILManipulator))), new HarmonyMethod(AccessTools.Method(typeof(CallClosureFixPatches), nameof(CallClosureFixPatches.DelegateManipulator))));
                    //todo: dont add?
                    MethodPatcher patcher = curNode.mtdinf_original.GetMethodPatcher();
                    var dmd = patcher.CopyOriginal();
                    curNode.mtdinf_original.CopyParamInfoTo(dmd);
                    var context = new ILContext(dmd.Definition);
                    list_temp.Add((dmd, context));
                    //add patched copy to the class
                    //the iteration is reversed so make sure we grab the latest method
                    CallClosureFixPatches.ApplyFix(typebd_newTarget);
                    HarmonyManipulator.Manipulate(curNode.mtdinf_original, curNode.patchinf_harmony, context);
                    CallClosureFixPatches.RemoveFix();
                    mtdinf_prev_original = curNode.mtdinf_original;
                    mtdbd_prev_replace = dmd.GenerateMethodBuilder(typebd_newTarget);
                    TranspilerReplacer.UpdateState(dmd.Module.ImportReference(mtdinf_prev_original), new DynamicMethodReference(dmd.Module, mtdbd_prev_replace));
                }
                if (TranspilerReplacer.IsStateValid())
                {
                    GetOrCreateOverride(dict_overrides, pair.Key, mtdinf_prev_original, mtdbd_prev_replace);
                }
            }
            TranspilerReplacer.UpdateState(null, null);

            foreach (var pair in list_temp)
            {
                pair.Item1.Dispose();
                pair.Item2.Dispose();
            }
            list_temp = null;

            //Apply Postfixes first so that Prefixes can jump to the right instruction
            for (int i = 0; i < moduleTypes.Length; i++)
            {
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPostfixAttribute>(i);
                foreach (var pair in dict_targets)
                {
                    var mtdpinf_derived = GetOrCreateOverride(dict_overrides, pair.Key, pair.Value.mtdinf_base);
                    mtdpinf_derived.Postfixes.Add(pair.Value);
                }
            }

            //Apply Prefixes
            for (int i = moduleTypes.Length - 1; i >= 0; i--)
            {
                Dictionary<string, MethodOverrideInfo> dict_targets = GetMethodOverrideTargets<MethodTargetPrefixAttribute>(i);
                foreach (var pair in dict_targets)
                {
                    string id = pair.Key;
                    MethodPatchInfo mtdpinf_derived = GetOrCreateOverride(dict_overrides, id, pair.Value.mtdinf_base);
                    mtdpinf_derived.Prefixes.Add(pair.Value);
                }
            }

            foreach (var mtdpinf in dict_overrides.Values)
            {
                ApplyMethodPatches(mtdpinf);
                if (mtdpinf.States.Count > 0)
                {
                    throw new Exception($"__state variable count does not match for {mtdpinf.ID}!");
                }
            }
            var res = typebd_newTarget.CreateType();
            return res;
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
            string moduleID = ModuleUtils.CreateFieldName(moduleType);
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
                                    dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, hm.declaringType, moduleID, moduleIndex);
                                }
                            }
                            else
                            {
                                dict_overrides[id] = new MethodOverrideInfo(mtd, mtdinf_base, hm.declaringType, moduleID, moduleIndex);
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
        /// <param name="mtdinf_base"></param>
        /// <param name="module"></param>
        /// <returns></returns>
        private MethodPatchInfo GetOrCreateOverride(Dictionary<string, MethodPatchInfo> dict_overrides, string id, MethodInfo mtdinf_base, MethodBuilder mtdbd_base_override = null)
        {
            if (dict_overrides.TryGetValue(id, out var mtdpinf_derived))
            {
                return mtdpinf_derived;
            }
            //when overriding, retain attributes of base but make sure to remove the 'new' keyword which presents if you are overriding the root method
            ParameterInfo[] paramInfo = mtdinf_base.GetParameters();
            var mtdbd_derived = typebd_newTarget.DefineMethod(mtdinf_base.Name,
                                                              (mtdinf_base.Attributes | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final) & ~MethodAttributes.NewSlot,
                                                              CallingConventions.HasThis,
                                                              mtdinf_base.ReturnType,
                                                              mtdinf_base.ReturnParameter.GetRequiredCustomModifiers().Length > 0 ? mtdinf_base.ReturnParameter.GetRequiredCustomModifiers() : null,
                                                              mtdinf_base.ReturnParameter.GetOptionalCustomModifiers().Length > 0 ? mtdinf_base.ReturnParameter.GetOptionalCustomModifiers() : null,
                                                              paramInfo.Select(static p => p.ParameterType).ToArray(),
                                                              paramInfo.Select(static p => { var mod = p.GetRequiredCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray(),
                                                              paramInfo.Select(static p => { var mod = p.GetOptionalCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray());
            mtdinf_base.CopyParamInfoTo(mtdbd_derived);
            var il = mtdbd_derived.GetILGenerator();
            il.DeclareLocal(typeof(bool));
            if (!mtdbd_derived.ReturnType.IsVoid())
            {
                il.DeclareLocal(mtdbd_derived.ReturnType);
            }

            mtdpinf_derived = new MethodPatchInfo(mtdbd_derived, mtdinf_base, mtdbd_base_override, id);
            dict_overrides.Add(id, mtdpinf_derived);
            return mtdpinf_derived;
        }

        private void BuildConstructor()
        {
            ModuleManagers.LogOut($"Building ctor for {typebd_newTarget.Name}...");
            ConstructorInfo ctorinf_default = targetType.GetDeclaredConstructors()[0];
            ParameterInfo[] paramInfo = ctorinf_default.GetParameters();
            Type[] paramTypes = paramInfo.Select(static p => p.ParameterType).ToArray();
            var ctorbd_target = typebd_newTarget.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                                                         CallingConventions.HasThis,
                                                                         paramTypes,
                                                                         paramInfo.Select(static p => { var mod = p.GetRequiredCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray(),
                                                                         paramInfo.Select(static p => { var mod = p.GetOptionalCustomModifiers(); return mod != null && mod.Length > 0 ? mod : null; }).ToArray());
            if (!processor.DefineConstructorArgs(this, ctorbd_target, out var pbs))
            {
                pbs = ctorinf_default.CopyParamInfoTo(ctorbd_target);
            }
            ILGenerator generator = ctorbd_target.GetILGenerator();
            ConstructorInfo ctorinf_original = targetType.GetDeclaredConstructors()[0];

            generator.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < ctorinf_original.GetParameters().Length; i++)
            {
                generator.LoadArg(i + 1);
            }
            generator.Emit(OpCodes.Call, ctorinf_original);

            for (int i = 0; i < arr_fldbd_modules.Length; i++)
            {
                ConstructorInfo ctorinf_target = moduleTypes[i].GetDeclaredConstructors()[0];
                MatchConstructorArguments(generator, pbs, paramTypes, ctorinf_target, i);
            }
            processor.BuildConstructor(this, generator);
            generator.Emit(OpCodes.Ret);
        }

        private void ApplyMethodPatches(MethodPatchInfo info)
        {
            ModuleManagers.LogOut($"Building Method for {typebd_newTarget.Name}.{info.Method.Name}...");
            var generator = info.Method.GetILGenerator();
            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Stloc_0);
            if (info.HasRunOriginalCondition && info.HasReturnVal)
            {
                generator.Emit(OpCodes.Ldloca_S, 1);
                generator.Emit(OpCodes.Initobj, info.OriginalMethod.ReturnType);
            }

            foreach (var workingOverrideInfo in info.Prefixes)
            {
                //insert invocation
                var mtddef_target = workingOverrideInfo.mtdinf_target;
                var mtddef_derived = info.Method;
                MatchPatchArguments(generator, info, workingOverrideInfo, false);
                generator.Emit(mtddef_target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, mtddef_target);

                if (!mtddef_target.ReturnType.IsVoid())
                {
                    if (mtddef_target.ReturnType != typeof(bool))
                    {
                        generator.Emit(OpCodes.Pop);
                    }
                    else
                    {
                        generator.Emit(OpCodes.Ldloc_0);
                        generator.Emit(OpCodes.And);
                        generator.Emit(OpCodes.Stloc_0);
                        info.HasRunOriginalCondition = true;
                    }
                }
            }

            Label? lbl = null;
            if (info.HasRunOriginalCondition)
            {
                lbl = generator.DefineLabel();
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Brfalse_S, lbl.Value);
            }

            generator.Emit(OpCodes.Ldc_I4_1);
            generator.Emit(OpCodes.Stloc_0);
            generator.Emit(OpCodes.Ldarg_0);
            ParameterInfo[] paramInfo = info.OriginalMethod.GetParameters();
            for (int i = 0; i < paramInfo.Length; i++)
            {
                generator.LoadArg(i + 1);
            }
            generator.Emit(OpCodes.Call, info.TranspilerBuilder ?? info.OriginalMethod);
            if (info.HasReturnVal)
            {
                generator.Emit(OpCodes.Stloc_1);
            }

            bool postfixLabelApplied = lbl == null;
            foreach (var workingOverrideInfo in info.Postfixes)
            {
                if (!postfixLabelApplied)
                {
                    generator.MarkLabel(lbl.Value);
                    postfixLabelApplied = true;
                }
                //insert invocation
                MatchPatchArguments(generator, info, workingOverrideInfo, true);
                generator.Emit(workingOverrideInfo.mtdinf_target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, workingOverrideInfo.mtdinf_target);
            }

            if (!postfixLabelApplied)
            {
                generator.MarkLabel(lbl.Value);
                postfixLabelApplied = true;
            }
            if (info.HasReturnVal)
            {
                generator.Emit(OpCodes.Ldloc_1);
                generator.Emit(OpCodes.Ret);
            }
            else
            {
                generator.Emit(OpCodes.Ret);
            }
        }

        public void MatchConstructorArguments(ILGenerator generator, ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex)
        {
            generator.Emit(OpCodes.Ldarg_0);
            foreach (var par in ctorinf_target.GetParameters())
            {
                if (par.Name.StartsWith("___"))
                {
                    //___ means non public fields
                    string str_fldname = par.Name.Substring(3);
                    var fld_target = targetType.GetField(str_fldname, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fld_target == null)
                        throw new MissingFieldException($"Field with name \"{str_fldname}\" not found! Patch method: {ctorinf_target.DeclaringType.FullName}.{ctorinf_target.Name}");
                    if (fld_target.IsStatic)
                    {
                        generator.Emit((par.ParameterType.IsByRef) ? OpCodes.Ldsflda : OpCodes.Ldsfld, fld_target);
                    }
                    else
                    {
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(par.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fld_target);
                    }
                }
                else if (!processor.MatchConstructorArgs(this, generator, par, paramInfo, paramTypes, ctorinf_target, moduleIndex))
                {
                    //match param by name
                    int index = -1;
                    if (!par.Name.StartsWith("__") || !int.TryParse(par.Name.Substring(2), out index))
                    {
                        for (int j = 0; j < paramInfo.Length; j++)
                        {
                            if (paramInfo[j].Name == par.Name)
                            {
                                if (!par.ParameterType.IsAssignableFrom(paramTypes[j]))
                                {
                                    throw new Exception($"Parameter type does not match: Patch method: {ctorinf_target.DeclaringType.FullName}.{ctorinf_target.Name}, trying to match parameter original {par.Name} with target {paramInfo[j].Name}");
                                }
                                //ParameterBuilder.Position start at 1
                                index = paramInfo[j].Position;
                                break;
                            }
                        }
                    }

                    if (index < 0)
                        throw new ArgumentException($"Parameter \"{par.Name}\" not found! Patch method: {ctorinf_target.DeclaringType.FullName}.{ctorinf_target.Name}\noriginal parameters: {string.Join(", ", paramInfo.Select(p => p.Name))}");
                    if (!paramTypes[index - 1].IsByRef)
                    {
                        generator.LoadArg(index, par.ParameterType.IsByRef);
                    }
                    else
                    {
                        generator.LoadArg(index, true);
                        if (!par.ParameterType.IsByRef)
                        {
                            var opcode = LoadRefAsValue(par.ParameterType, out bool isStruct);
                            if (isStruct)
                            {
                                generator.Emit(opcode, par.ParameterType);
                            }
                            else
                            {
                                generator.Emit(opcode);
                            }
                        }
                    }
                }
            }
            generator.Emit(OpCodes.Newobj, ctorinf_target);
            generator.Emit(OpCodes.Stfld, arr_fldbd_modules[moduleIndex]);
        }

        /// <summary>
        /// Create a List<Instruction> that loads all arguments required to call the method onto stack.
        /// </summary>
        /// <param name="mtdinf_root">The root definition of this method.</param>
        /// <param name="mtdpinf_derived">The override method.</param>
        /// <param name="mtdinf_target">The patch method to be called.</param>
        /// <param name="flddef_module">The injected module field.</param>
        /// <param name="flddef_data">The injected data field.</param>
        /// <param name="module">The assembly's main module.</param>
        /// <param name="itemActionType">The base ItemAction type.</param>
        /// <returns></returns>
        /// <exception cref="MissingFieldException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        private void MatchPatchArguments(ILGenerator generator, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target, bool isPostfix)
        {
            var moduleID = mtdoinf_target.moduleName;
            int moduleIndex = mtdoinf_target.moduleIndex;
            var mtdinf_target = mtdoinf_target.mtdinf_target;
            var mtdinf_root = mtdoinf_target.mtdinf_base.GetBaseDefinition();
            var fldbd_module = arr_fldbd_modules[moduleIndex];
            //Match parameters
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, fldbd_module);
            // module extension methods are static and first arg matches the module type, skip it
            foreach (var par in mtdinf_target.IsStatic ? mtdinf_target.GetParameters().Skip(1) : mtdinf_target.GetParameters())
            {
                if (par.Name.StartsWith("___"))
                {
                    //___ means non public fields
                    string str_fldname = par.Name.Substring(3);
                    var fld_target = AccessTools.Field(targetType, str_fldname);
                    if (fld_target == null)
                        throw new MissingFieldException($"Field with name \"{str_fldname}\" not found! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}");
                    if (fld_target.IsStatic)
                    {
                        generator.Emit((par.ParameterType.IsByRef) ? OpCodes.Ldsflda : OpCodes.Ldsfld, fld_target);
                    }
                    else
                    {
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(par.ParameterType.IsByRef ? OpCodes.Ldflda : OpCodes.Ldfld, fld_target);
                    }
                }
                else if (!MatchPatchSpecialParameters(generator, par, mtdpinf_derived, mtdoinf_target, isPostfix))
                {
                    //match param by name
                    int index = -1;
                    if (!par.Name.StartsWith("__") || !int.TryParse(par.Name.Substring(2), out index))
                    {
                        var rootPars = mtdinf_root.GetParameters();
                        for (int j = 0; j < rootPars.Length; j++)
                        {
                            if (rootPars[j].Name == par.Name)
                            {
                                index = rootPars[j].Position + 1;
                                break;
                            }
                        }
                    }

                    if (index <= 0)
                        throw new ArgumentException($"Parameter \"{par.Name}\" not found! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}\noriginal parameters: {string.Join(", ", mtdinf_root.GetParameters().Select(p => p.Name))}");
                    if (!mtdinf_root.GetParameters()[index - 1].ParameterType.IsByRef)
                    {
                        generator.LoadArg(index, par.ParameterType.IsByRef);
                    }
                    else
                    {
                        generator.LoadArg(index, true);
                        if (!par.ParameterType.IsByRef)
                        {
                            var opcode = LoadRefAsValue(par.ParameterType, out bool isStruct);
                            if (isStruct)
                            {
                                generator.Emit(opcode, par.ParameterType);
                            }
                            else
                            {
                                generator.Emit(opcode);
                            }
                        }
                    }
                }
            }
        }

        private bool MatchPatchSpecialParameters(ILGenerator generator, ParameterInfo par, MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target, bool isPostfix)
        {
            var mtdinf_target = mtdoinf_target.mtdinf_target;
            var dict_states = mtdpinf_derived.States;
            var moduleID = mtdoinf_target.moduleName;
            int moduleIndex = mtdoinf_target.moduleIndex;
            var mtddef_derived = mtdpinf_derived.Method;
            switch (par.Name)
            {
                //load ItemAction instance
                case "__instance":
                    generator.Emit(OpCodes.Ldarg_0);
                    break;
                //load return value
                case "__result":
                    if (!mtddef_derived.ReturnType.IsVoid())
                    {
                        if (par.ParameterType.IsByRef)
                        {
                            generator.Emit(OpCodes.Ldloca_S, 1);
                        }
                        else
                        {
                            generator.Emit(OpCodes.Ldloc_1);
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"{mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name} does not have a return value!");
                    }
                    break;
                case "__runOriginal":
                    if (par.ParameterType.IsByRef)
                        throw new ArgumentException($"__runOriginal is readonly! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}");
                    generator.Emit(OpCodes.Ldloc_0);
                    break;
                case "__state":
                    if (isPostfix && !dict_states.TryGetValue(moduleID, out var lbd_var))
                    {
                        throw new KeyNotFoundException($"__state is found in postfix but not found in corresponding prefix! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}");
                    }
                    if (par.IsOut && isPostfix)
                    {
                        throw new ArgumentException($"__state is marked as out parameter in postfix! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}");
                    }
                    if (!par.IsOut && !isPostfix)
                    {
                        throw new ArgumentException($"__state is not marked as out in prefix! Patch method: {mtdinf_target.DeclaringType.FullName}.{mtdinf_target.Name}");
                    }
                    if (isPostfix)
                    {
                        lbd_var = dict_states[moduleID];
                        dict_states.Remove(moduleID);
                        generator.LoadLocal(lbd_var.LocalIndex);
                    }
                    else
                    {
                        lbd_var = generator.DeclareLocal(par.ParameterType.GetElementType());
                        dict_states.Add(moduleID, lbd_var);
                        generator.LoadLocal(lbd_var.LocalIndex, true);
                    }
                    break;
                default:
                    return processor != null ? processor.MatchSpecialArgs(this, generator, par, mtdpinf_derived, mtdoinf_target) : false;
            }
            return true;
        }

        public void MakeContainerFor(TypeBuilder typebd_container, Type type_interface, Type type_module, out FieldBuilder fldbd_module)
        {
            typebd_container.AddInterfaceImplementation(type_interface.MakeGenericType(type_module));
            fldbd_module = typebd_container.DefineField(ModuleUtils.CreateFieldName(type_module), type_module, System.Reflection.FieldAttributes.Public);
            var propbd_instance = typebd_container.DefineProperty("Instance", System.Reflection.PropertyAttributes.None, type_module, Type.EmptyTypes);
            var mtdbd_instance = typebd_container.DefineMethod("get_Instance", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, type_module, Type.EmptyTypes);
            var il = mtdbd_instance.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fldbd_module);
            il.Emit(OpCodes.Ret);
            typebd_container.DefineMethodOverride(mtdbd_instance, typeof(IModuleContainerFor<>).MakeGenericType(type_module).GetMethod("get_Instance"));
            propbd_instance.SetGetMethod(mtdbd_instance);
        }

        public static OpCode LoadRefAsValue(Type type, out bool isStruct)
        {
            isStruct = false;
            if (type.IsClass())
            {
                return OpCodes.Ldind_Ref;
            }
            if (type.IsPointer)
            {
                return OpCodes.Ldind_I;
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                    return OpCodes.Ldind_I1;
                case TypeCode.Byte:
                    return OpCodes.Ldind_U1;
                case TypeCode.Int16:
                    return OpCodes.Ldind_I2;
                case TypeCode.UInt16:
                    return OpCodes.Ldind_U2;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                    return OpCodes.Ldind_I4;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return OpCodes.Ldind_I8;
                case TypeCode.Single:
                    return OpCodes.Ldind_R4;
                case TypeCode.Double:
                    return OpCodes.Ldind_R8;
            }
            isStruct = true;
            return OpCodes.Ldobj;
        }
    }
}

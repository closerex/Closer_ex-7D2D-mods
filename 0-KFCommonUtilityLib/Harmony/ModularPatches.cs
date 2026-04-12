using HarmonyLib;
using HarmonyLib.Public.Patching;
using Mono.Collections.Generic;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UniLinq;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ModularPatches
    {
        [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_parseItem_ItemClassesFromXml(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var ctor_itemclass = AccessTools.Constructor(typeof(ItemClass));
            var mtd_createinstance = AccessTools.Method(typeof(Activator), nameof(Activator.CreateInstance), new[] { typeof(Type) });
            var mtd_concat3 = AccessTools.Method(typeof(string), nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string) });

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj && ctor_itemclass.Equals(codes[i].operand))
                {
                    codes.InsertRange(i + 1, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1).WithLabels(codes[i].ExtractLabels()),
                        CodeInstruction.CallClosure<Func<DynamicProperties, ItemClass>>(static (props) =>
                        {
                            if (props.Values.TryGetValue("ItemClassModules", out string modules) && ModuleManagers.PatchType<ItemClassModuleProcessor>(typeof(ItemClass), typeof(ItemClass), null, modules, out Type result))
                            {
                                return (ItemClass)Activator.CreateInstance(result);
                            }
                            return new();
                        })
                    });
                    codes.RemoveAt(i);
                    i++;
                }
                else if (codes[i].Calls(mtd_createinstance) && codes[i + 1].opcode == OpCodes.Castclass)
                {
                    if (typeof(ItemClass).Equals(codes[i + 1].operand))
                    {
                        codes.InsertRange(i, new CodeInstruction[]
                        {
                            new(OpCodes.Ldloc_1),
                            CodeInstruction.CallClosure<Func<Type, DynamicProperties, Type>>(static (originalType, props) =>
                            {
                                if (props.Values.TryGetValue("ItemClassModules", out string modules) && ModuleManagers.PatchType<ItemClassModuleProcessor>(originalType, typeof(ItemClass), null, modules, out Type result))
                                {
                                    return result;
                                }
                                return originalType;
                            })
                        });
                        i += 2;

                        for (int j = i + 1; j < codes.Count; j++)
                        {
                            if (codes[j].opcode == OpCodes.Pop)
                            {
                                codes[j].opcode = OpCodes.Callvirt;
                                codes[j].operand = AccessTools.Method(typeof(Exception), nameof(Exception.ToString), Type.EmptyTypes);

                                for (int k = j + 1; k < codes.Count; k++)
                                {
                                    if (codes[k].Calls(mtd_concat3))
                                    {
                                        codes[k] = CodeInstruction.Call(typeof(string), nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string), typeof(string) });
                                        break;
                                    }
                                }
                                break;
                            }
                        }

                    }
                    else if (typeof(ItemAction).Equals(codes[i + 1].operand))
                    {
                        codes.InsertRange(i, new CodeInstruction[]
                        {
                            new(OpCodes.Ldloc_1),
                            new(OpCodes.Ldloc_S, ((LocalBuilder)codes[i - 2].operand).LocalIndex - 2),
                            CodeInstruction.CallClosure<Func<Type, DynamicProperties, string, Type>>(static (originalType, props, actionName) =>
                            {
                                actionName += ".ItemActionModules";
                                if (props.Values.TryGetValue(actionName, out string modules) && ModuleManagers.PatchType<ItemActionModuleProcessor>(originalType, typeof(ItemAction), null, modules, out Type result))
                                {
                                    return result;
                                }
                                return originalType;
                            })
                        });
                        i += 3;

                        for (int j = i + 1; j < codes.Count; j++)
                        {
                            if (codes[j].opcode == OpCodes.Pop)
                            {
                                codes[j].opcode = OpCodes.Callvirt;
                                codes[j].operand = AccessTools.Method(typeof(Exception), nameof(Exception.ToString), Type.EmptyTypes);

                                for (int k = j + 1; k < codes.Count; k++)
                                {
                                    if (codes[k].Calls(mtd_concat3))
                                    {
                                        codes[k] = CodeInstruction.Call(typeof(string), nameof(string.Concat), new[] { typeof(string), typeof(string), typeof(string), typeof(string) });
                                        break;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Disconnect))]
        [HarmonyPostfix]
        private static void Postfix_Disconnect_GameManager()
        {
            ModuleManagers.Cleanup();
        }

        [HarmonyPatch(typeof(PatchManager), "GetRealMethod")]
        [HarmonyReversePatch]
        public static MethodBase GetRealMethod(MethodInfo method, bool useReplacement)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (instructions == null)
                {
                    return null;
                }
                return new CodeInstruction[]
                {
                    CodeInstruction.LoadField(typeof(PatchManager), "ReplacementToOriginals"),
                    CodeInstruction.LoadField(typeof(PatchManager), "ReplacementToOriginalsMono"),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(ModularPatches), nameof(ModularPatches.GetPatched)),
                    new CodeInstruction(OpCodes.Ret)
                };
            }
            _ = Transpiler(null);
            return null;
        }

        private static MethodBase GetPatched(ConditionalWeakTable<MethodBase, MethodBase> ReplacementToOriginals, Dictionary<long, MethodBase[]> ReplacementToOriginalsMono, MethodInfo method)
        {
            MethodInfo methodInfo = method.Identifiable();
            ConditionalWeakTable<MethodBase, MethodBase> replacementToOriginals = ReplacementToOriginals;
            lock (replacementToOriginals)
            {
                foreach (var pair in replacementToOriginals)
                {
                    if (pair.Value == method)
                    {
                        Log.Out($"Found method replacement {pair.Key.FullDescription()} for method {method.FullDescription()}");
                        return pair.Key;
                    }
                }
            }
            if (AccessTools.IsMonoRuntime)
            {
                long num = (long)method.MethodHandle.GetFunctionPointer();
                Dictionary<long, MethodBase[]> replacementToOriginalsMono = ReplacementToOriginalsMono;
                lock (replacementToOriginalsMono)
                {
                    foreach (var pair in replacementToOriginalsMono)
                    {
                        if (pair.Value[0] == method)
                        {
                            Log.Out($"Found MONO method replacement {pair.Value[1].FullDescription()} for method {method.FullDescription()}");
                            return pair.Value[1];
                        }
                    }
                }
            }
            return method;
        }

        [HarmonyPatch(typeof(PatchManager), nameof(PatchManager.ToPatchInfo))]
        [HarmonyReversePatch]
        public static PatchInfo ToPatchInfoDontAdd(this MethodBase methodBase)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (instructions == null)
                {
                    return null;
                }

                var codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Dup)
                    {
                        codes[i - 1].WithLabels(codes[i - 3].ExtractLabels());
                        codes.RemoveAt(i + 2);
                        codes.RemoveAt(i);
                        codes.RemoveRange(i - 3, 2);
                        break;
                    }
                }
                return codes;
            }
            _ = Transpiler(null);
            return null;
        }

        [HarmonyPatch(typeof(PatchInfo), "AddTranspilers")]
        [HarmonyReversePatch]
        public static void AddTranspilers(this PatchInfo self, string owner, params HarmonyMethod[] methods)
        { 
        }

        public static PatchInfo Copy(this PatchInfo self)
        {
            var res = new PatchInfo();
            res.prefixes = new Patch[self.prefixes.Length];
            res.postfixes = new Patch[self.postfixes.Length];
            res.transpilers = new Patch[self.transpilers.Length];
            res.finalizers = new Patch[self.finalizers.Length];
            res.ilmanipulators = new Patch[self.ilmanipulators.Length];
            Array.Copy(self.prefixes, res.prefixes, res.prefixes.Length);
            Array.Copy(self.postfixes, res.postfixes, res.postfixes.Length);
            Array.Copy(self.transpilers, res.transpilers, res.transpilers.Length);
            Array.Copy(self.finalizers, res.finalizers, res.finalizers.Length);
            Array.Copy(self.ilmanipulators, res.ilmanipulators, res.ilmanipulators.Length);
            return res;
        }

        [HarmonyPatch(typeof(PatchInfo), "AddILManipulators")]
        [HarmonyReversePatch]
        public static void AddILManipulators(this PatchInfo self, string owner, params HarmonyMethod[] methods)
        {
        }

        [HarmonyPatch(typeof(HarmonyManipulator), "ApplyManipulators")]
        [HarmonyReversePatch]
        public static void ApplyManipulators(ILContext ctx, MethodBase original, List<MethodInfo> ilManipulators, object retLabel)
        {
        }

        public static void LoadArg(this ILGenerator self, int index, bool useAddress = false)
        {
            if (useAddress)
            {
                if (index < 256)
                {
                    self.Emit(OpCodes.Ldarga_S, Convert.ToByte(index));
                }
                else
                {
                    self.Emit(OpCodes.Ldarga, index);
                }
            }
            else
            {
                switch (index)
                {
                    case 0:
                        self.Emit(OpCodes.Ldarg_0);
                        break;
                    case 1:
                        self.Emit(OpCodes.Ldarg_1);
                        break;
                    case 2:
                        self.Emit(OpCodes.Ldarg_2);
                        break;
                    case 3:
                        self.Emit(OpCodes.Ldarg_3);
                        break;
                    default:
                        if (index < 256)
                        {
                            self.Emit(OpCodes.Ldarg_S, Convert.ToByte(index));
                        }
                        else
                        {
                            self.Emit(OpCodes.Ldarg, index);
                        }
                        break;
                }
            }
        }

        public static void LoadLocal(this ILGenerator self, int index, bool useAddress = false)
        {
            if (useAddress)
            {
                if (index < 256)
                {
                    self.Emit(OpCodes.Ldloca_S, Convert.ToByte(index));
                }
                else
                {
                    self.Emit(OpCodes.Ldloc, index);
                }
            }
            else
            {
                switch (index)
                {
                    case 0:
                        self.Emit(OpCodes.Ldloc_0);
                        break;
                    case 1:
                        self.Emit(OpCodes.Ldloc_1);
                        break;
                    case 2:
                        self.Emit(OpCodes.Ldloc_2);
                        break;
                    case 3:
                        self.Emit(OpCodes.Ldloc_3);
                        break;
                    default:
                        if (index < 256)
                        {
                            self.Emit(OpCodes.Ldloc_S, Convert.ToByte(index));
                        }
                        else
                        {
                            self.Emit(OpCodes.Ldloc, index);
                        }
                        break;
                }
            }
        }

        //public static MethodBase GetOriginalMethod(this HarmonyMethod attr)
        //{
        //    try
        //    {
        //        MethodType? methodType = attr.methodType;
        //        if (methodType != null)
        //        {
        //            switch (methodType.GetValueOrDefault())
        //            {
        //                case MethodType.Normal:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes, null);
        //                case MethodType.Getter:
        //                    {
        //                        if (attr.methodName == null)
        //                        {
        //                            PropertyInfo propertyInfo = AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes);
        //                            return (propertyInfo != null) ? propertyInfo.GetGetMethod(true) : null;
        //                        }
        //                        PropertyInfo propertyInfo2 = AccessTools.DeclaredProperty(attr.declaringType, attr.methodName);
        //                        return (propertyInfo2 != null) ? propertyInfo2.GetGetMethod(true) : null;
        //                    }
        //                case MethodType.Setter:
        //                    {
        //                        if (attr.methodName == null)
        //                        {
        //                            PropertyInfo propertyInfo3 = AccessTools.DeclaredIndexer(attr.declaringType, attr.argumentTypes);
        //                            return (propertyInfo3 != null) ? propertyInfo3.GetSetMethod(true) : null;
        //                        }
        //                        PropertyInfo propertyInfo4 = AccessTools.DeclaredProperty(attr.declaringType, attr.methodName);
        //                        return (propertyInfo4 != null) ? propertyInfo4.GetSetMethod(true) : null;
        //                    }
        //                case MethodType.Constructor:
        //                    return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes, false);
        //                case MethodType.StaticConstructor:
        //                    return AccessTools.GetDeclaredConstructors(attr.declaringType, null).FirstOrDefault((ConstructorInfo c) => c.IsStatic);
        //                case MethodType.Enumerator:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.EnumeratorMoveNext(AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes, null));
        //                case MethodType.Async:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.AsyncMoveNext(AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes, null));
        //            }
        //        }
        //    }
        //    catch (AmbiguousMatchException ex)
        //    {
        //        throw new Exception("Ambiguous match for HarmonyMethod[" + attr.ToString() + "]", ex.InnerException ?? ex);
        //    }
        //    return null;
        //}

        //public static MethodBase GetBaseMethod(this HarmonyMethod attr)
        //{
        //    try
        //    {
        //        MethodType? methodType = attr.methodType;
        //        if (methodType != null)
        //        {
        //            switch (methodType.GetValueOrDefault())
        //            {
        //                case MethodType.Normal:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.Method(attr.declaringType, attr.methodName, attr.argumentTypes, null);
        //                case MethodType.Getter:
        //                    {
        //                        if (attr.methodName == null)
        //                        {
        //                            PropertyInfo propertyInfo = AccessTools.Indexer(attr.declaringType, attr.argumentTypes);
        //                            return (propertyInfo != null) ? propertyInfo.GetGetMethod(true) : null;
        //                        }
        //                        PropertyInfo propertyInfo2 = AccessTools.Property(attr.declaringType, attr.methodName);
        //                        return (propertyInfo2 != null) ? propertyInfo2.GetGetMethod(true) : null;
        //                    }
        //                case MethodType.Setter:
        //                    {
        //                        if (attr.methodName == null)
        //                        {
        //                            PropertyInfo propertyInfo3 = AccessTools.Indexer(attr.declaringType, attr.argumentTypes);
        //                            return (propertyInfo3 != null) ? propertyInfo3.GetSetMethod(true) : null;
        //                        }
        //                        PropertyInfo propertyInfo4 = AccessTools.Property(attr.declaringType, attr.methodName);
        //                        return (propertyInfo4 != null) ? propertyInfo4.GetSetMethod(true) : null;
        //                    }
        //                case MethodType.Constructor:
        //                    return AccessTools.Constructor(attr.declaringType, attr.argumentTypes, false);
        //                case MethodType.StaticConstructor:
        //                    return AccessTools.GetDeclaredConstructors(attr.declaringType, null).FirstOrDefault((ConstructorInfo c) => c.IsStatic);
        //                case MethodType.Enumerator:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.EnumeratorMoveNext(AccessTools.Method(attr.declaringType, attr.methodName, attr.argumentTypes, null));
        //                case MethodType.Async:
        //                    if (attr.methodName == null)
        //                    {
        //                        return null;
        //                    }
        //                    return AccessTools.AsyncMoveNext(AccessTools.Method(attr.declaringType, attr.methodName, attr.argumentTypes, null));
        //            }
        //        }
        //    }
        //    catch (AmbiguousMatchException ex)
        //    {
        //        throw new Exception("Ambiguous match for HarmonyMethod[" + attr.ToString() + "]", ex.InnerException ?? ex);
        //    }
        //    return null;
        //}
    }

    [HarmonyPatch]
    public static class PatchToolsPatches
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(AccessTools.TypeByName("HarmonyLib.PatchTools"), "GetOriginalMethod");
        }

        [HarmonyReversePatch]
        public static MethodBase GetOriginalMethod(this HarmonyMethod attr)
        {
            return null;
        }

        [HarmonyReversePatch]
        public static MethodBase GetBaseMethod(this HarmonyMethod attr)
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (instructions == null)
                    return null;
                return instructions.MethodReplacer(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.DeclaredMethod), new[] { typeof(Type), typeof(string), typeof(Type[]), typeof(Type[]) }),
                                                   AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Method), new[] { typeof(Type), typeof(string), typeof(Type[]), typeof(Type[]) }))
                                   .MethodReplacer(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.DeclaredIndexer)),
                                                   AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Indexer)))
                                   .MethodReplacer(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.DeclaredProperty), new[] { typeof(Type), typeof(string) }),
                                                   AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Property), new[] { typeof(Type), typeof(string) }))
                                   .MethodReplacer(AccessTools.Method(typeof(AccessTools), nameof(AccessTools.DeclaredConstructor)),
                                                   AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Constructor)));
            }
            _ = Transpiler(null);
            return null;
        }
    }

    [HarmonyPatch]
    public static class PatchFunctionPatches
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(AccessTools.TypeByName("HarmonyLib.PatchFunctions"), "UpdateWrapper");
        }

        [HarmonyReversePatch]
        public static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
        {
            return null;
        }
    }

    [HarmonyPatch]
    public static class MonoModPatches
    {
        private static MethodAttributes currentAttributes;
        private static CallingConventions currentCallingConventions;
        private static bool isStatic;
        private static ConstructorBuilder currentConstructorBuilder;

        //[HarmonyPatch(typeof(TypeBuilder), nameof(TypeBuilder.DefineMethod), new []{typeof(string), typeof(MethodAttributes), typeof(CallingConventions), typeof(Type), typeof(Type[]), typeof(Type[]), typeof(Type[]), typeof(Type[][]), typeof(Type[][])})]
        //[HarmonyPrefix]
        //private static void DebugLog(string name, MethodAttributes attributes, CallingConventions callingConvention, Type returnType, Type[] returnTypeRequiredCustomModifiers, Type[] returnTypeOptionalCustomModifiers, Type[] parameterTypes, Type[][] parameterTypeRequiredCustomModifiers, Type[][] parameterTypeOptionalCustomModifiers)
        //{
        //    Log.Out($"Define method {name} with {attributes} and {callingConvention},\nreturn type {returnType?.FullDescription() ?? "null"}\nreturn type req mod {returnTypeRequiredCustomModifiers?.Length.ToString() ?? "null"}\nreturn type opt mod {returnTypeOptionalCustomModifiers?.Length.ToString() ?? "null"}\nparams {parameterTypes.Join(t => t.FullDescription())}\nparam req mod {parameterTypeRequiredCustomModifiers?.Join(arr => arr?.Join(t => t?.FullDescription() ?? "null") ?? "null", " ") ?? "null"}\nparam opt mod {parameterTypeOptionalCustomModifiers?.Join(arr => arr?.Join(t => t?.FullDescription() ?? "null") ?? "null", " ") ?? "null"}");
        //}

#nullable enable
        public static MethodBuilder GenerateMethodBuilderWithAttributes(DynamicMethodDefinition dmd, TypeBuilder? typeBuilder, MethodAttributes attributes, CallingConventions callingConventions)
#nullable disable
        {
            currentAttributes = attributes;
            currentCallingConventions = callingConventions;
            isStatic = !callingConventions.HasFlag(CallingConventions.HasThis);
            return GenerateMethodBuilderOverride(dmd, typeBuilder);
        }

#nullable enable
        public static void GenerateConstructorBuilderWithAttributes(DynamicMethodDefinition dmd, TypeBuilder? typeBuilder, MethodAttributes attributes, CallingConventions callingConventions, out ConstructorBuilder ctorbd)
#nullable disable
        {
            ctorbd = null;
            currentAttributes = attributes;
            currentCallingConventions = callingConventions;
            isStatic = !callingConventions.HasFlag(CallingConventions.HasThis);
            GenerateConstructorBuilder(dmd, typeBuilder);
            ctorbd = currentConstructorBuilder;
        }

        public static bool Equals(this Mono.Cecil.MethodReference self, Mono.Cecil.MethodReference other)
        {
            try
            {
                return (self == null && other == null) ||
                       (self != null && other != null &&
                           ((self is DynamicMethodReference dself && other is DynamicMethodReference dother && dself.DynamicMethod.Equals(dother.DynamicMethod)) ||
                            (self is not DynamicMethodReference && other is not DynamicMethodReference && MethodReferenceComparePatch.MethodReferenceCompare(self, other))
                           )
                       );
            }
            catch (Exception e)
            {
                Log.Error($"failed comparing {self.GetType().FullDescription()} {self.Name} with {other.GetType().FullDescription()} {other.Name}");
                throw e;
            }
        }

        [HarmonyPatch]
        private static class MethodReferenceComparePatch
        {
            private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Mono.Cecil.MethodReferenceComparer"), "AreEqual");

            [HarmonyReversePatch]
            public static bool MethodReferenceCompare(Mono.Cecil.MethodReference x, Mono.Cecil.MethodReference y)
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(DMDEmitMethodBuilderGenerator), nameof(DMDEmitMethodBuilderGenerator.GenerateMethodBuilder))]
        [HarmonyReversePatch]
#nullable enable
        public static MethodBuilder GenerateMethodBuilder(this DynamicMethodDefinition dmd, TypeBuilder? typeBuilder)
#nullable disable
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (instructions == null)
                    return null;

                var codes = instructions.ToList();
                ProcessParameterModifiers(codes);
                return codes;
            }

            _ = Transpiler(null);
            return null;
        }

        [HarmonyPatch(typeof(DMDEmitMethodBuilderGenerator), nameof(DMDEmitMethodBuilderGenerator.GenerateMethodBuilder))]
        [HarmonyReversePatch]
#nullable enable
        private static MethodBuilder GenerateMethodBuilderOverride(DynamicMethodDefinition dmd, TypeBuilder? typeBuilder)
#nullable disable
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (instructions == null)
                    return null;

                var codes = instructions.ToList();
                ProcessMethodAttributes(codes);
                ProcessParameterModifiers(codes);
                return codes;
            }

            _ = Transpiler(null);
            return null;
        }
        
        [HarmonyPatch(typeof(DMDEmitMethodBuilderGenerator), nameof(DMDEmitMethodBuilderGenerator.GenerateMethodBuilder))]
        [HarmonyReversePatch]
#nullable enable
        private static MethodBuilder GenerateConstructorBuilder(DynamicMethodDefinition dmd, TypeBuilder? typeBuilder)
#nullable disable
        {
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                if (instructions == null || generator == null)
                    return null;

                var codes = instructions.ToList();
                ProcessMethodAttributes(codes);
                ProcessConstructorBuilder(codes, generator);
                ProcessParameterModifiers(codes);
                return codes;
            }

            _ = Transpiler(null, null);
            return null;
        }

        //add _DMDEmit patch to handle parameters offset
        [HarmonyPatch]
        private static class DMDEmit_GeneratePatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "Generate");
            }

            [HarmonyReversePatch]
            public static void Generate(DynamicMethodDefinition dmd, MethodBase _mb, ILGenerator il)
            {
                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
                {
                    if (instructions == null || generator == null)
                        return null;
                    var codes = instructions.ToList();

                    var prop_pars = AccessTools.PropertyGetter(typeof(Mono.Cecil.MethodReference), nameof(Mono.Cecil.MethodReference.Parameters));
                    var prop_index = AccessTools.PropertyGetter(typeof(Mono.Cecil.ParameterReference), nameof(Mono.Cecil.ParameterReference.Index));
                    var prop_hasthis = AccessTools.PropertyGetter(typeof(Mono.Cecil.MethodReference), nameof(Mono.Cecil.MethodReference.HasThis));

                    LocalBuilder lbd_par_offset = null;
                    for (int i = 0; i < codes.Count - 1; i++)
                    {
                        if (codes[i].Calls(prop_pars))
                        {
                            for (int j = i + 1; j < codes.Count; j++)
                            {
                                if (codes[j].opcode == OpCodes.Br_S)
                                {
                                    //assume dmd is always static, target method can be instanced of static
                                    //skip dmd par 0 if target method is instanced
                                    var lbl = generator.DefineLabel();
                                    codes[j].WithLabels(lbl);
                                    codes.InsertRange(j, new[]
                                    {
                                        CodeInstruction.LoadField(typeof(MonoModPatches), nameof(MonoModPatches.isStatic)),
                                        new CodeInstruction(OpCodes.Brtrue_S, lbl),
                                        new CodeInstruction(OpCodes.Ldloc_S, codes[i + 2].operand),
                                        CodeInstruction.Call(typeof(Collection<Mono.Cecil.ParameterDefinition>.Enumerator), nameof(Collection<Mono.Cecil.ParameterDefinition>.Enumerator.MoveNext)),
                                        new CodeInstruction(OpCodes.Pop)
                                    });
                                    i = j + 5;
                                    break;
                                }
                            }
                        }
                        //if target method is instanced => skip dmd par 0, define dmd par index 1 as target par index 1, index offset is 0
                        //if target method is static => keep dmd par 0, define dmd par index 0 as target par index 1, index offset is 1
                        else if (codes[i].Calls(prop_index))
                        {
                            codes[i + 1].opcode = OpCodes.Ldsfld;
                            codes[i + 1].operand = AccessTools.Field(typeof(MonoModPatches), nameof(MonoModPatches.isStatic));
                        }
                        else if (codes[i].Calls(prop_hasthis))
                        {
                            codes[i + 1].WithLabels(codes[i - 1].ExtractLabels());
                            codes.RemoveAt(i + 2);
                            codes.RemoveRange(i - 1, 2);
                            i -= 2;
                        }
                        else if (lbd_par_offset != null && codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == lbd_par_offset.LocalIndex && codes[i + 1].opcode == OpCodes.Add)
                        {
                            codes[i + 1].opcode = OpCodes.Sub;
                        }
                    }

                    return codes;
                }

                _ = Transpiler(null, null);
            }
        }

        [HarmonyPatch]
        private static class DMDEmit_ResolveWithModifiersPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "ResolveWithModifiers");
            }

            [HarmonyReversePatch]
            public static void ResolveWithModifiers(Mono.Cecil.TypeReference typeRef, out Type type, out Type[] typeModReq, out Type[] typeModOpt, List<Type> modReq = null, List<Type> modOpt = null)
            {
                IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                {
                    if (instructions == null)
                    {
                        return null;
                    }
                    var codes = instructions.ToList();

                    var mtd_toarray = AccessTools.Method(typeof(List<Type>), nameof(List<Type>.ToArray), Type.EmptyTypes);

                    for (int i = 0; i < codes.Count; i++)
                    {
                        if (codes[i].Calls(mtd_toarray))
                        {
                            codes.Insert(i + 1, CodeInstruction.CallClosure<Func<Type[], Type[]>>(arr =>
                            {
                                if (arr.Length == 0)
                                {
                                    return null;
                                }
                                return arr;
                            }));
                            i++;
                        }
                    }

                    return codes;
                }

                _ = Transpiler(null);
                type = null;
                typeModReq = null;
                typeModOpt = null;
            }
        }

        private static void ProcessParameterModifiers(List<CodeInstruction> codes)
        {
            var fld_emptytypes = AccessTools.Field(typeof(Type), nameof(Type.EmptyTypes));
            var mtd_getrequired = AccessTools.Method(typeof(ParameterInfo), nameof(ParameterInfo.GetRequiredCustomModifiers));
            var mtd_getoptional = AccessTools.Method(typeof(ParameterInfo), nameof(ParameterInfo.GetOptionalCustomModifiers));
            var mtd_resolvemod = AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "ResolveWithModifiers");
            var mtd_fixedresolve = AccessTools.Method(typeof(DMDEmit_ResolveWithModifiersPatch), nameof(DMDEmit_ResolveWithModifiersPatch.ResolveWithModifiers));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_emptytypes))
                {
                    codes[i].opcode = OpCodes.Ldnull;
                    codes[i].operand = null;
                }
                else if (codes[i].Calls(mtd_getrequired) || codes[i].Calls(mtd_getoptional))
                {
                    codes.Insert(i + 1, CodeInstruction.CallClosure<Func<Type[], Type[]>>(arr =>
                    {
                        if (arr.Length == 0)
                        {
                            return null;
                        }
                        return arr;
                    }));
                    i++;
                }
                else if (codes[i].Calls(mtd_resolvemod))
                {
                    codes[i].operand = mtd_fixedresolve;
                }
            }
        }

        private static void ProcessMethodAttributes(List<CodeInstruction> codes)
        {
            var prop_static = AccessTools.PropertyGetter(typeof(MethodBase), nameof(MethodBase.IsStatic));
            var prop_hasthis = AccessTools.PropertyGetter(typeof(Mono.Cecil.MethodDefinition), nameof(Mono.Cecil.MethodDefinition.IsStatic));
            var prop_count = AccessTools.PropertyGetter(typeof(Collection<Mono.Cecil.ParameterDefinition>), nameof(Collection<Mono.Cecil.ParameterDefinition>.Count));
            var mtd_getthis = AccessTools.Method(typeof(MonoMod.Utils.Extensions), nameof(MonoMod.Utils.Extensions.GetThisParamType));
            var mtd_resolve = AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "ResolveWithModifiers");
            var mtd_generate = AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "Generate");
            var mtd_resolveref = AccessTools.Method(typeof(ReflectionHelper), nameof(ReflectionHelper.ResolveReflection), new[] { typeof(Mono.Cecil.TypeReference) });
            var fld_emptytypes = AccessTools.Field(typeof(Type), nameof(Type.EmptyTypes));

            LocalBuilder lbd_par_offset = null;
            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].Calls(prop_static) || codes[i].Calls(prop_hasthis))
                {
                    codes[i + 1].opcode = OpCodes.Brtrue_S;
                    codes.RemoveAt(i);
                    codes[i - 1].opcode = OpCodes.Ldsfld;
                    codes[i - 1].operand = AccessTools.Field(typeof(MonoModPatches), nameof(isStatic));
                    i--;
                    int removeStart = -1, removeEnd = -1;
                    //assuming dmd definition is always static and target method could be instanced
                    //when target method is instanced, set parameter count to dmd parameter count - 1
                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].opcode == OpCodes.Br_S && removeEnd < 0)
                        {
                            removeEnd = j;
                            break;
                        }
                        else if (codes[j].opcode == OpCodes.Add && codes[j - 1].opcode == OpCodes.Ldc_I4_1)
                        {
                            if (codes[j - 2].Calls(prop_count) || codes[j - 2].opcode == OpCodes.Conv_I4)
                            {
                                codes[j].opcode = OpCodes.Sub;
                            }
                            else if (codes[j - 2].opcode == OpCodes.Ldloc_S)
                            {
                                lbd_par_offset = (LocalBuilder)codes[j - 2].operand;
                            }
                        }
                        else if (codes[j].Calls(mtd_getthis) && removeStart < 0)
                        {
                            removeStart = j - 3;
                        }
                        else if (codes[j].Calls(mtd_resolveref) && removeStart < 0)
                        {
                            removeStart = j - 2;
                        }
                    }
                    if (removeStart >= 0 && removeEnd >= 0)
                    {
                        codes.RemoveRange(removeStart, removeEnd - removeStart);
                    }
                }
                else if (codes[i].LoadsConstant(150))
                {
                    codes[i].opcode = OpCodes.Ldsfld;
                    codes[i].operand = AccessTools.Field(typeof(MonoModPatches), nameof(currentAttributes));
                    codes[i + 1].opcode = OpCodes.Ldsfld;
                    codes[i + 1].operand = AccessTools.Field(typeof(MonoModPatches), nameof(currentCallingConventions));
                    break;
                }
                //change target method resolve offset
                else if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 22 && codes[i + 1].opcode == OpCodes.Br_S)
                {
                    //instanced method start at param 1
                    codes[i - 1].opcode = OpCodes.Ldloc_S;
                    codes[i - 1].operand = lbd_par_offset;
                    //set array index back
                    for (int j = i + 2; j < codes.Count - 1; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[j].operand).LocalIndex == lbd_par_offset.LocalIndex && codes[j + 1].opcode == OpCodes.Add)
                        {
                            codes[j + 1].opcode = OpCodes.Sub;
                        }
                    }
                }
                else if (codes[i].Calls(mtd_generate))
                {
                    codes[i].operand = AccessTools.Method(typeof(DMDEmit_GeneratePatch), nameof(DMDEmit_GeneratePatch.Generate));
                }
            }
        }

        private static void ProcessConstructorBuilder(List<CodeInstruction> codes, ILGenerator generator)
        {
            var prop_returntype = AccessTools.PropertyGetter(typeof(Mono.Cecil.MethodReference), nameof(Mono.Cecil.MethodReference.ReturnType));
            var mtd_resolve = AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "ResolveWithModifiers");
            var mtd_definemtd = AccessTools.Method(typeof(TypeBuilder), nameof(TypeBuilder.DefineMethod), new[] { typeof(string), typeof(MethodAttributes), typeof(CallingConventions), typeof(Type), typeof(Type[]), typeof(Type[]), typeof(Type[]), typeof(Type[][]), typeof(Type[][]) });
            var mtd_getilgen = AccessTools.Method(typeof(MethodBuilder), nameof(MethodBuilder.GetILGenerator));
            var mtd_generate = AccessTools.Method(AccessTools.TypeByName("MonoMod.Utils._DMDEmit"), "Generate");
            var mtd_generate_patched = AccessTools.Method(typeof(DMDEmit_GeneratePatch), nameof(DMDEmit_GeneratePatch.Generate));

            var lbd_ctor = generator.DeclareLocal(typeof(ConstructorBuilder));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(prop_returntype))
                {
                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].Calls(mtd_resolve))
                        {
                            codes[j + 1].WithLabels(codes[i - 1].ExtractLabels());
                            codes.RemoveRange(i - 1, j - i + 2);
                            i = i - 1;
                            break;
                        }
                    }
                }
                else if (codes[i].Calls(mtd_definemtd))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldnull),
                        new CodeInstruction(OpCodes.Stloc_S, codes[i + 1].operand),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_ctor),
                        CodeInstruction.StoreField(typeof(MonoModPatches), nameof(MonoModPatches.currentConstructorBuilder))
                    });
                    codes[i].operand = AccessTools.Method(typeof(TypeBuilder), nameof(TypeBuilder.DefineConstructor), new[] { typeof(MethodAttributes), typeof(CallingConventions), typeof(Type[]), typeof(Type[][]), typeof(Type[][]) });
                    codes[i + 1].operand = lbd_ctor;
                    codes.RemoveRange(i - 6, 3);
                    codes.Insert(i - 8, new CodeInstruction(OpCodes.Pop).WithLabels(codes[i - 8].ExtractLabels()));
                    i--;
                }
                else if (codes[i].Calls(mtd_getilgen))
                {
                    codes[i - 1].operand = lbd_ctor;
                }
                else if (codes[i].Calls(mtd_generate) || codes[i].Calls(mtd_generate_patched))
                {
                    codes[i - 2].operand = lbd_ctor;
                }
            }
        }
    }

    [HarmonyPatchCategory("CallClosureFix")]
    [HarmonyPatch]
    public static class CallClosureFixPatches
    {
        private static Func<MethodBuilder, Type[]> GetDefinedParameterTypes;
        static CallClosureFixPatches()
        {
            var dynamicMethod = new DynamicMethod("GetDefinedParameterTypes", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(Type[]), new[] { typeof(MethodBuilder) }, typeof(MethodBuilder), true);
            var generator = dynamicMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(MethodBuilder), "parameters"));
            generator.Emit(OpCodes.Ret);

            GetDefinedParameterTypes = (Func<MethodBuilder, Type[]>)dynamicMethod.CreateDelegate(typeof(Func<MethodBuilder, Type[]>));
        }

#nullable enable
        private static TypeBuilder? WorkingTypeBuilder;
#nullable disable
        private static readonly Dictionary<Mono.Cecil.MethodReference, DynamicMethodReference> DelegateReplacers = new();

        [HarmonyPatch(typeof(DynamicMethodDefinition), nameof(DynamicMethodDefinition.Generate), new Type[0])]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Generate_DynamicMethodDefinition(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_generate = AccessTools.Method(typeof(DynamicMethodDefinition), nameof(DynamicMethodDefinition.Generate), new[] { typeof(object) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_generate))
                {
                    codes.InsertRange(i + 1, new CodeInstruction[]
                    {
                        new(OpCodes.Dup),
                        new(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(CallClosureFixPatches), nameof(WorkingTypeBuilder)),
                        CodeInstruction.Call(typeof(MonoModPatches), nameof(MonoModPatches.GenerateMethodBuilder)),
                        new(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(CallClosureFixPatches), nameof(AddReplacer))
                    });
                    break;
                }
            }
            return codes;
        }

        private static void AddReplacer(MethodInfo dele, MethodBuilder replacer, DynamicMethodDefinition dmd)
        {
            DelegateReplacers.Add(dmd.Module.ImportReference(dele), new(dmd.Module, replacer));
        }

        public static void ApplyFix(TypeBuilder typeBuilder)
        {
            WorkingTypeBuilder = typeBuilder;
            DelegateReplacers.Clear();
            CommonUtilityLibInit.HarmonyInstance.PatchCategory(Assembly.GetExecutingAssembly(), "CallClosureFix");
        }

        public static void RemoveFix()
        {
            CommonUtilityLibInit.HarmonyInstance.UnpatchCategory(Assembly.GetExecutingAssembly(), "CallClosureFix");
            WorkingTypeBuilder = null;
            DelegateReplacers.Clear();
        }

        public static void DelegateManipulator(ILContext il)
        {
            foreach (var ins in il.Body.Instructions)
            {
                if (ins.OpCode == Mono.Cecil.Cil.OpCodes.Call && ins.Operand is Mono.Cecil.MethodReference mtdref)
                {
                    foreach (var pair in DelegateReplacers)
                    {
                        if (mtdref.Equals(other: pair.Key))
                        {
                            ModuleManagers.LogOut($"Adding Delegate Replacer\nfrom {mtdref.FullName}\nto {pair.Value.FullName}");
                            ins.Operand = pair.Value;
                            break;
                        }
                    }
                }
            }
        }
    }
}

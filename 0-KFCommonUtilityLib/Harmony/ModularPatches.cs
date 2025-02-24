using HarmonyLib;
using HarmonyLib.Public.Patching;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ModularPatches
    {
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPrefix]
        private static bool Prefix_StartGame_GameManager()
        {
            ModuleManagers.InitNew();
            return true;
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
        [HarmonyPostfix]
        private static void Postfix_Init_ItemClass(ItemClass __instance)
        {
            ItemClassModuleManager.CheckItem(__instance);
            ItemActionModuleManager.CheckItem(__instance);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.worldInfoCo), MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_worldInfoCo_GameManager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_all = AccessTools.Method(typeof(WorldStaticData), nameof(WorldStaticData.AllConfigsReceivedAndLoaded));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_all))
                {
                    codes.Insert(i + 2, CodeInstruction.Call(typeof(ModuleManagers), nameof(ModuleManagers.FinishAndLoad)).WithLabels(codes[i + 2].ExtractLabels()));
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(ConnectionManager), nameof(ConnectionManager.ServerReady))]
        [HarmonyPrefix]
        private static void Prefix_ServerReady_ConnectionManager()
        {
            ModuleManagers.FinishAndLoad();
        }

        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.ReloadAllXmlsSync))]
        [HarmonyPrefix]
        private static void Prefix_ReloadAllXmlsSync_WorldStaticData()
        {
            ModuleManagers.InitNew();
        }

        [HarmonyPatch(typeof(WorldStaticData), nameof(WorldStaticData.ReloadAllXmlsSync))]
        [HarmonyPostfix]
        private static void Postfix_ReloadAllXmlsSync_WorldStaticData()
        {
            ModuleManagers.FinishAndLoad();
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
}

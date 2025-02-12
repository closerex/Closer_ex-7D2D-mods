using HarmonyLib;
using HarmonyLib.Public.Patching;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class ItemActionModulePatch
    {
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPrefix]
        private static bool Prefix_StartGame_GameManager()
        {
            ItemActionModuleManager.InitNew();
            return true;
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.Init))]
        [HarmonyPostfix]
        private static void Postfix_Init_ItemClass(ItemClass __instance)
        {
            ItemActionModuleManager.CheckItem(__instance);
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInitAll))]
        [HarmonyPrefix]
        private static bool Prefix_LateInitAll_ItemClass()
        {
            ItemActionModuleManager.FinishAndLoad();
            return true;
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
                    CodeInstruction.Call(typeof(ItemActionModulePatch), nameof(ItemActionModulePatch.GetPatched)),
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
    }
}

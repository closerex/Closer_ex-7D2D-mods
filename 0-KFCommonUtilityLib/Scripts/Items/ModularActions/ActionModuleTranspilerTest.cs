using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleTranspilerTest
{
    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_InvalidTest(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Ranged!");
        yield return CodeInstruction.Call(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething));
        foreach (var ins in instructions)
        {
            yield return ins;
        }
    }


    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_RangedTest(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Ranged!");
        yield return CodeInstruction.Call(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething));
        foreach (var ins in instructions)
        {
            yield return ins;
        }
    }

    [HarmonyPatch(typeof(ItemActionCatapult), nameof(ItemAction.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_CatapultTest(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Catapult!");
        yield return CodeInstruction.Call(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething));
        foreach (var ins in instructions)
        {
            yield return ins;
        }
    }

    private static void CallSomething(string str)
    {
        Log.Out($"Call something: {str}\n{StackTraceUtility.ExtractStackTrace()}");
    }
}
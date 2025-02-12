using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleTranspilerTest
{
    [MethodTargetTranspiler(nameof(ItemActionAttack.ExecuteAction), typeof(ItemActionAttack))]
    private static IEnumerable<CodeInstruction> Transpiler_InvalidTest(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Ranged!");
        yield return CodeInstruction.Call(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething));
        foreach (var ins in instructions)
        {
            yield return ins;
        }
    }


    [MethodTargetTranspiler(nameof(ItemAction.ExecuteAction), typeof(ItemActionRanged))]
    private static IEnumerable<CodeInstruction> Transpiler_RangedTest(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Ranged!");
        yield return CodeInstruction.Call(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething));
        foreach (var ins in instructions)
        {
            yield return ins;
        }
    }

    [MethodTargetTranspiler(nameof(ItemAction.ExecuteAction), typeof(ItemActionCatapult))]
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
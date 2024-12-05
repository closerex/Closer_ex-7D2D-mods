using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleTranspilerTest
{
    [MethodTargetTranspiler(nameof(ItemAction.ExecuteAction), typeof(ItemActionRanged))]
    private static void Transpiler_RangedTest(MethodBody body, ModuleDefinition module)
    {
        var worker = body.GetILProcessor();
        var start = body.Instructions[0];
        worker.InsertBefore(start, worker.Create(OpCodes.Ldstr, "Ranged!"));
        worker.InsertBefore(start, worker.Create(OpCodes.Call, module.ImportReference(AccessTools.Method(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething)))));
    }

    [MethodTargetTranspiler(nameof(ItemAction.ExecuteAction), typeof(ItemActionCatapult))]
    private static void Transpiler_CatapultTest(MethodBody body, ModuleDefinition module)
    {
        var worker = body.GetILProcessor();
        var start = body.Instructions[0];
        worker.InsertBefore(start, worker.Create(OpCodes.Ldstr, "Catapult!"));
        worker.InsertBefore(start, worker.Create(OpCodes.Call, module.ImportReference(AccessTools.Method(typeof(ActionModuleTranspilerTest), nameof(ActionModuleTranspilerTest.CallSomething)))));
    }

    private static void CallSomething(string _)
    {

    }
}
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;

[TypeTarget(typeof(ItemActionThrowAway))]
public class ActionModuleDynamicDropLifetime
{
    public float lifetime;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props)
    {
        lifetime = 60f;
        _props.ParseFloat("DropItemLifetime", ref lifetime);
        if (lifetime <= 0)
        {
            lifetime = 419430f;
        }
    }

    [HarmonyPatch(typeof(ItemActionThrowAway), nameof(ItemActionThrowAway.throwAway)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionThrowAway_throwAway(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsConstant(60f))
            {
                codes.RemoveAt(i);
                codes.InsertRange(i, new[]
                {
                    CodeInstruction.LoadArgument(0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<ActionModuleDynamicDropLifetime>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<ActionModuleDynamicDropLifetime>), nameof(IModuleContainerFor<ActionModuleDynamicDropLifetime>.Instance))),
                    CodeInstruction.LoadField(typeof(ActionModuleDynamicDropLifetime), nameof(ActionModuleDynamicDropLifetime.lifetime))
                });
                break;
            }
        }
        return codes;
    }
}

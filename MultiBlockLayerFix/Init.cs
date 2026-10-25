using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UniLinq;

namespace MultiBlockLayerFix
{
    public class Init : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
            {
                return;
            }
            inited = true;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class Patches
    {
        [HarmonyPatch(typeof(Block), nameof(Block.Init))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Block_Init(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_trim = AccessTools.Method(typeof(string), nameof(string.Trim), Array.Empty<Type>());
            var ctor_vec3i = AccessTools.Constructor(typeof(Vector3i), new[] { typeof(int), typeof(int), typeof(int) });

            for (int i = 5; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_trim) && codes[i - 5].opcode == OpCodes.Ldloc_S)
                {
                    var lbd_x = generator.DeclareLocal(typeof(int));
                    var lbd_z = generator.DeclareLocal(typeof(int));

                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].opcode == OpCodes.Newobj && ctor_vec3i.Equals(codes[j].operand))
                        {
                            codes[j - 6].operand = (sbyte)'x';
                            codes.InsertRange(j, new[]
                            {
                                new CodeInstruction(OpCodes.Ldloc_S, lbd_z),
                                new CodeInstruction(OpCodes.Sub)
                            });
                            codes.InsertRange(j - 2, new[]
                            {
                                new CodeInstruction(OpCodes.Ldloc_S, lbd_x),
                                new CodeInstruction(OpCodes.Sub)
                            });
                            break;
                        }
                    }

                    int local_index;
                    local_index = 9;
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, local_index).WithLabels(codes[i + 2].ExtractLabels()),
                        CodeInstruction.LoadField(typeof(Vector3i), nameof(Vector3i.x)),
                        new CodeInstruction(OpCodes.Ldc_I4_2),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_x),
                        new CodeInstruction(OpCodes.Ldloc_S, local_index),
                        CodeInstruction.LoadField(typeof(Vector3i), nameof(Vector3i.z)),
                        new CodeInstruction(OpCodes.Ldc_I4_2),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_z),
                    });
                    break;
                }
            }

            return codes;
        }
    }
}

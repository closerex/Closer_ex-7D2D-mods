using HarmonyLib;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using System.Reflection.Emit;

namespace TrueNoRotation
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
        [HarmonyPatch(typeof(Block), nameof(Block.OnBlockPlaceBefore))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Block_OnBlockPlaceBefore(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 4 && codes[i + 1].Branches(out _))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0).WithLabels(codes[i].ExtractLabels()),
                        new CodeInstruction(OpCodes.Ldloc_S, 4),
                        CodeInstruction.CallClosure<Func<Block, bool, bool>>(static (block, flag) =>
                        {
                            if (flag || block.AllowedRotations == EBlockRotationClasses.None)
                            {
                                //Log.Out($"{block.blockName} allowed rotations {block.AllowedRotations.ToString()} flag {flag}");
                                return true;
                            }
                            return false;
                        }),
                        new CodeInstruction(OpCodes.Stloc_S, 4)
                    });
                    i += 4;
                }
                else if (codes[i].opcode == OpCodes.Stloc_3)
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.CallClosure<Func<int, Block, int>>(static (rnd, block) =>
                        {
                            if (block.AllowedRotations == EBlockRotationClasses.None)
                            {
                                //Log.Out($"{block.blockName} has no allowed rotations, setting random rotation to 0");
                                return 0;
                            }
                            return rnd;
                        })
                    });
                    i += 2;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(Block), nameof(Block.RotateHoldingBlock))]
        [HarmonyPrefix]
        private static bool Prefix_Block_RotateHoldingBlock(Block __instance)
        {
            if (__instance.AllowedRotations == EBlockRotationClasses.None)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(BlockPlacement), nameof(BlockPlacement.OnPlaceBlock))]
        [HarmonyPostfix]
        private static void Postfix_BlockPlacement_OnPlaceBlock(BlockValue _bv, ref BlockPlacement.Result __result)
        {
            var block = _bv.Block;
            if (block.AllowedRotations == EBlockRotationClasses.None)
            {
                __result.blockValue.rotation = 0;
            }
        }

        [HarmonyPatch(typeof(BlockPlacement), nameof(BlockPlacement.LimitRotation))]
        [HarmonyPostfix]
        private static void Postfix_BlockPlacement_LimitRotation(BlockValue _bv, ref byte __result)
        {
            var block = _bv.Block;
            if (block.AllowedRotations == EBlockRotationClasses.None)
            {
                __result = 0;
            }
        }
    }
}

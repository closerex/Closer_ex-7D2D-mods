using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace NoHentai
{
    public class NoHentaiInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class Patches
    {
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.dropBackpack))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_EntityPlayerLocal_dropBackpack(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_setslot = AccessTools.Method(typeof(Equipment), nameof(Equipment.SetSlotItem));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_setslot))
                {
                    codes.InsertRange(i - 5, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Brtrue_S, codes[i + 1].labels[0])
                    });
                    i += 2;
                }
            }

            return codes;
        }
    }
}


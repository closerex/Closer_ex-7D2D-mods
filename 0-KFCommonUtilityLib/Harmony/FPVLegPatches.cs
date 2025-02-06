using HarmonyLib;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;

namespace KFCommonUtilityLib.Harmony
{
    //[HarmonyPatch]
    public static class FPVLegPatches
    {
        [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.CreateVizTP))]
        [HarmonyPrefix]
        private static void Prefix_SDCSUtils_CreateTP(EntityAlive entity, ref bool isFPV, out bool __state)
        {
            __state = isFPV;
            if (entity is EntityPlayerLocal)
            {
                entity.emodel.IsFPV = false;
                isFPV = false;
            }
        }

        [HarmonyPatch(typeof(SDCSUtils), nameof(SDCSUtils.CreateVizTP))]
        [HarmonyPostfix]
        private static void Postfix_SDCSUtils_CreateTP(EntityAlive entity, ref bool isFPV, bool __state)
        {
            if (entity is EntityPlayerLocal)
            {
                entity.emodel.IsFPV = __state;
                isFPV = __state;
            }
        }
    }
}

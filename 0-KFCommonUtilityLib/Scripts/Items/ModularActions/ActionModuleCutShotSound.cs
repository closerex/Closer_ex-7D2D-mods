using Audio;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;


[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(CutShotSoundData))]
public class ActionModuleCutShotSound
{
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ItemActionEffects(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var mtd_play = AccessTools.Method(typeof(Manager), nameof(Manager.Play), new[] { typeof(Entity), typeof(string), typeof(float), typeof(bool) });
        var mtd_soundstart = AccessTools.Field(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.SoundStart));

        var lbd_data = generator.DeclareLocal(typeof(CutShotSoundData));

        int soundLocalIndex = GameManager.IsDedicatedServer && Application.platform == RuntimePlatform.LinuxServer ? 4 : 6;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_play) && (codes[i - 3].LoadsField(mtd_soundstart) || (codes[i - 3].IsLdloc() && ((LocalBuilder)codes[i - 3].operand).LocalIndex == soundLocalIndex)))
            {
                codes[i - 1].opcode = OpCodes.Ldc_I4_1;
                codes.RemoveAt(i + 1);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data),
                    CodeInstruction.Call(typeof(CutShotSoundData), nameof(CutShotSoundData.Record))
                });
                codes.InsertRange(i, new[]
                {
                    CodeInstruction.LoadLocal(1),
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data),
                    CodeInstruction.Call(typeof(CutShotSoundData), nameof(CutShotSoundData.StopPrevious))
                });
                i += 5;
            }
            else if (codes[i].opcode == OpCodes.Stloc_0)
            {
                codes.InsertRange(i + 1, new[]
                {
                    CodeInstruction.LoadLocal(0),
                    new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<CutShotSoundData>)),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<CutShotSoundData>), nameof(IModuleContainerFor<CutShotSoundData>.Instance))),
                    new CodeInstruction(OpCodes.Stloc_S, lbd_data)
                });
                i += 5;
            }
        }

        return codes;
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(CutShotSoundData __customData)
    {
        __customData.curHandle = null;
    }

    public class CutShotSoundData
    {
        public Handle curHandle;

        public static void StopPrevious(EntityAlive entity, CutShotSoundData data)
        {
            if (data.curHandle != null)
            {
                data.curHandle.Stop(entity.entityId);
                data.curHandle = null;
            }
        }

        public static void Record(Handle handle, CutShotSoundData data)
        {
            data.curHandle = handle;
        }
    }
}
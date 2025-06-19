using HarmonyLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[HarmonyPatch]
public static class MultiActionReversePatches
{
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.GetValue))]
    public static float ProjectileGetValue(PassiveEffects _passiveEffect, ItemValue _originalItemValue = null, float _originalValue = 0f, EntityAlive _entity = null, Recipe _recipe = null, FastTags<TagGroup.Global> tags = default(FastTags<TagGroup.Global>), bool calcEquipment = true, bool calcHoldingItem = true, bool calcProgression = true, bool calcBuffs = true, bool calcChallenges = true, int craftingTier = 1, bool useMods = true, bool _useDurability = false)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
                return null;
            var codes = instructions.ToList();

            var mtd_modify = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.ModifyValue));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_modify) && codes[i - 9].opcode == OpCodes.Ldarg_1)
                {
                    code.operand = AccessTools.Method(typeof(MultiActionProjectileRewrites), nameof(MultiActionProjectileRewrites.ProjectileValueModifyValue));
                }
            }
            return codes;
        }

        _ = Transpiler(null);
        return _originalValue;
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.Fire))]
    public static void ProjectileFire(this ProjectileMoveScript __instance, Vector3 _idealStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride = 0, float _radius = 0f, bool _isBallistic = false)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions == null)
                return null;

            var codes = instructions.ToList();

            FieldInfo fld_launcher = AccessTools.Field(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.itemValueLauncher));
            FieldInfo fld_projectile = AccessTools.Field(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.itemValueProjectile));
            MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
            MethodInfo mtd_getvaluenew = AccessTools.Method(typeof(MultiActionReversePatches), nameof(ProjectileGetValue));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_launcher))
                {
                    codes[i].operand = fld_projectile;
                }
                else if (codes[i].Calls(mtd_getvalue))
                {
                    codes[i].operand = mtd_getvaluenew;
                }
            }
            return codes;
        }
        _ = Transpiler(null);
    }
}
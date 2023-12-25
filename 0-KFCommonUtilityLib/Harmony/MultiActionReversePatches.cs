using HarmonyLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

[HarmonyPatch]
public class MultiActionReversePatches
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.GetValue))]
    public static float ProjectileGetValue(PassiveEffects _passiveEffect, ItemValue _originalItemValue = null, float _originalValue = 0f, EntityAlive _entity = null, Recipe _recipe = null, FastTags tags = default(FastTags), bool calcEquipment = true, bool calcHoldingItem = true, bool calcProgression = true, bool calcBuffs = true, int craftingTier = 1, bool useMods = true, bool _useDurability = false)
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
                    break;
                }
            }
            return codes;
        }

        _ = Transpiler(null);
        return _originalValue;
    }
}
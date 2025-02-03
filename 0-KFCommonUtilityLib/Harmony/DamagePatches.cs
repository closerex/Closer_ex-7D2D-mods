using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[HarmonyPatch]
public static class DamagePatches
{
    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.damageEntityLocal))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_damageEntityLocal_EntityAlive(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_calc = AccessTools.Method(typeof(Equipment), nameof(Equipment.CalcDamage));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_calc))
            {
                codes[i] = CodeInstruction.Call(typeof(DamagePatches), nameof(DamagePatches.CalcEquipmentDamage));
                codes.RemoveRange(i - 12, 12);
                codes.InsertRange(i - 12, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                    CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.Other))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_Hit_ItemActionAttack(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_getdamagegroup = AccessTools.Method(typeof(DamageSource), nameof(DamageSource.GetEntityDamageEquipmentSlotGroup));
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_getdamagegroup))
            {
                codes.InsertRange(i + 3, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i - 2].operand),
                    new CodeInstruction(OpCodes.Ldloc_S, codes[i - 1].operand),
                    CodeInstruction.Call(typeof(DamageSource), nameof(DamageSource.GetEntityDamageBodyPart)),
                    CodeInstruction.Call(typeof(DamagePatches), nameof(DamagePatches.GetBodyPartTags)),
                    CodeInstruction.Call(typeof(FastTags<TagGroup.Global>), "op_BitwiseOr")
                });
                break;
            }
        }
        return codes;
    }

    private static void CalcEquipmentDamage(Equipment equipment, ref DamageResponse damageResponse, EntityAlive attacker)
    {
        damageResponse.ArmorDamage = damageResponse.Strength;
        if (damageResponse.Source.DamageTypeTag.Test_AnySet(Equipment.physicalDamageTypes))
        {
            if (damageResponse.Strength > 0)
            {
                float totalPhysicalArmorResistPercent = GetTotalPhysicalArmorResistPercent(equipment, in damageResponse, attacker) * .01f;
                damageResponse.ArmorDamage = Utils.FastMax((totalPhysicalArmorResistPercent > 0f) ? 1 : 0, Mathf.RoundToInt((float)damageResponse.Strength * totalPhysicalArmorResistPercent));
                damageResponse.Strength -= damageResponse.ArmorDamage;
                return;
            }
        }
        else
        {
            damageResponse.Strength = Mathf.RoundToInt(Utils.FastMax(0f, (float)damageResponse.Strength * (1f - EffectManager.GetValue(PassiveEffects.ElementalDamageResist, null, 0f, equipment.m_entity, null, damageResponse.Source.DamageTypeTag) * .01f)));
            damageResponse.ArmorDamage = Mathf.RoundToInt((float)Utils.FastMax(0, damageResponse.ArmorDamage - damageResponse.Strength));
        }
    }

    private static float GetTotalPhysicalArmorResistPercent(Equipment equipment, in DamageResponse damageResponse, EntityAlive attacker)
    {
        if (!equipment?.m_entity)
            return 0f;
        FastTags<TagGroup.Global> bodyPartTags = GetBodyPartTags(damageResponse.HitBodyPart);
        float resist = EffectManager.GetValue(PassiveEffects.PhysicalDamageResist, null, 0f, equipment.m_entity, null, Equipment.coreDamageResist | bodyPartTags);
        if (attacker)
        {
            attacker.MinEventContext.Other = equipment.m_entity;
            attacker.MinEventContext.ItemValue = damageResponse.Source.AttackingItem ?? ItemValue.None;
            if (damageResponse.Source.AttackingItem != null)
            {
                if (damageResponse.Source.AttackingItem.ItemClass.Actions[1] is ItemActionProjectile)
                { 
                    return MultiActionReversePatches.ProjectileGetValue(PassiveEffects.TargetArmor, damageResponse.Source.AttackingItem, resist, attacker, null, damageResponse.Source.AttackingItem.ItemClass.ItemTags | bodyPartTags);
                }
                return EffectManager.GetValue(PassiveEffects.TargetArmor, damageResponse.Source.AttackingItem, resist, attacker, null, damageResponse.Source.AttackingItem.ItemClass.ItemTags | bodyPartTags);
            }
            return EffectManager.GetValue(PassiveEffects.TargetArmor, null, resist, attacker, null, bodyPartTags);
        }
        return resist;
    }

    public static FastTags<TagGroup.Global> GetBodyPartTags(EnumBodyPartHit bodyparts)
    {
        if ((bodyparts & EnumBodyPartHit.LeftUpperArm) > EnumBodyPartHit.None)
        {
            return LeftUpperArmTags;
        }
        if ((bodyparts & EnumBodyPartHit.LeftLowerArm) > EnumBodyPartHit.None)
        {
            return LeftLowerArmTags;
        }
        if ((bodyparts & EnumBodyPartHit.RightUpperArm) > EnumBodyPartHit.None)
        {
            return RightUpperArmTags;
        }
        if ((bodyparts & EnumBodyPartHit.RightLowerArm) > EnumBodyPartHit.None)
        {
            return RightLowerArmTags;
        }
        if ((bodyparts & EnumBodyPartHit.LeftUpperLeg) > EnumBodyPartHit.None)
        {
            return LeftUpperLegTags;
        }
        if ((bodyparts & EnumBodyPartHit.LeftLowerLeg) > EnumBodyPartHit.None)
        {
            return LeftLowerLegTags;
        }
        if ((bodyparts & EnumBodyPartHit.RightUpperLeg) > EnumBodyPartHit.None)
        {
            return RightUpperLegTags;
        }
        if ((bodyparts & EnumBodyPartHit.RightLowerLeg) > EnumBodyPartHit.None)
        {
            return RightLowerLegTags;
        }
        if ((bodyparts & EnumBodyPartHit.Head) > EnumBodyPartHit.None)
        {
            return HeadTags;
        }
        if ((bodyparts & EnumBodyPartHit.Torso) > EnumBodyPartHit.None)
        {
            return TorsoTags;
        }
        if ((bodyparts & EnumBodyPartHit.Special) > EnumBodyPartHit.None)
        {
            return SpecialTags;
        }
        return FastTags<TagGroup.Global>.none;
    }

    private static FastTags<TagGroup.Global> TorsoTags = FastTags<TagGroup.Global>.Parse("torso");
    private static FastTags<TagGroup.Global> HeadTags = FastTags<TagGroup.Global>.Parse("head");
    private static FastTags<TagGroup.Global> SpecialTags = FastTags<TagGroup.Global>.Parse("special");
    private static FastTags<TagGroup.Global> ArmsTags = FastTags<TagGroup.Global>.Parse("arms");
    private static FastTags<TagGroup.Global> UpperArmsTags = FastTags<TagGroup.Global>.Parse("upperArms");
    private static FastTags<TagGroup.Global> LowerArmsTags = FastTags<TagGroup.Global>.Parse("lowerArms");
    private static FastTags<TagGroup.Global> LeftArmTags = FastTags<TagGroup.Global>.Parse("leftArms");
    private static FastTags<TagGroup.Global> RightArmTags = FastTags<TagGroup.Global>.Parse("rightArms");
    private static FastTags<TagGroup.Global> LeftUpperArmTags = FastTags<TagGroup.Global>.Parse("leftUpperArms") | LeftArmTags | UpperArmsTags | ArmsTags;
    private static FastTags<TagGroup.Global> LeftLowerArmTags = FastTags<TagGroup.Global>.Parse("leftLowerArms") | LeftArmTags | LowerArmsTags | ArmsTags;
    private static FastTags<TagGroup.Global> RightUpperArmTags = FastTags<TagGroup.Global>.Parse("rightUpperArms") | RightArmTags | UpperArmsTags | ArmsTags;
    private static FastTags<TagGroup.Global> RightLowerArmTags = FastTags<TagGroup.Global>.Parse("rightLowerArms") | RightArmTags | LowerArmsTags | ArmsTags;
    private static FastTags<TagGroup.Global> LegsTags = FastTags<TagGroup.Global>.Parse("legs");
    private static FastTags<TagGroup.Global> UpperLegsTags = FastTags<TagGroup.Global>.Parse("upperLegs");
    private static FastTags<TagGroup.Global> LowerLegsTags = FastTags<TagGroup.Global>.Parse("lowerLegs");
    private static FastTags<TagGroup.Global> LeftLegTags = FastTags<TagGroup.Global>.Parse("leftLegs");
    private static FastTags<TagGroup.Global> RightLegTags = FastTags<TagGroup.Global>.Parse("rightLegs");
    private static FastTags<TagGroup.Global> LeftUpperLegTags = FastTags<TagGroup.Global>.Parse("leftUpperLegs") | LeftLegTags | UpperLegsTags | LegsTags;
    private static FastTags<TagGroup.Global> LeftLowerLegTags = FastTags<TagGroup.Global>.Parse("leftLowerLegs") | LeftLegTags | LowerLegsTags | LegsTags;
    private static FastTags<TagGroup.Global> RightUpperLegTags = FastTags<TagGroup.Global>.Parse("rightUpperLegs") | RightLegTags | UpperLegsTags | LegsTags;
    private static FastTags<TagGroup.Global> RightLowerLegTags = FastTags<TagGroup.Global>.Parse("rightLowerLegs") | RightLegTags | LowerLegsTags | LegsTags;
}
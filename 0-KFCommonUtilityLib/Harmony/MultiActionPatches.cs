using HarmonyLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;

namespace KFCommonUtilityLib.Harmony
{
    //todo: patch all accesses to ItemClass.Actions so that they process all actions
    [HarmonyPatch]
    public static class MultiActionPatches
    {
        #region Passive tags
        //maybe use TriggerHasTags instead?
        public struct TagsForAll
        {
            public FastTags tags;
            public bool matchAllTags;
            public bool invertTagCheck;

            public bool IsValid()
            {
                return !tags.IsEmpty || matchAllTags || invertTagCheck;
            }
        }

        [HarmonyPatch(typeof(MinEffectGroup), nameof(MinEffectGroup.ParseXml))]
        [HarmonyPrefix]
        private static bool Prefix_ParseXml_MinEffectGroup(XElement _element, out TagsForAll __state)
        {
            __state = new TagsForAll()
            {
                tags = FastTags.none,
                matchAllTags = false,
                invertTagCheck = false
            };
            string tags = _element.GetAttribute("tags");
            __state.tags = tags != null ? FastTags.Parse(tags) : FastTags.none;
            if (_element.HasAttribute("match_all_tags"))
            {
                __state.matchAllTags = true;
            }
            if (_element.HasAttribute("invert_tag_check"))
            {
                __state.invertTagCheck = true;
            }

            return true;
        }


        [HarmonyPatch(typeof(MinEffectGroup), nameof(MinEffectGroup.ParseXml))]
        [HarmonyPostfix]
        private static void Postfix_ParseXml_MinEffectGroup(MinEffectGroup __instance, TagsForAll __state)
        {
            if (!__state.IsValid())
                return;

            foreach (var passive in __instance.PassiveEffects)
            {
                if (!__state.tags.IsEmpty)
                {
                    passive.Tags |= __state.tags;
                }

                if (__state.matchAllTags)
                {
                    passive.MatchAnyTags = false;
                }

                if (__state.invertTagCheck)
                {
                    passive.InvertTagCheck = true;
                }
            }
        }
        #endregion

        #region Ranged Reload
        //Replace reload action index with animator item action index parameter
        [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnStateEnter_AnimatorRangedReloadState(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_actionData = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.actionData));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if ((code.LoadsField(fld_action) || code.LoadsField(fld_actionData)) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
            }

            codes.InsertRange(0, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(AvatarController), nameof(AvatarController.itemActionIndexHash))),
                CodeInstruction.Call(typeof(Animator), nameof(Animator.GetInteger), new [] { typeof(int) }),
                new CodeInstruction(OpCodes.Stloc_S, lbd_index)
            });

            return codes;
        }
        #endregion

    }

    #region Ranged Reload
    [HarmonyPatch]
    public static class RangedReloadPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.ReloadGun)),
                AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ReloadGun)),
                AccessTools.Method(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ReloadGun))
            };
        }

        //Why? Ask TFP why they don't just call base.ReloadGun()
        [HarmonyPrefix]
        private static bool Prefix_ReloadGun_ItemActionLauncher(ItemActionData _actionData)
        {
            _actionData.invData.holdingEntity.emodel?.avatarController?.UpdateInt(AvatarController.itemActionIndexHash, _actionData.indexInEntityOfAction, false);
            return true;
        }
    }
    #endregion

    #region Action tags
    [HarmonyPatch]
    public static class ActionTagPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(AnimatorMeleeAttackState), nameof(AnimatorMeleeAttackState.OnStateEnter)),
                AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageEntity)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageEntity)),
                AccessTools.Method(typeof(ItemActionThrownWeapon), nameof(ItemActionThrownWeapon.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionThrownWeapon), nameof(ItemActionThrownWeapon.GetDamageEntity))
            };
        }

        //set correct tag for action index above 2
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnStateEnter_AnimatorMeleeAttackState(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            FieldInfo fld_tag = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.PrimaryTag));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_tag))
                {
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.ActionIndexToTag)));
                    codes.RemoveRange(i - 3, 4);
                    break;
                }
            }

            return codes;
        }
    }
    #endregion
}

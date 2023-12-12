using HarmonyLib;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UniLinq;
using System.Xml.Linq;
using System;

namespace KFCommonUtilityLib.Harmony
{
    //done?: patch all accesses to ItemClass.Actions so that they process all actions
    //todo: patch ItemClass.ExecuteAction, MinEvent triggers
    //todo: replace GameManager.ItemReload*
    //done: patch ItemActionRanged.ConsumeAmmo
    //todo: patch passive effect handling and trigger effect firing, in ItemValue.ModifyValue set action index from tags
    //todo: patch trigger action index enum/ or just let it use secondary and tag check?
    //todo: handle ItemActionAttack.GetDamageEntity/GetDamageBlock and call sites actionIndex
    //todo: sell, assemble, scrap remove ammo

    //todo: figure out when is meta and ammo index used, how to set their value in minimum patches
    //ExecuteAction, Reload, what's more?
    //safe to work within ItemAction scope
    //even if meta and ammo index is set accordingly, better keep checking them in reload script
    [HarmonyPatch]
    public static class MultiActionPatches
    {
        #region Run Correct ItemAction

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
            FieldInfo fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if ((code.LoadsField(fld_action) || code.LoadsField(fld_actionData)) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    //get correct ItemAction and data
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
                else if (code.opcode == OpCodes.Initobj)
                {
                    //set action index tag
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetItemTagsWithActionIndex))
                    });
                    codes.RemoveRange(i - 1, 3);
                }
                else if (code.LoadsField(fld_meta))
                {
                    //load correct meta
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionGetMeta)));
                    codes.RemoveRange(i - 2, 3);
                }
                else if (code.LoadsField(fld_ammoindex))
                {
                    //load correct selected ammo index
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionGetSelectedAmmoTypeIndex))
                    });
                    codes.RemoveRange(i - 1, 2);
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

        [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateExit))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnStateExit_AnimatorRangedReloadState(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_fastmin = AccessTools.Method(typeof(Utils), nameof(Utils.FastMin), new[] { typeof(int), typeof(int) });
            MethodInfo prop_itemvalue = AccessTools.PropertyGetter(typeof(ItemInventoryData), nameof(ItemInventoryData.itemValue));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Initobj)
                {
                    //set action index tag
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetItemTagsWithActionIndex))
                    });
                    codes.RemoveRange(i - 1, 3);
                }
                else if (code.Calls(mtd_fastmin))
                {
                    //reload to correct metadata
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData)),
                        new CodeInstruction(OpCodes.Castclass, typeof(ItemActionRanged.ItemActionDataRanged)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionReload))
                    });
                    codes.RemoveRange(i - 14, 16);
                }
                else if (code.LoadsField(fld_ammoindex))
                {
                    //load correct ammo index
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionGetSelectedAmmoTypeIndex)));
                    codes.RemoveRange(i - 2, 3);
                }
            }

            return codes;
        }


        [HarmonyPatch(typeof(AnimatorRangedReloadState), "GetAmmoCountToReload")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetAmmoCountToReload_AnimatorRangedReloadState(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            FieldInfo fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_meta))
                {
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionGetMeta)));
                    codes.RemoveRange(i - 2, 3);
                }
            }

            return codes;
        }
        #endregion

        #region Aiming
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.IsAimingGunPossible))]
        [HarmonyPrefix]
        private static bool Prefix_IsAimingGunPossible_EntityPlayerLocal(ref bool __result, EntityPlayerLocal __instance)
        {
            __result = true;
            for (int i = 0; i < __instance.inventory.holdingItem.Actions.Length; i++)
            {
                ItemAction action = __instance.inventory.holdingItem.Actions[i];
                ItemActionData actionData = __instance.inventory.holdingItemData.actionData[i];
                __result &= (action == null || action.IsAimingGunPossible(actionData));
            }
            return false;
        }
        #endregion

        #region Cancel bow draw
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.TryCancelBowDraw))]
        [HarmonyPrefix]
        private static bool Prefix_TryCancelBowDraw(EntityPlayerLocal __instance)
        {
            for (int i = 0; i < __instance.inventory.holdingItem.Actions.Length; i++)
            {
                ItemAction action = __instance.inventory.holdingItem.Actions[i];
                ItemActionData actionData = __instance.inventory.holdingItemData.actionData[i];
                if (action is ItemActionCatapult catapult)
                {
                    action.CancelAction(actionData);
                    actionData.HasExecuted = false;
                }
            }
            return false;
        }
        #endregion

        #region Consume wheel scroll
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ConsumeScrollWheel))]
        [HarmonyPrefix]
        private static bool Prefix_ConsumeScrollWheel_ItemClass(ItemClass __instance, ItemInventoryData _data, float _scrollWheelInput, PlayerActionsLocal _playerInput, ref bool __result)
        {
            __result = false;
            for (int i = 0; i < __instance.Actions.Length; i++)
            {
                ItemAction action = __instance.Actions[i];
                if (action != null && action.ConsumeScrollWheel(_data.actionData[i], _scrollWheelInput, _playerInput))
                {
                    __result = true;
                    break;
                }

            }
            return false;
        }
        #endregion

        #region Create modifier data for more actions
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.CreateInventoryData))]
        [HarmonyPostfix]
        private static void Postfix_CreateInventoryData_ItemClass(ItemInventoryData __result, ItemClass __instance)
        {
            int prevCount = __result.actionData.Count;
            while (__result.actionData.Count < __instance.Actions.Length)
            {
                __result.actionData.Add(null);
            }
            for (; prevCount < __instance.Actions.Length; prevCount++)
            {
                if (__instance.Actions[prevCount] != null)
                    __result.actionData[prevCount] = __instance.Actions[prevCount].CreateModifierData(__result, prevCount);
            }
        }
        #endregion

        //todo: should I patch ItemClass.ExecuteAction for melee actions?

        #region IsFocusBlockInside?
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.IsFocusBlockInside))]
        [HarmonyPrefix]
        private static bool Prefix_IsFocusBlockInside(ItemClass __instance, ref bool __result)
        {
            __result = __instance.Actions.All(action => action != null && action.IsFocusBlockInside());
            return false;
        }
        #endregion

        #region IsHUDDisabled
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.IsHUDDisabled))]
        [HarmonyPrefix]
        private static bool Prefix_IsHUDDisabled_ItemClass(ItemClass __instance, ref bool __result, ItemInventoryData _data)
        {
            __result = false;
            for (int i = 0; i < __instance.Actions.Length; i++)
            {
                __result |= __instance.Actions[i] != null && __instance.Actions[i].IsHUDDisabled(_data.actionData[i]);
            }
            return false;
        }
        #endregion

        #region OnHUD
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.OnHUD))]
        [HarmonyPrefix]
        private static bool Prefix_OnHUD_ItemClass(ItemInventoryData _data, int _x, int _y, ItemClass __instance)
        {
            for (int i = 0; i < __instance.Actions.Length; i++)
            {
                __instance.Actions[i]?.OnHUD(_data.actionData[i], _x, _y);
            }
            return false;
        }
        #endregion

        #region OnScreenOverlay
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.OnScreenOverlay))]
        [HarmonyPrefix]
        private static bool Prefix_OnScreenOverlay_ItemClass(ItemInventoryData _data, ItemClass __instance)
        {
            for (int i = 0; i < __instance.Actions.Length; i++)
            {
                __instance.Actions[i]?.OnScreenOverlay(_data.actionData[i]);
            }
            return false;
        }
        #endregion

        #region Initial meta, Max ammo count
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.GetInitialMetadata))]
        [HarmonyPrefix]
        private static bool Prefix_GetInitialMetadata_ItemClass(ItemClass __instance, ItemValue _itemValue, ref int __result)
        {
            if (__instance.Actions[0] == null)
            {
                __result = 0;
            }
            else
            {
                foreach (var action in __instance.Actions)
                {
                    int? meta = action?.GetInitialMeta(_itemValue);
                    if (action.ActionIndex == 0)
                        __result = meta.Value;
                }
            }

            return false;
        }
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.GetInitialMeta))]
        [HarmonyPrefix]
        private static bool Prefix_GetInitialMeta_ItemActionRanged(ItemActionRanged __instance, ItemValue _itemValue, ref int __result)
        {
            __result = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, _itemValue, __instance.BulletsPerMagazine, null, null, _itemValue.ItemClass.ItemTags | MultiActionUtils.ActionIndexToTag(__instance.ActionIndex), true, true, true, true, 1, true, false);
            return false;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.GetInitialMeta))]
        [HarmonyPostfix]
        private static void Postfix_GetInitialMeta_ItemActionRanged(ItemActionRanged __instance, ItemValue _itemValue, int __result)
        {
            _itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[__instance.ActionIndex], __result, TypedMetadataValue.TypeTag.Integer);
        }

        [HarmonyPatch(typeof(ItemActionRanged.ItemActionDataRanged), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void Postfix_ctor_ItemActionDataRanged(ItemActionRanged.ItemActionDataRanged __instance)
        {
            ItemValue itemValue = __instance.invData.itemValue;
            int actionIndex = __instance.indexInEntityOfAction;
            //if metadata of index 0 does not exist then the item is not initialized
            if (!itemValue.HasMetadata(MultiActionUtils.ActionMetaNames[actionIndex], TypedMetadataValue.TypeTag.Integer))
            {
                itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[actionIndex], actionIndex == 0 ? itemValue.Meta : 0, TypedMetadataValue.TypeTag.Integer);
            }

            if (!itemValue.HasMetadata(MultiActionUtils.ActionSelectedAmmoNames[actionIndex], TypedMetadataValue.TypeTag.Integer))
            {
                itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[actionIndex], actionIndex == 0 ? itemValue.SelectedAmmoTypeIndex : 0, TypedMetadataValue.TypeTag.Integer);
            }
        }
        #endregion

        #region Ranged ExecuteAction FireEvent params
        //why? ask TFP the fuck they are doing
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            FieldInfo fld_itemactiondata = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.ItemActionData));
            MethodInfo mtd_reloadserver = AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemReloadServer));
            FieldInfo fld_gamemanager = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.gameManager));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.StoresField(fld_itemactiondata))
                {
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                    codes.RemoveRange(i - 5, 5);
                    break;
                }
                else if (code.Calls(mtd_reloadserver))
                {
                    int j = i;
                    while (!codes[j].LoadsField(fld_gamemanager))
                    {
                        j--;
                    }
                    codes.RemoveAt(i);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.FixedItemReloadServer))
                    });
                    codes.RemoveRange(j - 2, 3);
                }
            }

            return codes;
        }
        #endregion

        #region ItemActionEffect meta
        //load correct meta
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> codeInstructions)
        {
            var codes = codeInstructions.ToList();
            FieldInfo fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_meta))
                {
                    codes.Insert(i + 1, CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionGetMeta)));
                    codes.RemoveRange(i - 2, 3);
                }
            }
            return codes;
        }

        #endregion

        #region GameManager.ItemReload*
        //SwapSelectedAmmo and SwapAmmoType?
        #endregion

        #endregion

        #region Use Correct Meta

        #region ItemActionLauncher initiate projectile, swap ammo index
        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
        [HarmonyPrefix]
        private static bool Prefix_instantiateProjectile_ItemActionLauncher(ItemActionData _actionData)
        {
            MultiActionUtils.SetCurrentMetaAndAmmoIndex(_actionData);
            return true;
        }

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
        [HarmonyPostfix]
        private static void Postfix_instantiateProjectile_ItemActionLauncher(ItemActionData _actionData)
        {
            MultiActionUtils.ResetCurrentMetaAndAmmoIndex(_actionData);
        }
        #endregion

        #endregion

        #region Correct tags for PassiveEffects

        #region ActionData tags
        [HarmonyPatch(typeof(ItemActionData), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void Postfix_ctor_ItemActionData(ItemActionData __instance)
        {
            __instance.ActionTags = MultiActionUtils.ActionIndexToTag(__instance.indexInEntityOfAction);
        }
        #endregion

        #region Spread multiplier
        [HarmonyPatch(typeof(ItemActionRanged), "onHoldingEntityFired")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_onHoldingEntityFired_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Initobj)
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetItemTagsWithActionIndex))
                    });
                    codes.RemoveRange(i - 1, 3);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionRanged), "updateAccuracy")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_updateAccuracy_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Initobj)
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetItemTagsWithActionIndex))
                    });
                    codes.RemoveRange(i - 1, 3);
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region Simple patches
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.HasInfiniteAmmo))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_HasInfiniteAmmo_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Initobj)
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetItemTagsWithActionIndex))
                    });
                    codes.RemoveRange(i - 1, 3);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.GetMaxAmmoCount))]
        [HarmonyPrefix]
        private static bool Prefix_GetMaxAmmoCount_ItemActionRanged(ItemActionRanged __instance, ItemActionData _actionData, ref int __result)
        {
            __result = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, _actionData.invData.itemValue, __instance.BulletsPerMagazine, _actionData.invData.holdingEntity, null, MultiActionUtils.GetItemTagsWithActionIndex(_actionData), true, true, true, true, 1, true, false);
            return false;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.GetRange))]
        [HarmonyPrefix]
        private static bool Prefix_GetRange_ItemActionRanged(ItemActionRanged __instance, ItemActionData _actionData, ref int __result)
        {
            __result = (int)EffectManager.GetValue(PassiveEffects.MaxRange, _actionData.invData.itemValue, __instance.Range, _actionData.invData.holdingEntity, null, MultiActionUtils.GetItemTagsWithActionIndex(_actionData), true, true, true, true, 1, true, false);
            return false;
        }
        #endregion

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

    #region Melee action tags
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

    #region 3
    [HarmonyPatch]
    public static class ThreePatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.OnHoldingUpdate)),
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.CleanupHoldingActions)),
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.StartHolding)),
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.StopHolding)),
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.IsActionRunning))
            };
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Three(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_3)
                    instruction.opcode = OpCodes.Ldc_I4_5;
                yield return instruction;
            }
        }
    }
    #endregion
    //todo: handle meta switch
}

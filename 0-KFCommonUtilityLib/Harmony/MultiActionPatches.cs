﻿using GameEvent.SequenceActions;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.NetPackages;
using KFCommonUtilityLib.Scripts.StaticManagers;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib.Harmony
{
    //done?: patch all accesses to ItemClass.Actions so that they process all actions
    //done?: patch ItemClass.ExecuteAction, MinEvent triggers
    //done: replace GameManager.ItemReload*
    //done: patch ItemActionRanged.ConsumeAmmo
    //todo: patch passive effect handling and trigger effect firing, in ItemValue.ModifyValue set action index from tags
    //todo: patch trigger action index enum/ or just let it use secondary and tag check?
    //todo: handle ItemActionAttack.GetDamageEntity/GetDamageBlock and call sites actionIndex
    //todo: sell, assemble, scrap remove ammo

    //nope, not gonna work
    //try old style action toggle,
    //replace meta and ammo index when toggling, => solves ammo issue
    //provide requirement for checking current mode, => solves effect group condition issue
    //replace hardcoded action index with current mode => bulk patch is enough?
    //ItemClass subclass? maybe not
    //is inventory update on remote client really needed? put off

    //todo: figure out when is meta and ammo index used, how to set their value in minimum patches
    //ExecuteAction, Reload, what's more?
    //safe to work within ItemAction scope
    //even if meta and ammo index is set accordingly, better keep checking them in reload script
    [HarmonyPatch]
    public static class MultiActionPatches
    {
        #region Run Correct ItemAction

        #region Ranged Reload
        //Replace reload action index with animator item action index parameter
        //set MinEventParams.ItemActionData before getting passive value
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
            MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
            bool firstRet = true;

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if ((code.LoadsField(fld_action) || code.LoadsField(fld_actionData)) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    //get correct ItemAction and data
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
                else if (code.Calls(mtd_getvalue))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                        CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), "actionData"),
                        CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData))
                    });
                    break;
                }
                else if (code.opcode == OpCodes.Ret && firstRet)
                {
                    firstRet = false;
                    var insert = new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    };
                    insert[0].MoveLabelsFrom(codes[i + 1]);
                    codes.InsertRange(i + 1, insert);
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateExit))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnStateExit_AnimatorRangedReloadState(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_getvalue))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.actionData)),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                        CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.actionData)),
                        CodeInstruction.StoreField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData))
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        //KEEP
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

        //KEEP
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

        //KEEP
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

        //KEEP
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

        //KEEP
        #region IsFocusBlockInside?
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.IsFocusBlockInside))]
        [HarmonyPrefix]
        private static bool Prefix_IsFocusBlockInside_ItemClass(ItemClass __instance, ref bool __result)
        {
            __result = __instance.Actions.All(action => action != null && action.IsFocusBlockInside());
            return false;
        }
        #endregion

        //KEEP
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

        //KEEP
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

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.GetCrosshairType))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetCrosshairType_ItemClass(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_action) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
            }

            codes.InsertRange(0, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity)),
                new CodeInstruction(OpCodes.Stloc_S, lbd_index)
            });

            return codes;
        }
        #endregion

        //KEEP
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

        #region inventory related
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetHoldingGun))]
        [HarmonyPrefix]
        private static bool Prefix_GetHoldingGun_Inventory(Inventory __instance, ref ItemActionAttack __result)
        {
            __result = __instance.holdingItem.Actions[MultiActionManager.GetActionIndexForEntity(__instance.entity)] as ItemActionAttack ?? __instance.holdingItem.Actions[0] as ItemActionAttack;
            return false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetHoldingDynamicMelee))]
        [HarmonyPrefix]
        private static bool Prefix_GetHoldingDynamicMelee_Inventory(Inventory __instance, ref ItemActionDynamic __result)
        {
            __result = __instance.holdingItem.Actions[MultiActionManager.GetActionIndexForEntity(__instance.entity)] as ItemActionDynamic ?? __instance.holdingItem.Actions[0] as ItemActionDynamic;
            return false;
        }

        //[HarmonyPatch(typeof(Inventory), "clearSlotByIndex")]
        //[HarmonyPrefix]
        //private static bool Prefix_clearSlotByIndex_Inventory(int _idx, Inventory __instance, EntityAlive ___entity)
        //{
        //    if (__instance.holdingItemIdx == _idx && ___entity != null && !___entity.isEntityRemote)
        //    {
        //        var mapping = MultiActionManager.GetMappingForEntity(___entity.entityId);
        //        mapping?.SaveMeta();
        //    }
        //    return true;
        //}
        #endregion

        #region GameManager.ItemReload*
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SetAmmoType))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SetAmmoType_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_reloadserver = AccessTools.Method(typeof(GameManager), nameof(GameManager.ItemReloadServer));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_reloadserver))
                {
                    codes.RemoveAt(i);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.FixedItemReloadServer))
                    });
                    codes.RemoveAt(i - 3);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapAmmoType))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SwapAmmoType_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_reloadserver = AccessTools.Method(typeof(GameManager), nameof(GameManager.ItemReloadServer));
            FieldInfo fld_actiondata = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.actionData));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_reloadserver))
                {
                    codes.RemoveAt(i);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.FixedItemReloadServer))
                    });
                    codes[i - 4].MoveLabelsFrom(codes[i - 5]);
                    codes.RemoveAt(i - 5);
                    break;
                }
                else if (code.LoadsField(fld_actiondata))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex))
                    });
                    i += 1;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.SwapAmmoType))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SwapAmmoType_ItemActionLauncher(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            FieldInfo fld_actiondata = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.actionData));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_actiondata))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex))
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.SwapSelectedAmmo))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SwapSelectedAmmo_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_reloadserver = AccessTools.Method(typeof(GameManager), nameof(GameManager.ItemReloadServer));
            MethodInfo mtd_canreload = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.CanReload));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_reloadserver))
                {
                    codes.RemoveAt(i);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.FixedItemReloadServer))
                    });
                    codes.RemoveAt(i - 3);
                    break;
                }
                else if (code.Calls(mtd_canreload))
                {
                    codes.RemoveAt(i - 2);
                    codes.InsertRange(i - 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex))
                    });
                    codes.RemoveRange(i - 9, 3);
                    codes.Insert(i - 9, new CodeInstruction(OpCodes.Ldarg_0));
                    i--;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.loadNewAmmunition))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_loadNewAmmunition_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_actiondata = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.actionData));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_actiondata))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_3),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity))
                    });
                    break;
                }
            }
            return codes;
        }
        #endregion

        //KEEP
        #region Launcher logic
        #region Launcher projectile meta and action index
        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.StartHolding))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_StartHolding_ItemActionLauncher(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var fld_meta = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Meta));
            var mtd_delete = AccessTools.Method(typeof(ItemActionLauncher), nameof(ItemActionLauncher.DeleteProjectiles));
            var prop_itemvalue = AccessTools.PropertyGetter(typeof(ItemInventoryData), nameof(ItemInventoryData.itemValue));
            var lbd_meta = generator.DeclareLocal(typeof(int));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_delete))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                        new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetMetaByActionIndex)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_meta)
                    });
                    i += 7;
                }
                else if (codes[i].LoadsField(fld_meta))
                {
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_S, lbd_meta));
                    codes.RemoveRange(i - 3, 4);
                    i -= 3;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_instantiateProjectile_ItemActionLauncher_MetaIndex(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction))
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
        [HarmonyTranspiler]
        //use custom script
        private static IEnumerable<CodeInstruction> Transpiler_instantiateProjectile_ItemActionLauncher_ReplaceScript(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_addcomponent = AccessTools.Method(typeof(GameObject), nameof(GameObject.AddComponent), Array.Empty<Type>());
            MethodInfo mtd_addcomponentprev = mtd_addcomponent.MakeGenericMethod(typeof(ProjectileMoveScript));
            MethodInfo mtd_addcomponentnew = mtd_addcomponent.MakeGenericMethod(typeof(CustomProjectileMoveScript));
            foreach (var code in instructions)
            {
                if (code.Calls(mtd_addcomponentprev))
                {
                    Log.Out("replacing launcher projectile script...");
                    code.operand = mtd_addcomponentnew;
                }
                yield return code;
            }
        }

        //copy ItemValue to projectile
        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.instantiateProjectile))]
        [HarmonyPostfix]
        private static void Postfix_instantiateProjectile_ItemActionLauncher(Transform __result)
        {
            var script = __result.GetComponent<ProjectileMoveScript>();
            var projectileValue = script.itemValueProjectile;
            var launcherValue = script.itemValueLauncher;
            projectileValue.Activated = (byte)(Mathf.Clamp01(script.actionData.strainPercent) * byte.MaxValue);
            MultiActionUtils.CopyLauncherValueToProjectile(launcherValue, projectileValue, script.actionData.indexInEntityOfAction);
        }

        //
        [HarmonyPatch(typeof(ProjectileMoveScript), nameof(ProjectileMoveScript.Fire))]
        [HarmonyPrefix]
        private static bool Prefix_Fire_ProjectileMoveScript(ProjectileMoveScript __instance, Vector3 _idealStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride, float _radius)
        {
            if (_firingEntity is EntityAlive entityAlive)
                entityAlive.MinEventContext.ItemActionData = __instance.actionData;
            if (__instance is CustomProjectileMoveScript)
            {
                __instance.ProjectileFire(_idealStartPosition, _flyDirection, _firingEntity, _hmOverride, _radius);
                return false;
            }

            return true;
        }
        #endregion

        #endregion

        #region FireEvent patches, set params
        //KEEP
        #region Ranged ExecuteAction FireEvent params
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
        [HarmonyPrefix]
        private static bool Prefix_ExecuteAction_ItemClass(ref int _actionIdx, ItemInventoryData _data, PlayerActionsLocal _playerActions)
        {

            if (_actionIdx != 0 || _playerActions == null || !(_data.holdingEntity is EntityPlayerLocal player))
            {
                return true;
            }
            _actionIdx = MultiActionManager.GetActionIndexForEntity(player);
            player.MinEventContext.ItemActionData = _data.actionData[_actionIdx];
            return true;
        }

        //why? ask TFP the fuck they are doing
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            FieldInfo fld_itemactiondata = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.ItemActionData));
            MethodInfo mtd_reloadserver = AccessTools.Method(typeof(IGameManager), nameof(IGameManager.ItemReloadServer));
            FieldInfo fld_gamemanager = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.gameManager));
            MethodInfo mtd_getkickback = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetKickbackForce));
            MethodInfo mtd_getmaxammo = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.GetMaxAmmoCount));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.StoresField(fld_itemactiondata))
                {
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                    codes.RemoveRange(i - 5, 5);
                    i -= 4;
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
                    i--;
                }
                //else if (code.Calls(mtd_getmaxammo))
                //{
                //    int j = i + 1;
                //    for (; j < codes.Count; j++)
                //    {
                //        if (codes[j].Calls(mtd_getkickback))
                //        {
                //            break;
                //        }
                //    }
                //    if (j < codes.Count)
                //    {
                //        var jumpto = codes[j - 2];
                //        var label = generator.DefineLabel();
                //        jumpto.labels.Add(label);
                //        codes.Insert(i - 2, new CodeInstruction(OpCodes.Br_S, label));
                //        codes[i - 2].MoveLabelsFrom(codes[i - 1]);
                //        i++;
                //    }
                //}
            }

            return codes;
        }
        #endregion

        #region ItemAction.CancelAction
        [HarmonyPatch(typeof(ItemActionCatapult), nameof(ItemActionCatapult.CancelAction))]
        [HarmonyPrefix]
        private static bool Prefix_CancelAction_ItemActionCatapult(ItemActionData _actionData)
        {
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
            return true;
        }
        #endregion

        #region ItemActionDynamicMelee.Raycast
        [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast))]
        [HarmonyPrefix]
        private static bool Prefix_Raycast_ItemActionDynamicMelee(ItemActionDynamic.ItemActionDynamicData _actionData)
        {
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
            return true;
        }
        #endregion

        #region Inventory.FireEvent, set current action
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.FireEvent))]
        [HarmonyPrefix]
        private static bool Prefix_FireEvent_Inventory(Inventory __instance)
        {
            MultiActionUtils.SetMinEventParamsByEntityInventory(__instance.entity);
            return true;
        }
        #endregion

        #region Inventory.syncHeldItem, set current action
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.syncHeldItem))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_syncHeldItem_Inventory(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_test = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
            var fld_itemvalue = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.ItemValue));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].StoresField(fld_itemvalue) && codes[i - 7].Calls(mtd_test))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(Inventory), nameof(Inventory.entity)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.SetMinEventParamsByEntityInventory))
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region ItemValue.FireEvent, read current action
        [HarmonyPatch(typeof(ItemValue), nameof(ItemValue.FireEvent))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_FireEvent_ItemValue(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            MethodInfo mtd_fireevent = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.FireEvent));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_action) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                    if (codes[i - 9].opcode == OpCodes.Ret)
                    {
                        codes.InsertRange(i - 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_2),
                            CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByEventParams)),
                            new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                        });
                        i += 3;
                    }
                }
                else if (code.LoadsField(fld_ammoindex))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    i++;
                }
                //action exclude mods
                else if (code.Calls(mtd_fireevent) && codes[i + 1].opcode != OpCodes.Ldloc_0)
                {
                    for (int j = i; j >= 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Brfalse_S || codes[j].opcode == OpCodes.Brfalse)
                        {
                            var label = codes[j].operand;
                            codes.InsertRange(j + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, codes[j + 2].operand),
                                new CodeInstruction(codes[j + 3].opcode, codes[j + 3].operand),
                                new CodeInstruction(OpCodes.Ldelem_Ref),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldloc_S, lbd_index),
                                CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ShouldExcludeTrigger)),
                                new CodeInstruction(OpCodes.Brtrue_S, label)
                            });
                            i += 10;
                            break;
                        }
                    }
                }
            }

            return codes;
        }
        #endregion

        //onSelfEquipStop, onSelfHoldingItemCreated, onSelfEquipStart are not available for individual action,

        //some are already set in update or execute action

        #endregion

        #region EffectManager.GetValue patches, set params
        //set correct action index for ItemValue
        [HarmonyPatch(typeof(ItemValue), nameof(ItemValue.ModifyValue))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ModifyValue_ItemValue(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            FieldInfo fld_mods = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Modifications));
            FieldInfo fld_cos = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.CosmeticMods));
            MethodInfo mtd_modify = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.ModifyValue));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Stloc_1)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByEntityEventParams)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                }
                else if (code.LoadsField(fld_action) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
                else if (code.LoadsField(fld_ammoindex))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    i++;
                }
                else if (code.Calls(mtd_modify))
                {
                    for (int j = i; j >= 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Brfalse_S || codes[j].opcode == OpCodes.Brfalse)
                        {
                            var label = codes[j].operand;
                            codes.InsertRange(j + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, codes[j + 2].operand),
                                new CodeInstruction(codes[j + 3].opcode, codes[j + 3].operand),
                                new CodeInstruction(OpCodes.Ldelem_Ref),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldloc_S, lbd_index),
                                CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ShouldExcludePassive)),
                                new CodeInstruction(OpCodes.Brtrue_S, label)
                            });
                            i += 10;
                            break;
                        }
                    }
                }
            }

            return codes;
        }

        //only current mode should execute OnHUD
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnHUD))]
        [HarmonyPrefix]
        private static bool Prefix_OnHUD_EntityPlayerLocal(EntityPlayerLocal __instance, out ItemActionData __state)
        {
            __state = __instance.MinEventContext.ItemActionData;
            MultiActionUtils.SetMinEventParamsByEntityInventory(__instance);
            return true;
        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnHUD))]
        [HarmonyPostfix]
        private static void Postfix_OnHUD_EntityPlayerLocal(EntityPlayerLocal __instance, ItemActionData __state)
        {
            __instance.MinEventContext.ItemActionData = __state;
        }

        //for passive value calc
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.guiDrawCrosshair))]
        [HarmonyPrefix]
        private static bool Prefix_guiDrawCrosshair_EntityPlayerLocal(EntityPlayerLocal __instance)
        {
            MultiActionUtils.SetMinEventParamsByEntityInventory(__instance);
            return true;
        }

        //draw crosshair for current action
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.guiDrawCrosshair))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_guiDrawCrosshair_EntityPlayerLocal(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_actiondata = AccessTools.Field(typeof(ItemInventoryData), nameof(ItemInventoryData.actionData));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_1)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 3;
                }
                else if (codes[i].LoadsField(fld_actiondata) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.ExecuteBuffActions))]
        [HarmonyPrefix]
        private static bool Prefix_ExecuteBuffActions_ItemAction(int instigatorId, out (EntityAlive entity, ItemActionData actionData) __state)
        {
            __state = default;
            EntityAlive entity = GameManager.Instance.World.GetEntity(instigatorId) as EntityAlive;
            if (entity != null)
            {
                __state.entity = entity;
                __state.actionData = entity.MinEventContext.ItemActionData;
                MultiActionUtils.SetMinEventParamsByEntityInventory(entity);
            }
            else
                return false;
            return true;
        }

        [HarmonyPatch(typeof(ItemAction), nameof(ItemAction.ExecuteBuffActions))]
        [HarmonyPostfix]
        private static void Postfix_ExecuteBuffActions_ItemAction((EntityAlive entity, ItemActionData actionData) __state, bool __runOriginal)
        {
            if (__runOriginal && __state.entity != null)
                __state.entity.MinEventContext.ItemActionData = __state.actionData;
        }

        //ItemAction.GetDismemberChance already set
        //ItemActionDynamic.GetExecuteActionTarget not needed

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
        [HarmonyPrefix]
        private static bool Prefix_ItemActionEffects_ItemActionLauncher(ItemActionData _actionData, out ItemActionData __state)
        {
            __state = _actionData.invData.holdingEntity.MinEventContext.ItemActionData;
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
            return true;
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
        [HarmonyPostfix]
        private static void Postfix_ItemActionEffects_ItemActionLauncher(ItemActionData _actionData, ItemActionData __state)
        {
            _actionData.invData.holdingEntity.MinEventContext.ItemActionData = __state;
        }

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ClampAmmoCount))]
        [HarmonyPrefix]
        private static bool Prefix_ClampAmmoCount_ItemActionLauncher(ItemActionLauncher.ItemActionDataLauncher actionData, out ItemActionData __state)
        {
            __state = actionData.invData.holdingEntity.MinEventContext.ItemActionData;
            actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
            return true;
        }

        [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ClampAmmoCount))]
        [HarmonyPostfix]
        private static void Postfix_ClampAmmoCount_ItemActionLauncher(ItemActionLauncher.ItemActionDataLauncher actionData, ItemActionData __state)
        {
            actionData.invData.holdingEntity.MinEventContext.ItemActionData = __state;
        }
        #endregion

        #region EffectManager.GetValuesAndSources patches, set params

        [HarmonyPatch(typeof(EntityStats), nameof(EntityStats.Update))]
        [HarmonyPrefix]
        private static bool Prefix_Update_EntityStats(EntityStats __instance)
        {
            MultiActionUtils.SetMinEventParamsByEntityInventory(__instance.m_entity);
            return true;
        }

        //set correct action index for ItemValue
        [HarmonyPatch(typeof(ItemValue), nameof(ItemValue.GetModifiedValueData))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetModifiedValueData_ItemValue(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            LocalBuilder lbd_index = generator.DeclareLocal(typeof(int));

            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            FieldInfo fld_mods = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.Modifications));
            FieldInfo fld_cos = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.CosmeticMods));
            MethodInfo mtd_getvalue = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.GetModifiedValueData));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Stloc_0)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_3),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByEntityEventParams)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                }
                else if (code.LoadsField(fld_action) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes[i + 1].opcode = OpCodes.Ldloc_S;
                    codes[i + 1].operand = lbd_index;
                }
                else if (code.LoadsField(fld_ammoindex))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    i++;
                }
                else if (code.Calls(mtd_getvalue) && codes[i + 1].opcode != OpCodes.Ldloc_0)
                {
                    for (int j = i; j >= 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Brfalse_S || codes[j].opcode == OpCodes.Brfalse)
                        {
                            var label = codes[j].operand;
                            codes.InsertRange(j + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, codes[j + 2].operand),
                                new CodeInstruction(codes[j + 3].opcode, codes[j + 3].operand),
                                new CodeInstruction(OpCodes.Ldelem_Ref),
                                CodeInstruction.LoadField(typeof(ItemValue), nameof(ItemValue.type)),
                                new CodeInstruction(OpCodes.Ldloc_S, lbd_index),
                                CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ShouldExcludePassive)),
                                new CodeInstruction(OpCodes.Brtrue_S, label)
                            });
                            i += 10;
                            break;
                        }
                    }
                }
            }

            return codes;
        }
        #endregion

        //KEEP
        #region Misc
        //load correct property for melee
        [HarmonyPatch(typeof(AnimatorMeleeAttackState), nameof(AnimatorMeleeAttackState.OnStateEnter))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnStateEnter_AnimatorMeleeAttackState(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var fld_actionindex = AccessTools.Field(typeof(AnimatorMeleeAttackState), nameof(AnimatorMeleeAttackState.actionIndex));
            MethodInfo mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_actionindex) && codes[i + 2].opcode == OpCodes.Ldstr)
                {
                    string property = codes[i + 2].operand.ToString();
                    property = property.Split('.')[1];
                    codes.RemoveRange(i + 1, 4);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldstr, property),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetPropertyName))
                    });
                    i -= 2;
                }
                else if (code.Calls(mtd_getvalue))
                {
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorMeleeAttackState), "entity"),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(AnimatorMeleeAttackState), "actionIndex"),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.SetMinEventParamsActionData))
                    });
                    i += 5;
                }
            }

            return codes;
        }

        //make sure it's set to current action after for loop
        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.OnHoldingUpdate))]
        [HarmonyPostfix]
        private static void Postfix_OnHoldingUpdate_ItemClass(ItemInventoryData _data)
        {
            MultiActionUtils.SetMinEventParamsByEntityInventory(_data.holdingEntity);
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.StopHolding))]
        [HarmonyPostfix]
        private static void Postfix_StopHolding_ItemClass(ItemInventoryData _data)
        {
            if (_data.holdingEntity != null)
            {
                MultiActionUtils.SetMinEventParamsByEntityInventory(_data.holdingEntity);
                MultiActionManager.SetMappingForEntity(_data.holdingEntity.entityId, null);
            }
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.StartHolding))]
        [HarmonyPostfix]
        private static void Postfix_StartHolding_ItemClass(ItemInventoryData _data)
        {
            MultiActionUtils.SetMinEventParamsByEntityInventory(_data.holdingEntity);
        }

        //should be fixed in Harmony 2.12.0.0
        //[HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
        //[HarmonyPrefix]
        //private static bool Prefix_parseItem_ItemClassesFromXml(XElement _node)
        //{
        //    DynamicProperties dynamicProperties = new DynamicProperties();
        //    string attribute = _node.GetAttribute("name");
        //    if (attribute.Length == 0)
        //    {
        //        throw new Exception("Attribute 'name' missing on item");
        //    }
        //    //here
        //    List<IRequirement>[] array = new List<IRequirement>[ItemClass.cMaxActionNames];
        //    for (int i = 0; i < array.Length; i++)
        //    {
        //        array[i] = new List<IRequirement>();
        //    }

        //    foreach (XElement item in _node.Elements("property"))
        //    {
        //        dynamicProperties.Add(item);
        //        string attribute2 = item.GetAttribute("class");
        //        if (attribute2.StartsWith("Action"))
        //        {
        //            int num = attribute2[attribute2.Length - 1] - '0';
        //            array[num].AddRange(RequirementBase.ParseRequirements(item));
        //        }
        //    }

        //    if (dynamicProperties.Values.ContainsKey("Extends"))
        //    {
        //        string text = dynamicProperties.Values["Extends"];
        //        ItemClass itemClass = ItemClass.GetItemClass(text);
        //        if (itemClass == null)
        //        {
        //            throw new Exception($"Extends item {text} is not specified for item {attribute}'");
        //        }

        //        HashSet<string> hashSet = new HashSet<string> { Block.PropCreativeMode };
        //        if (dynamicProperties.Params1.ContainsKey("Extends"))
        //        {
        //            string[] array2 = dynamicProperties.Params1["Extends"].Split(new[] { ',' }, StringSplitOptions.None);
        //            foreach (string text2 in array2)
        //            {
        //                hashSet.Add(text2.Trim());
        //            }
        //        }

        //        DynamicProperties dynamicProperties2 = new DynamicProperties();
        //        dynamicProperties2.CopyFrom(itemClass.Properties, hashSet);
        //        dynamicProperties2.CopyFrom(dynamicProperties);
        //        dynamicProperties = dynamicProperties2;
        //    }

        //    ItemClass itemClass2;
        //    if (dynamicProperties.Values.ContainsKey("Class"))
        //    {
        //        string text3 = dynamicProperties.Values["Class"];
        //        if (!text3.Contains(","))
        //        {
        //            text3 += ",Assembly-CSharp";
        //        }
        //        try
        //        {
        //            itemClass2 = (ItemClass)Activator.CreateInstance(Type.GetType(text3));
        //        }
        //        catch (Exception)
        //        {
        //            throw new Exception("No item class '" + text3 + " found!");
        //        }
        //    }
        //    else
        //    {
        //        itemClass2 = new ItemClass();
        //    }

        //    itemClass2.Properties = dynamicProperties;
        //    if (dynamicProperties.Params1.ContainsKey("Extends"))
        //    {
        //        string text4 = dynamicProperties.Values["Extends"];
        //        if (ItemClass.GetItemClass(text4) == null)
        //        {
        //            throw new Exception($"Extends item {text4} is not specified for item {attribute}'");
        //        }
        //    }

        //    itemClass2.Effects = MinEffectController.ParseXml(_node, null, MinEffectController.SourceParentType.ItemClass, itemClass2.Id);
        //    itemClass2.SetName(attribute);
        //    itemClass2.setLocalizedItemName(Localization.Get(attribute));
        //    if (dynamicProperties.Values.ContainsKey("Stacknumber"))
        //    {
        //        itemClass2.Stacknumber = new DataItem<int>(int.Parse(dynamicProperties.Values["Stacknumber"]));
        //    }
        //    else
        //    {
        //        itemClass2.Stacknumber = new DataItem<int>(500);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("Canhold"))
        //    {
        //        itemClass2.SetCanHold(StringParsers.ParseBool(dynamicProperties.Values["Canhold"]));
        //    }

        //    if (dynamicProperties.Values.ContainsKey("Candrop"))
        //    {
        //        itemClass2.SetCanDrop(StringParsers.ParseBool(dynamicProperties.Values["Candrop"]));
        //    }

        //    if (!dynamicProperties.Values.ContainsKey("Material"))
        //    {
        //        throw new Exception("Attribute 'material' missing on item '" + attribute + "'");
        //    }

        //    itemClass2.MadeOfMaterial = MaterialBlock.fromString(dynamicProperties.Values["Material"]);
        //    if (itemClass2.MadeOfMaterial == null)
        //    {
        //        throw new Exception("Attribute 'material' '" + dynamicProperties.Values["Material"] + "' refers to not existing material in item '" + attribute + "'");
        //    }

        //    if (!dynamicProperties.Values.ContainsKey("Meshfile") && itemClass2.CanHold())
        //    {
        //        throw new Exception("Attribute 'Meshfile' missing on item '" + attribute + "'");
        //    }

        //    itemClass2.MeshFile = dynamicProperties.Values["Meshfile"];
        //    DataLoader.PreloadBundle(itemClass2.MeshFile);
        //    StringParsers.TryParseFloat(dynamicProperties.Values["StickyOffset"], out itemClass2.StickyOffset);
        //    StringParsers.TryParseFloat(dynamicProperties.Values["StickyColliderRadius"], out itemClass2.StickyColliderRadius);
        //    StringParsers.TryParseSInt32(dynamicProperties.Values["StickyColliderUp"], out itemClass2.StickyColliderUp);
        //    StringParsers.TryParseFloat(dynamicProperties.Values["StickyColliderLength"], out itemClass2.StickyColliderLength);
        //    itemClass2.StickyMaterial = dynamicProperties.Values["StickyMaterial"];
        //    if (dynamicProperties.Values.ContainsKey("ImageEffectOnActive"))
        //    {
        //        itemClass2.ImageEffectOnActive = new DataItem<string>(dynamicProperties.Values["ImageEffectOnActive"]);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("Active"))
        //    {
        //        itemClass2.Active = new DataItem<bool>(_startValue: false);
        //    }

        //    if (dynamicProperties.Values.ContainsKey(ItemClass.PropIsSticky))
        //    {
        //        itemClass2.IsSticky = StringParsers.ParseBool(dynamicProperties.Values[ItemClass.PropIsSticky]);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("DropMeshfile") && itemClass2.CanHold())
        //    {
        //        itemClass2.DropMeshFile = dynamicProperties.Values["DropMeshfile"];
        //        DataLoader.PreloadBundle(itemClass2.DropMeshFile);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("HandMeshfile") && itemClass2.CanHold())
        //    {
        //        itemClass2.HandMeshFile = dynamicProperties.Values["HandMeshfile"];
        //        DataLoader.PreloadBundle(itemClass2.HandMeshFile);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("HoldType"))
        //    {
        //        string s = dynamicProperties.Values["HoldType"];
        //        int result = 0;
        //        if (!int.TryParse(s, out result))
        //        {
        //            throw new Exception("Cannot parse attribute hold_type for item '" + attribute + "'");
        //        }

        //        itemClass2.HoldType = new DataItem<int>(result);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("RepairTools"))
        //    {
        //        string[] array3 = dynamicProperties.Values["RepairTools"].Replace(" ", "").Split(new[] { ',' }, StringSplitOptions.None);
        //        DataItem<string>[] array4 = new DataItem<string>[array3.Length];
        //        for (int k = 0; k < array3.Length; k++)
        //        {
        //            array4[k] = new DataItem<string>(array3[k]);
        //        }

        //        itemClass2.RepairTools = new ItemData.DataItemArrayRepairTools(array4);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("RepairAmount"))
        //    {
        //        int result2 = 0;
        //        int.TryParse(dynamicProperties.Values["RepairAmount"], out result2);
        //        itemClass2.RepairAmount = new DataItem<int>(result2);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("RepairTime"))
        //    {
        //        float _result = 0f;
        //        StringParsers.TryParseFloat(dynamicProperties.Values["RepairTime"], out _result);
        //        itemClass2.RepairTime = new DataItem<float>(_result);
        //    }
        //    else if (itemClass2.RepairAmount != null)
        //    {
        //        itemClass2.RepairTime = new DataItem<float>(1f);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("Degradation"))
        //    {
        //        itemClass2.MaxUseTimes = new DataItem<int>(int.Parse(dynamicProperties.Values["Degradation"]));
        //    }
        //    else
        //    {
        //        itemClass2.MaxUseTimes = new DataItem<int>(0);
        //        itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(_startValue: false);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("DegradationBreaksAfter"))
        //    {
        //        itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(StringParsers.ParseBool(dynamicProperties.Values["DegradationBreaksAfter"]));
        //    }
        //    else if (dynamicProperties.Values.ContainsKey("Degradation"))
        //    {
        //        itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(_startValue: true);
        //    }

        //    if (dynamicProperties.Values.ContainsKey("EconomicValue"))
        //    {
        //        itemClass2.EconomicValue = StringParsers.ParseFloat(dynamicProperties.Values["EconomicValue"]);
        //    }

        //    if (dynamicProperties.Classes.ContainsKey("Preview"))
        //    {
        //        DynamicProperties dynamicProperties3 = dynamicProperties.Classes["Preview"];
        //        itemClass2.Preview = new PreviewData();
        //        if (dynamicProperties3.Values.ContainsKey("Zoom"))
        //        {
        //            itemClass2.Preview.Zoom = new DataItem<int>(int.Parse(dynamicProperties3.Values["Zoom"]));
        //        }

        //        if (dynamicProperties3.Values.ContainsKey("Pos"))
        //        {
        //            itemClass2.Preview.Pos = new DataItem<Vector2>(StringParsers.ParseVector2(dynamicProperties3.Values["Pos"]));
        //        }
        //        else
        //        {
        //            itemClass2.Preview.Pos = new DataItem<Vector2>(Vector2.zero);
        //        }

        //        if (dynamicProperties3.Values.ContainsKey("Rot"))
        //        {
        //            itemClass2.Preview.Rot = new DataItem<Vector3>(StringParsers.ParseVector3(dynamicProperties3.Values["Rot"]));
        //        }
        //        else
        //        {
        //            itemClass2.Preview.Rot = new DataItem<Vector3>(Vector3.zero);
        //        }
        //    }

        //    for (int l = 0; l < itemClass2.Actions.Length; l++)
        //    {
        //        string text5 = ItemClass.itemActionNames[l];
        //        if (dynamicProperties.Classes.ContainsKey(text5))
        //        {
        //            if (!dynamicProperties.Values.ContainsKey(text5 + ".Class"))
        //            {
        //                throw new Exception("No class attribute found on " + text5 + " in item with '" + attribute + "'");
        //            }

        //            string text6 = dynamicProperties.Values[text5 + ".Class"];
        //            ItemAction itemAction;
        //            try
        //            {
        //                itemAction = (ItemAction)Activator.CreateInstance(ReflectionHelpers.GetTypeWithPrefix("ItemAction", text6));
        //            }
        //            catch (Exception)
        //            {
        //                throw new Exception("ItemAction class '" + text6 + " could not be instantiated");
        //            }

        //            itemAction.item = itemClass2;
        //            itemAction.ActionIndex = l;
        //            itemAction.ReadFrom(dynamicProperties.Classes[text5]);
        //            if (array[l].Count > 0)
        //            {
        //                itemAction.ExecutionRequirements = array[l];
        //            }

        //            itemClass2.Actions[l] = itemAction;
        //        }
        //    }

        //    itemClass2.Init();
        //    return false;
        //}

        /// <summary>
        /// fix requirement array count
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_parseItem_ItemClassesFromXml(IEnumerable<CodeInstruction> instructions)
        {
            bool isLastInsThrow = false;
            foreach (var code in instructions)
            {
                if (isLastInsThrow)
                {
                    isLastInsThrow = false;
                    if (code.opcode == OpCodes.Ldc_I4_3)
                    {
                        code.opcode = OpCodes.Ldc_I4_5;
                    }
                }
                if (code.opcode == OpCodes.Throw)
                {
                    isLastInsThrow = true;
                }
                yield return code;
            }
        }

        /*
        [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
        [HarmonyPrefix]
        private static bool Prefix_parseItem_ItemClassesFromXml(XElement _node)
        {
            //throw new Exception("Exception thrown from here!");
            DynamicProperties dynamicProperties = new DynamicProperties();
            string attribute = _node.GetAttribute("name");
            if (attribute.Length == 0)
            {
                throw new Exception("Attribute 'name' missing on item");
            }
            //Log.Out($"Parsing item {attribute}...");
            List<IRequirement>[] array = new List<IRequirement>[5];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new List<IRequirement>();
            }

            foreach (XElement item in _node.Elements("property"))
            {
                dynamicProperties.Add(item);
                string attribute2 = item.GetAttribute("class");
                if (attribute2.StartsWith("Action"))
                {
                    int num = attribute2[attribute2.Length - 1] - 48;
                    array[num].AddRange(RequirementBase.ParseRequirements(item));
                }
            }

            if (dynamicProperties.Values.ContainsKey("Extends"))
            {
                string text = dynamicProperties.Values["Extends"];
                ItemClass itemClass = ItemClass.GetItemClass(text);
                if (itemClass == null)
                {
                    throw new Exception($"Extends item {text} is not specified for item {attribute}'");
                }

                HashSet<string> hashSet = new HashSet<string> { Block.PropCreativeMode };
                if (dynamicProperties.Params1.ContainsKey("Extends"))
                {
                    string[] array2 = dynamicProperties.Params1["Extends"].Split(',');
                    foreach (string text2 in array2)
                    {
                        hashSet.Add(text2.Trim());
                    }
                }

                DynamicProperties dynamicProperties2 = new DynamicProperties();
                dynamicProperties2.CopyFrom(itemClass.Properties, hashSet);
                dynamicProperties2.CopyFrom(dynamicProperties);
                dynamicProperties = dynamicProperties2;
            }

            ItemClass itemClass2;
            if (dynamicProperties.Values.ContainsKey("Class"))
            {
                string text3 = dynamicProperties.Values["Class"];
                if (!text3.Contains(","))
                {
                    text3 += ",Assembly-CSharp";
                }
                try
                {
                    itemClass2 = (ItemClass)Activator.CreateInstance(Type.GetType(text3));
                }
                catch (Exception)
                {
                    throw new Exception("No item class '" + text3 + "' found!");
                }
            }
            else
            {
                itemClass2 = new ItemClass();
            }

            itemClass2.Properties = dynamicProperties;
            if (dynamicProperties.Params1.ContainsKey("Extends"))
            {
                string text4 = dynamicProperties.Values["Extends"];
                if (ItemClass.GetItemClass(text4) == null)
                {
                    throw new Exception($"Extends item {text4} is not specified for item {attribute}'");
                }
            }

            itemClass2.Effects = MinEffectController.ParseXml(_node, null, MinEffectController.SourceParentType.ItemClass, itemClass2.Id);
            itemClass2.SetName(attribute);
            itemClass2.setLocalizedItemName(Localization.Get(attribute));
            if (dynamicProperties.Values.ContainsKey("Stacknumber"))
            {
                itemClass2.Stacknumber = new DataItem<int>(int.Parse(dynamicProperties.Values["Stacknumber"]));
            }
            else
            {
                itemClass2.Stacknumber = new DataItem<int>(500);
            }

            if (dynamicProperties.Values.ContainsKey("Canhold"))
            {
                itemClass2.SetCanHold(StringParsers.ParseBool(dynamicProperties.Values["Canhold"]));
            }

            if (dynamicProperties.Values.ContainsKey("Candrop"))
            {
                itemClass2.SetCanDrop(StringParsers.ParseBool(dynamicProperties.Values["Candrop"]));
            }

            if (!dynamicProperties.Values.ContainsKey("Material"))
            {
                throw new Exception("Attribute 'material' missing on item '" + attribute + "'");
            }

            itemClass2.MadeOfMaterial = MaterialBlock.fromString(dynamicProperties.Values["Material"]);
            if (itemClass2.MadeOfMaterial == null)
            {
                throw new Exception("Attribute 'material' '" + dynamicProperties.Values["Material"] + "' refers to not existing material in item '" + attribute + "'");
            }

            if (!dynamicProperties.Values.ContainsKey("Meshfile") && itemClass2.CanHold())
            {
                throw new Exception("Attribute 'Meshfile' missing on item '" + attribute + "'");
            }

            itemClass2.MeshFile = dynamicProperties.Values["Meshfile"];
            DataLoader.PreloadBundle(itemClass2.MeshFile);
            StringParsers.TryParseFloat(dynamicProperties.Values["StickyOffset"], out itemClass2.StickyOffset);
            StringParsers.TryParseFloat(dynamicProperties.Values["StickyColliderRadius"], out itemClass2.StickyColliderRadius);
            StringParsers.TryParseSInt32(dynamicProperties.Values["StickyColliderUp"], out itemClass2.StickyColliderUp);
            StringParsers.TryParseFloat(dynamicProperties.Values["StickyColliderLength"], out itemClass2.StickyColliderLength);
            itemClass2.StickyMaterial = dynamicProperties.Values["StickyMaterial"];
            if (dynamicProperties.Values.ContainsKey("ImageEffectOnActive"))
            {
                itemClass2.ImageEffectOnActive = new DataItem<string>(dynamicProperties.Values["ImageEffectOnActive"]);
            }

            if (dynamicProperties.Values.ContainsKey("Active"))
            {
                itemClass2.Active = new DataItem<bool>(_startValue: false);
            }

            if (dynamicProperties.Values.ContainsKey(ItemClass.PropIsSticky))
            {
                itemClass2.IsSticky = StringParsers.ParseBool(dynamicProperties.Values[ItemClass.PropIsSticky]);
            }

            if (dynamicProperties.Values.ContainsKey("DropMeshfile") && itemClass2.CanHold())
            {
                itemClass2.DropMeshFile = dynamicProperties.Values["DropMeshfile"];
                DataLoader.PreloadBundle(itemClass2.DropMeshFile);
            }

            if (dynamicProperties.Values.ContainsKey("HandMeshfile") && itemClass2.CanHold())
            {
                itemClass2.HandMeshFile = dynamicProperties.Values["HandMeshfile"];
                DataLoader.PreloadBundle(itemClass2.HandMeshFile);
            }

            if (dynamicProperties.Values.ContainsKey("HoldType"))
            {
                string s = dynamicProperties.Values["HoldType"];
                int result = 0;
                if (!int.TryParse(s, out result))
                {
                    throw new Exception("Cannot parse attribute hold_type for item '" + attribute + "'");
                }

                itemClass2.HoldType = new DataItem<int>(result);
            }

            if (dynamicProperties.Values.ContainsKey("RepairTools"))
            {
                string[] array3 = dynamicProperties.Values["RepairTools"].Replace(" ", "").Split(',');
                DataItem<string>[] array4 = new DataItem<string>[array3.Length];
                for (int k = 0; k < array3.Length; k++)
                {
                    array4[k] = new DataItem<string>(array3[k]);
                }

                itemClass2.RepairTools = new ItemData.DataItemArrayRepairTools(array4);
            }

            if (dynamicProperties.Values.ContainsKey("RepairAmount"))
            {
                int result2 = 0;
                int.TryParse(dynamicProperties.Values["RepairAmount"], out result2);
                itemClass2.RepairAmount = new DataItem<int>(result2);
            }

            if (dynamicProperties.Values.ContainsKey("RepairTime"))
            {
                float _result = 0f;
                StringParsers.TryParseFloat(dynamicProperties.Values["RepairTime"], out _result);
                itemClass2.RepairTime = new DataItem<float>(_result);
            }
            else if (itemClass2.RepairAmount != null)
            {
                itemClass2.RepairTime = new DataItem<float>(1f);
            }

            if (dynamicProperties.Values.ContainsKey("Degradation"))
            {
                itemClass2.MaxUseTimes = new DataItem<int>(int.Parse(dynamicProperties.Values["Degradation"]));
            }
            else
            {
                itemClass2.MaxUseTimes = new DataItem<int>(0);
                itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(_startValue: false);
            }

            if (dynamicProperties.Values.ContainsKey("DegradationBreaksAfter"))
            {
                itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(StringParsers.ParseBool(dynamicProperties.Values["DegradationBreaksAfter"]));
            }
            else if (dynamicProperties.Values.ContainsKey("Degradation"))
            {
                itemClass2.MaxUseTimesBreaksAfter = new DataItem<bool>(_startValue: true);
            }

            if (dynamicProperties.Values.ContainsKey("EconomicValue"))
            {
                itemClass2.EconomicValue = StringParsers.ParseFloat(dynamicProperties.Values["EconomicValue"]);
            }

            if (dynamicProperties.Classes.ContainsKey("Preview"))
            {
                DynamicProperties dynamicProperties3 = dynamicProperties.Classes["Preview"];
                itemClass2.Preview = new PreviewData();
                if (dynamicProperties3.Values.ContainsKey("Zoom"))
                {
                    itemClass2.Preview.Zoom = new DataItem<int>(int.Parse(dynamicProperties3.Values["Zoom"]));
                }

                if (dynamicProperties3.Values.ContainsKey("Pos"))
                {
                    itemClass2.Preview.Pos = new DataItem<Vector2>(StringParsers.ParseVector2(dynamicProperties3.Values["Pos"]));
                }
                else
                {
                    itemClass2.Preview.Pos = new DataItem<Vector2>(Vector2.zero);
                }

                if (dynamicProperties3.Values.ContainsKey("Rot"))
                {
                    itemClass2.Preview.Rot = new DataItem<Vector3>(StringParsers.ParseVector3(dynamicProperties3.Values["Rot"]));
                }
                else
                {
                    itemClass2.Preview.Rot = new DataItem<Vector3>(Vector3.zero);
                }
            }

            for (int l = 0; l < itemClass2.Actions.Length; l++)
            {
                string text5 = ItemClass.itemActionNames[l];
                if (dynamicProperties.Classes.ContainsKey(text5))
                {
                    if (!dynamicProperties.Values.ContainsKey(text5 + ".Class"))
                    {
                        throw new Exception("No class attribute found on " + text5 + " in item with '" + attribute + "'");
                    }

                    string text6 = dynamicProperties.Values[text5 + ".Class"];
                    ItemAction itemAction;
                    try
                    {
                        itemAction = (ItemAction)Activator.CreateInstance(ReflectionHelpers.GetTypeWithPrefix("ItemAction", text6));
                    }
                    catch (Exception)
                    {
                        throw new Exception("ItemAction class '" + text6 + " could not be instantiated");
                    }

                    itemAction.item = itemClass2;
                    itemAction.ActionIndex = l;
                    itemAction.ReadFrom(dynamicProperties.Classes[text5]);
                    if (array[l].Count > 0)
                    {
                        itemAction.ExecutionRequirements = array[l];
                    }

                    itemClass2.Actions[l] = itemAction;
                }
            }

            itemClass2.Init();
            return false;
        }
        */

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.onInventoryChanged))]
        [HarmonyPrefix]
        private static bool Prefix_onInventoryChanged_Inventory(Inventory __instance)
        {
            if (__instance.entity != null)
                MultiActionManager.UpdateLocalMetaSave(__instance.entity.entityId);
            return true;
        }
        #endregion

        //KEEP
        #region Action mode handling
        [HarmonyPatch(typeof(NetPackagePlayerStats), nameof(NetPackagePlayerStats.ProcessPackage))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ProcessPackage_NetPackagePlayerStats(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_entityid = AccessTools.Field(typeof(NetPackagePlayerStats), nameof(NetPackagePlayerStats.entityId));
            var fld_itemstack = AccessTools.Field(typeof(NetPackagePlayerStats), nameof(NetPackagePlayerStats.holdingItemStack));
            codes.InsertRange(codes.Count - 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, fld_entityid),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, fld_itemstack),
                CodeInstruction.Call(typeof(MultiActionPatches), nameof(CheckItemValueMode))
            });

            return codes;
        }
        [HarmonyPatch(typeof(NetPackageHoldingItem), nameof(NetPackageHoldingItem.ProcessPackage))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ProcessPackage_NetPackageHoldingItem(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_entityid = AccessTools.Field(typeof(NetPackagePlayerStats), nameof(NetPackageHoldingItem.entityId));
            var fld_itemstack = AccessTools.Field(typeof(NetPackagePlayerStats), nameof(NetPackageHoldingItem.holdingItemStack));
            codes.InsertRange(codes.Count - 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, fld_entityid),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, fld_itemstack),
                CodeInstruction.Call(typeof(MultiActionPatches), nameof(CheckItemValueMode))
            });

            return codes;
        }

        private static void CheckItemValueMode(int entityId, ItemStack holdingItemStack)
        {
            ItemValue itemValue = holdingItemStack.itemValue;
            if (itemValue.HasMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX))
            {
                int mode = (int)itemValue.GetMetadata(MultiActionMapping.STR_MULTI_ACTION_INDEX);
                if (MultiActionManager.SetModeForEntity(entityId, mode) && ConnectionManager.Instance.IsServer)
                {
                    ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageEntityActionIndex>().Setup(entityId, mode), false, -1, entityId);
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.UpdateTick))]
        [HarmonyPostfix]
        private static void Postfix_UpdateTick_GameManager(GameManager __instance)
        {
            if (MultiActionManager.LocalModeChanged && __instance.m_World != null)
            {
                MultiActionManager.LocalModeChanged = false;
                int playerID = __instance.m_World.GetPrimaryPlayerId();
                if (ConnectionManager.Instance.IsClient)
                {
                    ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageEntityActionIndex>().Setup(playerID, MultiActionManager.GetModeForEntity(playerID)));
                }
                else
                {
                    ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageEntityActionIndex>().Setup(playerID, MultiActionManager.GetModeForEntity(playerID)), false, -1, playerID);
                }
            }
        }
        #endregion

        #region Input Handling
        [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
        [HarmonyPrefix]
        private static bool Prefix_Update_PlayerMoveController(PlayerMoveController __instance)
        {
            if (DroneManager.Debug_LocalControl || !__instance.gameManager.gameStateManager.IsGameStarted() || GameStats.GetInt(EnumGameStats.GameState) != 1)
                return true;

            bool isUIOpen = __instance.windowManager.IsCursorWindowOpen() || __instance.windowManager.IsInputActive() || __instance.windowManager.IsModalWindowOpen();

            MultiActionManager.UpdateLocalInput(__instance.entityPlayerLocal, __instance.playerInput, isUIOpen, Time.deltaTime);

            return true;
        }
        #endregion

        #region HUD display
        /// <summary>
        /// redirect check to alternative action module
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.HasChanged))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_HasChanged_XUiC_HUDStatBar(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_edittool = AccessTools.Method(typeof(ItemAction), nameof(ItemAction.IsEditingTool));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.Calls(mtd_edittool))
                {
                    codes.RemoveRange(i - 1, 3);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.SetupActiveItemEntry))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SetupActiveItemEntry_XUiC_HUDStatBar(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_index = generator.DeclareLocal(typeof(int));
            FieldInfo fld_action = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));
            FieldInfo fld_ammoindex = AccessTools.Field(typeof(ItemValue), nameof(ItemValue.SelectedAmmoTypeIndex));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_action))
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_2),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData)),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_index)
                    });
                    i += 3;
                }
                else if (codes[i].LoadsField(fld_ammoindex))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetSelectedAmmoIndexByActionIndex));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_S, lbd_index));
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(XUiC_Radial), nameof(XUiC_Radial.handleActivatableItemCommand))]
        [HarmonyPrefix]
        private static bool Prefix_handleActivatableItemCommand_XUiC_Radial(XUiC_Radial _sender)
        {
            EntityPlayerLocal entityPlayer = _sender.xui.playerUI.entityPlayer;
            MultiActionUtils.SetMinEventParamsByEntityInventory(entityPlayer);
            return true;
        }

        [HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValue))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetBindingValue_XUiC_ItemInfoWindow(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_actions = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_actions) && codes[i + 1].opcode == OpCodes.Ldc_I4_0)
                {
                    codes.RemoveAt(i + 1);
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.itemStack)),
                        CodeInstruction.LoadField(typeof(ItemStack), nameof(ItemStack.itemValue)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.GetActionIndexByMetaData))
                    });
                    i += 3;
                }
            }

            return codes;
        }
        #endregion

        #region Cancel reload on switching item
        //redirect these calls to action 0 and handle them in alternative module
        //may change in the future
        [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Update_PlayerMoveController(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_getgun = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetHoldingGun));
            MethodInfo mtd_getprimary = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetHoldingPrimary));
            FieldInfo fld_reload = AccessTools.Field(typeof(PlayerActionsPermanent), nameof(PlayerActionsPermanent.Reload));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_getgun))
                {
                    codes[i].operand = mtd_getprimary;
                    //codes.RemoveAt(i - 2);
                    //codes.InsertRange(i - 2, new[]
                    //{
                    //    new CodeInstruction(OpCodes.Ldarg_0),
                    //    CodeInstruction.LoadField(typeof(PlayerMoveController), "entityPlayerLocal"),
                    //    CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity))
                    //});
                    //i += 2;
                }
                else if (codes[i].LoadsField(fld_reload))
                {
                    var label = codes[i + 6].operand;
                    codes.InsertRange(i + 7, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(PlayerMoveController), nameof(PlayerMoveController.entityPlayerLocal)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                        CodeInstruction.Call(typeof(Inventory), nameof(Inventory.GetIsFinishedSwitchingHeldItem)),
                        new CodeInstruction(OpCodes.Brfalse, label)
                    });
                    i += 5;
                }
            }

            return codes;
        }
        #endregion

        #region Underwater check
        //skip underwater check if action is not current action
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.OnHoldingUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnHoldingUpdate_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var fld_ammonames = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.MagazineItemNames));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_ammonames) && (codes[i + 1].opcode == OpCodes.Brfalse_S || codes[i + 1].opcode == OpCodes.Brfalse))
                {
                    var jumpto = codes[i + 1].operand;
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i - 1].ExtractLabels()),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction)),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.invData)),
                        CodeInstruction.LoadField(typeof(ItemInventoryData), nameof(ItemInventoryData.holdingEntity)),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.GetActionIndexForEntity)),
                        new CodeInstruction(OpCodes.Bne_Un_S, jumpto)
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region GameEvent
        [HarmonyPatch(typeof(ActionUnloadItems), nameof(ActionUnloadItems.HandleItemStackChange))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_HandleItemStackChange_ActionUnloadItems(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var fld_actions = AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Actions));

            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_actions))
                {
                    var label = generator.DefineLabel();
                    codes[i - 1].WithLabels(label);
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldind_Ref),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ActionUnloadItems), nameof(ActionUnloadItems.ItemStacks)),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.MultiActionRemoveAmmoFromItemStack)),
                        new CodeInstruction(OpCodes.Brfalse_S, label),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Ret)
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region fast toolbelt item switching issue fix
        private static Coroutine switchHoldingItemCo;
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ShowHeldItem))]
        [HarmonyPrefix]
        private static bool Prefix_ShowHeldItem_Inventory(bool show, Inventory __instance)
        {
            //Log.Out($"ShowHeldItem {show} on entity {__instance.entity.entityName}\n{StackTraceUtility.ExtractStackTrace()}");
            if (show && __instance.entity is EntityPlayerLocal && switchHoldingItemCo != null)
            {
                GameManager.Instance.StopCoroutine(switchHoldingItemCo);
                switchHoldingItemCo = null;
                __instance.SetIsFinishedSwitchingHeldItem();
            }
            return true;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ShowHeldItem))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ShowHeldItem_Inventory(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Pop)
                {
                    var label = generator.DefineLabel();
                    codes[i].WithLabels(label);
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Brfalse_S, label),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(Inventory), nameof(Inventory.entity)),
                        new CodeInstruction(OpCodes.Isinst, typeof(EntityPlayerLocal)),
                        new CodeInstruction(OpCodes.Brfalse_S, label),
                        CodeInstruction.StoreField(typeof(MultiActionPatches), nameof(switchHoldingItemCo)),
                        new CodeInstruction(OpCodes.Ret)
                    });
                    break;
                }
            }
            return codes;
        }
        #endregion

        #region item info display fix
        [HarmonyPatch(typeof(XUiM_ItemStack), nameof(XUiM_ItemStack.GetStatItemValueTextWithModInfo))]
        [HarmonyPrefix]
        private static bool Prefix_GetStatItemValueTextWithModInfo_XUiM_ItemStack(ItemStack itemStack)
        {
            MultiActionUtils.SetCachedEventParamsDummyAction(itemStack);
            return true;
        }

        [HarmonyPatch(typeof(XUiM_ItemStack), nameof(XUiM_ItemStack.GetStatItemValueTextWithModColoring))]
        [HarmonyPrefix]
        private static bool Prefix_GetStatItemValueTextWithModColoring_XUiM_ItemStack(ItemStack itemStack)
        {
            MultiActionUtils.SetCachedEventParamsDummyAction(itemStack);
            return true;
        }

        [HarmonyPatch(typeof(XUiM_ItemStack), nameof(XUiM_ItemStack.GetStatItemValueTextWithCompareInfo))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetStatItemValueTextWithCompareInfo_XUiM_ItemStack(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_getvalue = AccessTools.Method(typeof(EffectManager), nameof(EffectManager.GetValue));
            var fld_seed = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.Seed));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_getvalue))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.SetCachedEventParamsDummyAction)),
                    });
                    for (int j = i; j >= 0; j--)
                    {
                        if (codes[j].StoresField(fld_seed))
                        {
                            codes.InsertRange(j + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_0),
                                CodeInstruction.Call(typeof(MultiActionUtils), nameof(MultiActionUtils.SetCachedEventParamsDummyAction))
                            });
                            codes.RemoveRange(j - 8, 9);
                            break;
                        }
                    }
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region ItemAction exclude tags
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        [HarmonyPrefix]
        private static bool Prefix_StartGame_GameManager()
        {
            MultiActionManager.PreloadCleanup();
            return true;
        }

        [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInit))]
        [HarmonyPostfix]
        private static void Postfix_LateInit_ItemClass(ItemClass __instance)
        {
            MultiActionManager.ParseItemActionExcludeTagsAndModifiers(__instance);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.GetValue))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_GetValue_EffectManager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].IsStarg(5))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i + 1].ExtractLabels()),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.CachedEventParam)),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData)),
                        new CodeInstruction(OpCodes.Ldarga_S, 5),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ModifyItemTags))
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(EffectManager), nameof(EffectManager.GetValuesAndSources))]
        [HarmonyPrefix]
        private static bool Prefix_GetValuesAndSources_EffectManager(ItemValue _originalItemValue, EntityAlive _entity, ref FastTags<TagGroup.Global> tags)
        {
            MultiActionManager.ModifyItemTags(_originalItemValue, _entity?.MinEventContext?.ItemActionData, ref tags);
            return true;
        }
        #endregion

        #region ItemAction exclude modifiers
        //see Transpiler_ModifyValue_ItemValue
        //see Transpiler_GetModifiedValueData_ItemValue
        //see MultiActionProjectileRewrites.ProjectileValueModifyValue
        //see MultiActionUtils.GetPropertyOverrideForAction
        //see MultiActionManager.ParseItemActionExcludeTagsAndModifiers
        #endregion

        #region requirement tags exclude
        [HarmonyPatch(typeof(TriggerHasTags), nameof(TriggerHasTags.IsValid))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_IsValid_TriggerHasTags(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_tags = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
            FieldInfo fld_tags = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.Tags));
            bool firstRet = true;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ret && firstRet)
                {
                    firstRet = false;
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldfld, fld_tags),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_tags),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.ItemValue)),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData)),
                        new CodeInstruction(OpCodes.Ldloca_S, lbd_tags),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ModifyItemTags))
                    });
                    i += 9;
                }
                else if (codes[i].LoadsField(fld_tags))
                {
                    codes[i].opcode = OpCodes.Ldloca_S;
                    codes[i].operand = lbd_tags;
                    codes[i].WithLabels(codes[i - 1].ExtractLabels());
                    codes.RemoveAt(i - 1);
                    i--;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ItemHasTags), nameof(ItemHasTags.IsValid))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_IsValid_ItemHasTags(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_tags = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
            FieldInfo fld_itemvalue = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.ItemValue));
            FieldInfo fld_hasalltags = AccessTools.Field(typeof(ItemHasTags), nameof(ItemHasTags.hasAllTags));
            MethodInfo prop_itemclass = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
            MethodInfo mtd_hasanytags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
            MethodInfo mtd_hasalltags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAllTags));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_hasalltags))
                {
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldfld, fld_itemvalue),
                        new CodeInstruction(OpCodes.Callvirt, prop_itemclass),
                        CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.ItemTags)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_tags),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldfld, fld_itemvalue),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData)),
                        new CodeInstruction(OpCodes.Ldloca_S, lbd_tags),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ModifyItemTags))
                    });
                    i += 11;
                }
                else if (codes[i].Calls(mtd_hasanytags))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
                    var labels = codes[i - 5].ExtractLabels();
                    codes.RemoveRange(i - 5, 3);
                    codes.Insert(i - 5, new CodeInstruction(OpCodes.Ldloca_S, lbd_tags).WithLabels(labels));
                    i -= 2;
                }
                else if (codes[i].Calls(mtd_hasalltags))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AllSet));
                    var labels = codes[i - 5].ExtractLabels();
                    codes.RemoveRange(i - 5, 3);
                    codes.Insert(i - 5, new CodeInstruction(OpCodes.Ldloca_S, lbd_tags).WithLabels(labels));
                    i -= 2;
                }
            }
            return codes;
        }


        [HarmonyPatch(typeof(HoldingItemHasTags), nameof(ItemHasTags.IsValid))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_IsValid_HoldingItemHasTags(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var lbd_tags = generator.DeclareLocal(typeof(FastTags<TagGroup.Global>));
            FieldInfo fld_itemvalue = AccessTools.Field(typeof(MinEventParams), nameof(MinEventParams.ItemValue));
            FieldInfo fld_hasalltags = AccessTools.Field(typeof(HoldingItemHasTags), nameof(HoldingItemHasTags.hasAllTags));
            MethodInfo prop_itemclass = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.ItemClass));
            MethodInfo prop_itemvalue = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemItemValue));
            MethodInfo mtd_hasanytags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAnyTags));
            MethodInfo mtd_hasalltags = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.HasAllTags));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].LoadsField(fld_hasalltags))
                {
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(HoldingItemHasTags), nameof(HoldingItemHasTags.target)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                        new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                        new CodeInstruction(OpCodes.Callvirt, prop_itemclass),
                        CodeInstruction.LoadField(typeof(ItemClass), nameof(ItemClass.ItemTags)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_tags),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(HoldingItemHasTags), nameof(HoldingItemHasTags.target)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.inventory)),
                        new CodeInstruction(OpCodes.Callvirt, prop_itemvalue),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(HoldingItemHasTags), nameof(HoldingItemHasTags.target)),
                        CodeInstruction.LoadField(typeof(EntityAlive), nameof(EntityAlive.MinEventContext)),
                        CodeInstruction.LoadField(typeof(MinEventParams), nameof(MinEventParams.ItemActionData)),
                        new CodeInstruction(OpCodes.Ldloca_S, lbd_tags),
                        CodeInstruction.Call(typeof(MultiActionManager), nameof(MultiActionManager.ModifyItemTags))
                    });
                    i += 17;
                }
                else if (codes[i].Calls(mtd_hasanytags))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AnySet));
                    var labels = codes[i - 6].ExtractLabels();
                    codes.RemoveRange(i - 6, 4);
                    codes.Insert(i - 6, new CodeInstruction(OpCodes.Ldloca_S, lbd_tags).WithLabels(labels));
                    i -= 3;
                }
                else if (codes[i].Calls(mtd_hasalltags))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(FastTags<TagGroup.Global>), nameof(FastTags<TagGroup.Global>.Test_AllSet));
                    var labels = codes[i - 6].ExtractLabels();
                    codes.RemoveRange(i - 6, 4);
                    codes.Insert(i - 6, new CodeInstruction(OpCodes.Ldloca_S, lbd_tags).WithLabels(labels));
                    i -= 3;
                }
            }
            return codes;
        }
        #endregion

        #region Inventory make ItemValue valid on creating inventory data
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetItem), new[] { typeof(int), typeof(ItemValue), typeof(int), typeof(bool) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_SetItem_Inventory(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_clone = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.Clone));
            var mtd_create = AccessTools.Method(typeof(Inventory), nameof(Inventory.createHeldItem));
            var mtd_invdata = AccessTools.Method(typeof(Inventory), nameof(Inventory.createInventoryData));
            for (int i = 0; i < codes.Count; i++)
            {
                //if (codes[i].opcode == OpCodes.Ldarg_3 && (codes[i + 1].opcode == OpCodes.Brtrue_S || codes[i + 1].opcode == OpCodes.Brtrue))
                //{
                //    var label = codes[i + 4].ExtractLabels();
                //    codes.InsertRange(i + 4, new[]
                //    {
                //        new CodeInstruction(OpCodes.Ldarg_2).WithLabels(label),
                //        new CodeInstruction(OpCodes.Callvirt, mtd_clone),
                //        new CodeInstruction(OpCodes.Starg_S, 2)
                //    });
                //    i += 7;
                //}
                //else 
                if (codes[i].Calls(mtd_create))
                {
                    codes.InsertRange(i - 12, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_2),
                        CodeInstruction.StoreField(typeof(ActionModuleAlternative), nameof(ActionModuleAlternative.InventorySetItemTemp))
                    });
                    i += 4;
                    for (int j = i; j < codes.Count; j++)
                    {
                        if (codes[j].Calls(mtd_invdata))
                        {
                            codes.InsertRange(j + 2, new[]
                            {
                                new CodeInstruction(OpCodes.Ldnull),
                                CodeInstruction.StoreField(typeof(ActionModuleAlternative), nameof(ActionModuleAlternative.InventorySetItemTemp))
                            });
                            i = j + 4;
                            break;
                        }
                    }
                    break;
                }
                //else if (codes[i].Calls(mtd_clone))
                //{
                //    codes.RemoveAt(i);
                //    break;
                //}
            }

            return codes;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ForceHoldingItemUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ForceHoldingItemUpdate_Inventory(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_invdata = AccessTools.Method(typeof(Inventory), nameof(Inventory.createInventoryData));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_invdata))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldnull),
                        CodeInstruction.StoreField(typeof(ActionModuleAlternative), nameof(ActionModuleAlternative.InventorySetItemTemp))
                    });
                    codes.InsertRange(i - 8, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0).WithLabels(codes[i - 8].ExtractLabels()),
                        CodeInstruction.StoreField(typeof(ActionModuleAlternative), nameof(ActionModuleAlternative.InventorySetItemTemp))
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region Temporaty fix for hud ammo mismatch
        [HarmonyPatch(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Update_XUiC_HUDStatBar(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mtd_getfocus = AccessTools.Method(typeof(Inventory), nameof(Inventory.GetFocusedItemIdx));
            MethodInfo mtd_getholding = AccessTools.PropertyGetter(typeof(Inventory), nameof(Inventory.holdingItemIdx));

            foreach (var ins in instructions)
            {
                if (ins.Calls(mtd_getfocus))
                {
                    ins.operand = mtd_getholding;
                }
                yield return ins;
            }
        }
        #endregion
    }

    //Moved to MultiActionFix
    //#region Ranged Reload
    //[HarmonyPatch]
    //public static class RangedReloadPatches
    //{
    //    private static IEnumerable<MethodBase> TargetMethods()
    //    {
    //        return new MethodInfo[]
    //        {
    //            AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.ReloadGun)),
    //            AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ReloadGun)),
    //            AccessTools.Method(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ReloadGun))
    //        };
    //    }

    //    //Why? Ask TFP why they don't just call base.ReloadGun()
    //    [HarmonyPrefix]
    //    private static bool Prefix_ReloadGun(ItemActionData _actionData)
    //    {
    //        int reloadAnimationIndex = MultiActionManager.GetMetaIndexForActionIndex(_actionData.invData.holdingEntity.entityId, _actionData.indexInEntityOfAction);
    //        _actionData.invData.holdingEntity.emodel?.avatarController?.UpdateInt(AvatarController.itemActionIndexHash, reloadAnimationIndex, false);
    //        _actionData.invData.holdingEntity.MinEventContext.ItemActionData = _actionData;
    //        return true;
    //    }
    //}
    //#endregion

    //KEEP
    #region Melee action tags
    [HarmonyPatch]
    public static class ActionTagPatches1
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(AnimatorMeleeAttackState), nameof(AnimatorMeleeAttackState.OnStateEnter), new[] {typeof(Animator), typeof(AnimatorStateInfo), typeof(int)}),
                AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageEntity)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetDamageEntity)),
                AccessTools.Method(typeof(ItemActionThrownWeapon), nameof(ItemActionThrownWeapon.GetDamageBlock)),
                AccessTools.Method(typeof(ItemActionThrownWeapon), nameof(ItemActionThrownWeapon.GetDamageEntity))
            };
        }

        //set correct tag for action index above 2
        //only action 1 uses secondary tag, others still use primary
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            FieldInfo fld_tag = AccessTools.Field(typeof(ItemActionAttack), nameof(ItemActionAttack.SecondaryTag));
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_tag))
                {
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Ceq)
                    });
                    i += 2;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch]
    public static class ActionTagPatches2
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.Raycast)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.GetExecuteActionGrazeTarget)),
                AccessTools.Method(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget)),
                AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.canStartAttack)),
                AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.OnHoldingUpdate)),
                AccessTools.Method(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.SetAttackFinished)),
                AccessTools.Method(typeof(ItemActionMelee), nameof(ItemActionMelee.OnHoldingUpdate)),
                AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction)),
                AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.fireShot)),
                AccessTools.Method(typeof(ItemActionThrownWeapon), nameof(ItemActionThrownWeapon.throwAway)),
                AccessTools.Method(typeof(ItemActionUseOther), nameof(ItemActionUseOther.ExecuteAction)),

            };
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            FieldInfo fld_index = AccessTools.Field(typeof(ItemActionData), nameof(ItemActionData.indexInEntityOfAction));
            for (int i = 0; i < codes.Count - 1; i++)
            {
                var code = codes[i];
                if (code.LoadsField(fld_index) && (codes[i + 1].opcode == OpCodes.Brfalse_S || codes[i + 1].opcode == OpCodes.Brfalse))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Ceq)
                    });
                    i += 2;
                }
            }

            return codes;
        }
    }
    #endregion

    //KEEP
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

    #region ItemAction property override
    [HarmonyPatch]
    public static class ItemActionPropertyOverridePatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                                          .SelectMany(a =>
                                          {
                                              try
                                              {
                                                  return a.GetTypes();
                                              }
                                              catch
                                              {
                                                  return new Type[0];
                                              }
                                          })
                                          .Where(t => t.IsSubclassOf(typeof(ItemAction)))
                                          .Select(t => AccessTools.Method(t, nameof(ItemAction.OnModificationsChanged)))
                                          .Where(m => m.IsDeclaredMember());
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            Log.Out($"Patching property override method {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}");
            var codes = instructions.ToList();
            var mtd_override = AccessTools.Method(typeof(ItemValue), nameof(ItemValue.GetPropertyOverride));
            var mtd_newoverride = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetPropertyOverrideForAction));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_override))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = mtd_newoverride;
                    codes.InsertRange(i, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.LoadField(typeof(ItemAction), nameof(ItemAction.ActionIndex))
                    });
                    i += 2;
                }
            }
            return codes;
        }
    }

    #endregion

    #region Remove ammo
    [HarmonyPatch]
    public static class RemoveAmmoPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new[]
            {
                AccessTools.Method(typeof(ItemActionEntryAssemble), nameof(ItemActionEntryAssemble.HandleRemoveAmmo)),
                AccessTools.Method(typeof(ItemActionEntryScrap), nameof(ItemActionEntryScrap.HandleRemoveAmmo)),
                AccessTools.Method(typeof(ItemActionEntrySell), nameof(ItemActionEntrySell.HandleRemoveAmmo)),
            };
        }

        [HarmonyPrefix]
        private static bool Prefix(BaseItemActionEntry __instance, ItemStack stack, ref ItemStack __result)
        {
            List<ItemStack> list_ammo_stack = new List<ItemStack>();
            if (!MultiActionUtils.MultiActionRemoveAmmoFromItemStack(stack, list_ammo_stack))
                return true;

            foreach (var ammoStack in list_ammo_stack)
            {
                if (!__instance.ItemController.xui.PlayerInventory.AddItem(ammoStack))
                {
                    __instance.ItemController.xui.PlayerInventory.DropItem(ammoStack);
                }
            }
            __result = stack;
            return false;
        }
    }
    #endregion

    #region Generate initial meta
    [HarmonyPatch]
    public static class InitialMetaPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodInfo[]
            {
                AccessTools.Method(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.SetupStartingItems)),
                AccessTools.Method(typeof(ItemClass), nameof(ItemClass.CreateItemStacks))
            };
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_initial = AccessTools.Method(typeof(ItemClass), nameof(ItemClass.GetInitialMetadata));
            var mtd_initialnew = AccessTools.Method(typeof(MultiActionUtils), nameof(MultiActionUtils.GetMultiActionInitialMetaData));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_initial))
                {
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = mtd_initialnew;
                    break;
                }
            }

            return codes;
        }
    }
    #endregion
}

using HarmonyLib;
using InControl;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

internal static class ReversePatches
{
    //[HarmonyPatch(typeof(XUiC_OptionsControls), nameof(XUiC_OptionsControls.createControlsEntries))]
    //[HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
    internal static void InitPlayerActionList(XUiC_OptionsControls __instance)
    {
        //IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    if (instructions == null)
        //    {
        //        yield break;
        //    }

        //    yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.InitCustomControls));

        //    foreach (var code in instructions)
        //    {
        //        if (code.opcode != OpCodes.Stloc_1)
        //        {
        //            yield return code;
        //        }
        //        else
        //        {
        //            yield return new CodeInstruction(OpCodes.Pop);
        //            yield return new CodeInstruction(OpCodes.Ldloc_0);
        //            yield return CodeInstruction.Call(typeof(CustomPlayerActionManager), nameof(CustomPlayerActionManager.ResizeGrid));
        //            yield return new CodeInstruction(OpCodes.Ret);
        //            break;
        //        }
        //    }
        //}
        //_ = Transpiler(null);
        SortedDictionary<PlayerActionData.ActionTab, SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>>> sortedDictionary = new SortedDictionary<PlayerActionData.ActionTab, SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>>>();
        PlayerActionsBase[] array = CustomPlayerActionManager.CreateActionArray(new PlayerActionsBase[5]
        {
            __instance.xui.playerUI.playerInput,
            __instance.xui.playerUI.playerInput.VehicleActions,
            __instance.xui.playerUI.playerInput.PermanentActions,
            __instance.xui.playerUI.playerInput.GUIActions,
            PlayerActionsGlobal.Instance
        });
        for (int i = 0; i < array.Length; i++)
        {
            foreach (PlayerAction action in array[i].Actions)
            {
                if (!(action.UserData is PlayerActionData.ActionUserData actionUserData))
                {
                    continue;
                }

                switch (actionUserData.appliesToInputType)
                {
                    default:
                        throw new ArgumentOutOfRangeException();
                    case PlayerActionData.EAppliesToInputType.KbdMouseOnly:
                    case PlayerActionData.EAppliesToInputType.Both:
                        if (!actionUserData.doNotDisplay)
                        {
                            SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>> sortedDictionary2;
                            if (sortedDictionary.ContainsKey(actionUserData.actionGroup.actionTab))
                            {
                                sortedDictionary2 = sortedDictionary[actionUserData.actionGroup.actionTab];
                            }
                            else
                            {
                                sortedDictionary2 = new SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>>();
                                sortedDictionary.Add(actionUserData.actionGroup.actionTab, sortedDictionary2);
                            }

                            List<PlayerAction> list;
                            if (sortedDictionary2.ContainsKey(actionUserData.actionGroup))
                            {
                                list = sortedDictionary2[actionUserData.actionGroup];
                            }
                            else
                            {
                                list = new List<PlayerAction>();
                                sortedDictionary2.Add(actionUserData.actionGroup, list);
                            }

                            list.Add(action);
                        }

                        break;
                    case PlayerActionData.EAppliesToInputType.None:
                    case PlayerActionData.EAppliesToInputType.ControllerOnly:
                        break;
                }
            }
        }

        CustomPlayerActionManager.ResizeGrid(sortedDictionary);
    }

    internal static void InitControllerActionList(XUiC_OptionsController __instance)
    {
        PlayerActionsBase[] array = new PlayerActionsBase[]
        {
            __instance.xui.playerUI.playerInput,
            __instance.xui.playerUI.playerInput.VehicleActions
        };
        Dictionary<string, List<PlayerAction>> dictionary = new Dictionary<string, List<PlayerAction>>();
        dictionary.Add("inpTabPlayerOnFoot", new List<PlayerAction>());
        PlayerActionsBase[] array2 = array;
        for (int i = 0; i < array2.Length; i++)
        {
            foreach (PlayerAction playerAction in array2[i].ControllerRebindableActions)
            {
                PlayerActionData.ActionUserData actionUserData = playerAction.UserData as PlayerActionData.ActionUserData;
                if (actionUserData != null)
                {
                    if (actionUserData.actionGroup.actionTab.tabNameKey == "inpTabPlayerControl" || actionUserData.actionGroup.actionTab.tabNameKey == "inpTabToolbelt")
                    {
                        dictionary["inpTabPlayerOnFoot"].Add(playerAction);
                    }
                    else if (dictionary.ContainsKey(actionUserData.actionGroup.actionTab.tabNameKey))
                    {
                        dictionary[actionUserData.actionGroup.actionTab.tabNameKey].Add(playerAction);
                    }
                    else
                    {
                        dictionary.Add(actionUserData.actionGroup.actionTab.tabNameKey, new List<PlayerAction>());
                        dictionary[actionUserData.actionGroup.actionTab.tabNameKey].Add(playerAction);
                    }
                }
            }
        }
        dictionary["inpTabPlayerOnFoot"].Add(__instance.xui.playerUI.playerInput.PermanentActions.PushToTalk);
        CustomPlayerActionManager.CreateControllerActions(dictionary);
        CustomPlayerActionManager.ResizeControllerGrid(dictionary);
    }
}
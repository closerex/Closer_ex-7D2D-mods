using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom)), TypeDataTarget(typeof(VariableZoomData))]
public class ActionModuleVariableZoom
{
    private const string METASAVENAME = "CurZoomStep";
    public static float zoomScale = 7.5f;
    [HarmonyPatch(nameof(ItemAction.ConsumeScrollWheel)), MethodTargetPostfix]
    private void Postfix_ConsumeScrollWheel(ItemActionData _actionData, float _scrollWheelInput, PlayerActionsLocal _playerInput, VariableZoomData __customData)
    {
        if (!_actionData.invData.holdingEntity.AimingGun || _scrollWheelInput == 0f)
        {
            return;
        }

        ItemActionZoom.ItemActionDataZoom itemActionDataZoom = (ItemActionZoom.ItemActionDataZoom)_actionData;
        if (!itemActionDataZoom.bZoomInProgress && !__customData.isToggleOnly)
        {
            __customData.curStep = Utils.FastClamp01(__customData.curStep + _scrollWheelInput);
            __customData.stepSign = Mathf.Sign(_scrollWheelInput);
            __customData.UpdateByStep();
            ItemValue scopeValue = __customData.ScopeValue;
            if (scopeValue != null)
            {
                scopeValue.SetMetadata(METASAVENAME, __customData.SignedStep, TypedMetadataValue.TypeTag.Float);
                _actionData.invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
            }
        }
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationChanged(ItemActionZoom __instance, ItemActionData _data, VariableZoomData __customData)
    {
        string str = __instance.Properties.GetString("ZoomRatio");
        if (string.IsNullOrEmpty(str))
        {
            str = "1";
        }
        __customData.maxScale = StringParsers.ParseFloat(_data.invData.itemValue.GetPropertyOverride("ZoomRatio", str));

        str = __instance.Properties.GetString("ZoomRatioMin");
        if (string.IsNullOrEmpty(str))
        {
            str = __customData.maxScale.ToString();
        }
        __customData.minScale = StringParsers.ParseFloat(_data.invData.itemValue.GetPropertyOverride("ZoomRatioMin", str));

        str = _data.invData.itemValue.GetPropertyOverride("ToggleOnly", null);
        if (!string.IsNullOrEmpty(str) && bool.TryParse(str, out __customData.isToggleOnly)) ;

        str = _data.invData.itemValue.GetPropertyOverride("ForceFovRange", null);
        if (!string.IsNullOrEmpty(str) && StringParsers.TryParseRange(str, out __customData.fovRange) && __customData.fovRange.min > 0 && __customData.fovRange.max > 0)
        {
            __customData.fovRange = new FloatRange(Mathf.Min(__customData.fovRange.max, __customData.fovRange.min), Mathf.Max(__customData.fovRange.max, __customData.fovRange.min));
            __customData.forceFov = true;
        }

        __customData.scopeValueIndex = _data.invData.itemValue.Modifications == null ? -1 : Array.FindIndex(_data.invData.itemValue.Modifications, static v => v?.ItemClass is IModuleContainerFor<ItemModuleVariableZoom>);
        if (__customData.scopeValueIndex == -1 && _data.invData.itemValue.ItemClass is not IModuleContainerFor<ItemModuleVariableZoom>)
        {
            __customData.scopeValueIndex = int.MinValue;
        }
        ItemValue scopeValue = __customData.ScopeValue;
        if (scopeValue != null)
        {
            if (scopeValue.GetMetadata(METASAVENAME) is float curStep)
            {
                __customData.curStep = Mathf.Abs(curStep);
                __customData.stepSign = Mathf.Sign(curStep);
            }
            __customData.curStep = Utils.FastClamp01(__customData.curStep);
            scopeValue.SetMetadata(METASAVENAME, __customData.SignedStep, TypedMetadataValue.TypeTag.Float);
            _data.invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
        }
        else
        {
            __customData.curStep = Utils.FastClamp01(__customData.curStep);
        }
        __customData.UpdateByStep();
    }

    public class VariableZoomData
    {
        public float maxScale = 1f;
        public float minScale = 1f;
        public float curScale = 0f;
        public bool forceFov = false;
        public FloatRange fovRange = new FloatRange(15f, 15f);
        public float curStep = 0;
        public float stepSign = 1f;
        public bool isToggleOnly = false;
        public bool shouldUpdate = true;
        public int scopeValueIndex = int.MinValue;

        public float SignedStep => curStep * stepSign;
        public ItemValue ScopeValue
        {
            get
            {
                if (invData == null)
                {
                    return null;
                }
                if (scopeValueIndex == -1)
                {
                    return invData.itemValue;
                }
                else if (scopeValueIndex >= 0 && scopeValueIndex < invData.itemValue.Modifications.Length)
                {
                    return invData.itemValue.Modifications[scopeValueIndex];
                }
                return null;
            }
        }
        public ItemInventoryData invData = null;

        public VariableZoomData(ItemInventoryData _inventoryData)
        {
            invData = _inventoryData;
        }

        public void ToggleZoom()
        {
            if (stepSign > 0)
            {
                if (this.curStep >= 1)
                {
                    this.curStep = 0;
                }
                else
                {
                    this.curStep = 1;
                }
            }
            else
            {
                if (this.curStep <= 0)
                {
                    this.curStep = 1;
                }
                else
                {
                    this.curStep = 0;
                }
            }
            UpdateByStep();
            ItemValue scopeValue = ScopeValue;
            if (scopeValue != null)
            {
                scopeValue.SetMetadata(METASAVENAME, SignedStep, TypedMetadataValue.TypeTag.Float);
                invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
            }
        }

        public void UpdateByStep()
        {
            curScale = Utils.FastLerp(minScale, maxScale, curStep);
            shouldUpdate = true;
        }
    }
}

[HarmonyPatch]
public static class VariableZoomPatches
{
    [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
    [HarmonyPrefix]
    private static bool Prefix_Update_PlayerMoveController(PlayerMoveController __instance)
    {
        if (DroneManager.Debug_LocalControl || !__instance.gameManager.gameStateManager.IsGameStarted() || GameStats.GetInt(EnumGameStats.GameState) != 1)
            return true;

        bool isUIOpen = __instance.windowManager.IsCursorWindowOpen() || __instance.windowManager.IsInputActive() || __instance.windowManager.IsModalWindowOpen();

        UpdateLocalInput(__instance.entityPlayerLocal, isUIOpen);

        return true;
    }

    private static void UpdateLocalInput(EntityPlayerLocal _player, bool _isUIOpen)
    {
        if (_isUIOpen || _player.emodel.IsRagdollActive || _player.IsDead() || _player.AttachedToEntity != null)
        {
            return;
        }

        if (PlayerActionKFLib.Instance.Enabled && PlayerActionKFLib.Instance.ToggleZoom.WasPressed)
        {
            var actionData = _player.inventory.holdingItemData.actionData[1];
            if (actionData is IModuleContainerFor<ActionModuleVariableZoom.VariableZoomData> variableZoomData)
            {
                variableZoomData.Instance.ToggleZoom();
            }
        }
    }

    //[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetItem), new Type[] { typeof(int), typeof(ItemValue), typeof(int), typeof(bool) })]
    //[HarmonyTranspiler]
    //private static IEnumerable<CodeInstruction> Transpiler_Test(IEnumerable<CodeInstruction> instructions)
    //{
    //    var codes = instructions.ToList();
    //    var fld = AccessTools.Field(typeof(ItemStack), nameof(ItemStack.count));

    //    for (int i = 0; i < codes.Count; i++)
    //    {
    //        if (codes[i].StoresField(fld))
    //        {
    //            codes.InsertRange(i + 1, new[]
    //            {
    //                new CodeInstruction(OpCodes.Ldloc_0),
    //                new CodeInstruction(OpCodes.Ldarg_0),
    //                new CodeInstruction(OpCodes.Ldarg_1),
    //                CodeInstruction.Call(typeof(VariableZoomPatches), nameof(LogMsg))
    //            });
    //            break;
    //        }
    //    }
    //    return codes;
    //}

    //private static void LogMsg(bool flag, Inventory inv, int idx)
    //{
    //    if (inv.holdingItemIdx == idx)
    //        Log.Out($"changed: {flag}\n{StackTraceUtility.ExtractStackTrace()}");
    //}
}
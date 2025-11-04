using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(FireModeData))]
public class ActionModuleFireModeSelector
{
    public struct FireMode
    {
        public byte burstCount;
        public bool isFullAuto;
    }
    public string fireModeSwitchingSound = null;
    private List<FireMode> modeCache = new List<FireMode>();
    private List<string> nameCache = new List<string>();
    public static string[] FireModeNames = new[]
    {
        "FireMode",
        "FireMode1",
        "FireMode2",
        "FireMode3",
        "FireMode4",
    };
    public static int[] FireModeParamHashes = new[]
    {
        Animator.StringToHash("FireMode"),
        Animator.StringToHash("FireMode1"),
        Animator.StringToHash("FireMode2"),
        Animator.StringToHash("FireMode3"),
        Animator.StringToHash("FireMode4"),
    };
    public static int[] FireModeSwitchParamHashes = new[]
    {
        Animator.StringToHash("FireModeChanged"),
        Animator.StringToHash("FireModeChanged1"),
        Animator.StringToHash("FireModeChanged2"),
        Animator.StringToHash("FireModeChanged3"),
        Animator.StringToHash("FireModeChanged4"),
    };

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationChanged(ItemActionData _data, FireModeData __customData, ItemActionRanged __instance)
    {
        __instance.Properties.ParseString("FireModeSwitchingSound", ref fireModeSwitchingSound);
        int actionIndex = _data.indexInEntityOfAction;
        for (int i = 0; i < 99; i++)
        {
            if (!__instance.Properties.Contains($"FireMode{i}.BurstCount"))
            {
                break;
            }
            string burstCount = 1.ToString();
            __instance.Properties.ParseString($"FireMode{i}.BurstCount", ref burstCount);
            string isFullAuto = false.ToString();
            __instance.Properties.ParseString($"FireMode{i}.IsFullAuto", ref isFullAuto);
            string modeName = null;
            __instance.Properties.ParseString($"FireMode{i}.ModeName", ref modeName);
            modeCache.Add(new FireMode
            {
                burstCount = byte.Parse(_data.invData.itemValue.GetPropertyOverrideForAction($"FireMode{i}.BurstCount", burstCount, actionIndex)),
                isFullAuto = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction($"FireMode{i}.IsFullAuto", isFullAuto, actionIndex))
            });
            nameCache.Add(_data.invData.itemValue.GetPropertyOverrideForAction($"FireMode{i}.ModeName", modeName, actionIndex));
        }
        foreach (var modePlus in _data.invData.itemValue.GetAllPropertyOverridesForAction("FireModePlus", actionIndex))
        {
            JObject jsonData = (JObject)JToken.Parse(modePlus);
            foreach (var modeProp in jsonData.Properties())
            {
                JObject modeValue = (JObject)modeProp.Value;
                modeCache.Add(new FireMode
                {
                    burstCount = (byte)modeValue.GetValue("BurstCount"),
                    isFullAuto = (bool)modeValue.GetValue("IsFullAuto")
                });
                nameCache.Add(modeProp.Name);
            }
        }
        //for (int i = 0; i < 99; i++)
        //{
        //    string burstCount = _data.invData.itemValue.GetPropertyOverrideForAction($"FireModePlus{i}.BurstCount", null, actionIndex);
        //    if (burstCount == null)
        //    {
        //        break;
        //    }
        //    modeCache.Add(new FireMode
        //    {
        //        burstCount = byte.Parse(_data.invData.itemValue.GetPropertyOverrideForAction($"FireModePlus{i}.BurstCount", burstCount, actionIndex)),
        //        isFullAuto = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction($"FireModePlus{i}.IsFullAuto", "false", actionIndex))
        //    });
        //    nameCache.Add(_data.invData.itemValue.GetPropertyOverrideForAction($"FireModePlus{i}.ModeName", null, actionIndex));
        //}
        __customData.fireModes = modeCache.ToArray();
        modeCache.Clear();
        __customData.modeNames = nameCache.ToArray();
        nameCache.Clear();
        if (_data.invData.itemValue.GetMetadata(FireModeNames[actionIndex]) is int mode)
        {
            __customData.currentFireMode = (byte)mode;
        }
        if (__customData.currentFireMode < 0 || __customData.currentFireMode >= __customData.fireModes.Length)
        {
            __customData.currentFireMode = 0;
        }
        if (__customData.delayFiringCo != null)
        {
            ThreadManager.StopCoroutine(__customData.delayFiringCo);
            __customData.delayFiringCo = null;
        }
        __customData.isRequestedByCoroutine = false;
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    private void Postfix_StartHolding(ItemActionData _data, FireModeData __customData)
    {
        __customData.SetFireMode(_data, __customData.currentFireMode);
        __customData.inputReleased = true;
    }

    [HarmonyPatch(nameof(ItemAction.OnHoldingUpdate)), MethodTargetPostfix]
    private static void Postfix_OnHoldingUpdate(ItemActionData _actionData, FireModeData __customData)
    {
        __customData.UpdateDelay(_actionData);
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    private static void Postfix_StopHolding(FireModeData __customData)
    {
        if (__customData.delayFiringCo != null)
        {
            ThreadManager.StopCoroutine(__customData.delayFiringCo);
            __customData.delayFiringCo = null;
        }
        __customData.isRequestedByCoroutine = false;
        __customData.inputReleased = true;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    private bool Prefix_ExecuteAction(ItemActionData _actionData, ItemActionRanged __instance, FireModeData __customData, bool _bReleased)
    {
        if (__customData.isRequestedByCoroutine)
        {
            return true;
        }
        __customData.inputReleased = _bReleased;
        if (__customData.delayFiringCo == null)
        {
            if (_bReleased || _actionData.invData.itemValue.Meta == 0 || _actionData.invData.itemValue.PercentUsesLeft <= 0)
            {
                return true;
            }
            FireMode curFireMode = __customData.fireModes[__customData.currentFireMode];
            if (curFireMode.burstCount == 1)
            {
                return true;
            }
            var rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
            if (__instance.GetBurstCount(_actionData) > rangedData.curBurstCount)
            {
                __customData.StartFiring(__instance, _actionData);
            }
        }
        return false;
    }

    [HarmonyPatch(nameof(ItemActionRanged.GetBurstCount)), MethodTargetPostfix]
    private void Postfix_GetBurstCount(FireModeData __customData, ref int __result)
    {
        FireMode fireMode = __customData.fireModes[__customData.currentFireMode];
        __result = fireMode.isFullAuto ? 999 : fireMode.burstCount;
    }

    [HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning(FireModeData __customData, ref bool __result)
    {
        __result |= __customData.delayFiringCo != null;
    }

    public class FireModeData
    {
        public string switchSound;
        public FireMode[] fireModes;
        public string[] modeNames;
        public byte currentFireMode;
        public Coroutine delayFiringCo;
        public bool isRequestedByCoroutine;
        public float shotDelay;
        public float burstDelay;
        public bool inputReleased;

        public void CycleFireMode(ItemActionData _data)
        {
            SetFireMode(_data, (byte)((currentFireMode + 1) % fireModes.Length));
        }

        public void SetFireMode(ItemActionData _data, byte _fireMode)
        {
            if (currentFireMode != _fireMode)
            {
                currentFireMode = _fireMode;
                FireMode curFireMode = fireModes[currentFireMode];
                if (!string.IsNullOrEmpty(switchSound))
                {
                    _data.invData.holdingEntity.PlayOneShot(switchSound);
                }
                _data.invData.holdingEntity.emodel.avatarController.TriggerEvent(FireModeSwitchParamHashes[_data.indexInEntityOfAction]);
            }
            if (!string.IsNullOrEmpty(modeNames[_fireMode]))
            {
                GameManager.ShowTooltip(_data.invData.holdingEntity as EntityPlayerLocal, modeNames[_fireMode], true);
            }
            else
            {
                GameManager.ShowTooltip(_data.invData.holdingEntity as EntityPlayerLocal, "ttCurrentFiringMode", _fireMode.ToString(), null, null, true);
            }
            //GameManager.ShowTooltip(_data.invData.holdingEntity as EntityPlayerLocal, "ttCurrentFiringMode", string.IsNullOrEmpty(modeNames[_fireMode]) ? _fireMode.ToString() : Localization.Get(modeNames[_fireMode]), null, null, true);
            _data.invData.holdingEntity.MinEventContext.ItemActionData = _data;
            _data.invData.holdingEntity.FireEvent(CustomEnums.onSelfBurstModeChanged);
            UpdateDelay(_data);

            ItemValue itemValue = _data.invData.itemValue;
            if (itemValue != null)
            {
                if (itemValue.Metadata == null)
                {
                    itemValue.Metadata = new Dictionary<string, TypedMetadataValue>();
                }

                if (!itemValue.Metadata.TryGetValue(ActionModuleFireModeSelector.FireModeNames[_data.indexInEntityOfAction], out var metadata) || !metadata.SetValue((int)_fireMode))
                {
                    itemValue.Metadata[ActionModuleFireModeSelector.FireModeNames[_data.indexInEntityOfAction]] = new TypedMetadataValue((int)_fireMode, TypedMetadataValue.TypeTag.Integer);
                }
                _data.invData.holdingEntity.inventory.CallOnToolbeltChangedInternal();
            }
        }

        public void UpdateDelay(ItemActionData _data)
        {
            FireMode curFireMode = fireModes[currentFireMode];
            if (curFireMode.burstCount == 1)
            {
                return;
            }

            var rangedData = _data as ItemActionRanged.ItemActionDataRanged;
            var rangedAction = _data.invData.item.Actions[_data.indexInEntityOfAction] as ItemActionRanged;
            if (rangedAction.rapidTrigger)
            {
                shotDelay = 0;
                burstDelay = rangedData.Delay;
            }
            else
            {
                float rpm = 1f;
                float perc = 1f;
                var tags = _data.invData.item.ItemTags;
                MultiActionManager.ModifyItemTags(_data.invData.itemValue, _data, ref tags);
                _data.invData.item.Effects.ModifyValue(_data.invData.holdingEntity, PassiveEffects.RoundsPerMinute, ref rpm, ref perc, _data.invData.itemValue.Quality, tags);
                float burstInterval = EffectManager.GetValue(CustomEnums.BurstShotInterval, _data.invData.itemValue, -1, _data.invData.holdingEntity);
                burstInterval *= 60f / (rpm * perc * rangedData.Delay);
                if (burstInterval >= 0 && rangedData.Delay > burstInterval)
                {
                    shotDelay = burstInterval;
                    burstDelay = (rangedData.Delay - burstInterval) * curFireMode.burstCount;
                }
                else
                {
                    shotDelay = rangedData.Delay;
                    burstDelay = 0;
                }
            }
        }

        public void StartFiring(ItemActionRanged _instance, ItemActionData _data)
        {
            UpdateDelay(_data);
            if (delayFiringCo != null)
            {
                ThreadManager.StopCoroutine(delayFiringCo);
            }
            ((ItemActionRanged.ItemActionDataRanged)_data).bPressed = true;
            ((ItemActionRanged.ItemActionDataRanged)_data).bReleased = false;

            delayFiringCo = ThreadManager.StartCoroutine(DelayFiring(_instance, _data));
        }

        private IEnumerator DelayFiring(ItemActionRanged _instance, ItemActionData _data)
        {
            FireMode curFireMode = fireModes[currentFireMode];
            var rangedData = _data as ItemActionRanged.ItemActionDataRanged;
            //byte curBurstCount = rangedData.curBurstCount;
            for (int i = 0; i < curFireMode.burstCount; i++)
            {
                isRequestedByCoroutine = true;
                rangedData.bPressed = true;
                rangedData.bReleased = false;
                rangedData.m_LastShotTime = 0;
                rangedData.state = ItemActionFiringState.Off;
                _instance.ExecuteAction(_data, false);
                //rangedData.curBurstCount = (byte)(curBurstCount + i + 1);
                isRequestedByCoroutine = false;
                if (rangedData.invData.itemValue.Meta <= 0 && !_instance.HasInfiniteAmmo(_data))
                {
                    goto cleanup;
                }
                if (shotDelay > 0)
                {
                    yield return new WaitForSeconds(shotDelay);
                }
            }
            if (burstDelay > 0)
            {
                yield return new WaitForSeconds(burstDelay);
            }

            cleanup:
            delayFiringCo = null;
            if (inputReleased)
            {
                _instance.ExecuteAction(_data, true);
            }
        }
    }
}

[HarmonyPatch]
public static class FireModePatches
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

        if (PlayerActionKFLib.Instance.Enabled && PlayerActionKFLib.Instance.ToggleFireMode.WasPressed)
        {
            if (_player.inventory.IsHoldingItemActionRunning())
            {
                return;
            }

            var actionData = _player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(_player)];
            if (actionData is IModuleContainerFor<ActionModuleFireModeSelector.FireModeData> fireModeData)
            {
                fireModeData.Instance.CycleFireMode(actionData);
            }
        }
    }
}
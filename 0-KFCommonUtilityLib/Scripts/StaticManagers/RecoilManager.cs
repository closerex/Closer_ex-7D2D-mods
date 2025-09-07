using CameraShake;
using GearsAPI.Settings.Global;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public static class RecoilManager
    {
        private enum RecoilState
        {
            None,
            Recoil,
            Return
        }

        private static RecoilState state;
        private static float lastKickTime = 0f;
        //private static float recoilScaledDelta = 0f;
        //private static float returnScaledDelta = 0f;
        private static Vector2 targetRotationXY = Vector2.zero;
        private static Vector2 targetReturnXY = Vector2.zero;
        private static Vector3 returnSpeedCur = Vector3.zero;
        private static Vector2 totalRotationXY = Vector2.zero;
        private static Vector3 totalReturnCur = Vector3.zero;
        private static EntityPlayerLocal player;
        //Gears options
        private static bool enableCap = false;
        private static bool enableDynamicCap = false;
        private static bool enableSoftCap = false;
        private static bool enablePreRecoilCompensation = false;
        private static float maxRecoilAngle = 15;
        private static int maxDynamicRecoilCapShots = 6;
        private static float recoilCapRemain = 1f;
        private static float recoilCompensationSensitivityMultiplier = 0f;
        private const float DEFAULT_SNAPPINESS_PISTOL = 6f;
        private const float DEFAULT_SNAPPINESS_RIFLE = 3.6f;
        private const float DEFAULT_SNAPPINESS_SHOTGUN = 6f;
        private const float DEFAULT_RETURN_SPEED_PISTOL = 8f;
        private const float DEFAULT_RETURN_SPEED_RIFLE = 4f;
        private const float DEFAULT_RETURN_SPEED_SHOTGUN = 4f;
        private static readonly FastTags<TagGroup.Global> PistolTag = FastTags<TagGroup.Global>.Parse("pistol");
        private static readonly FastTags<TagGroup.Global> ShotgunTag = FastTags<TagGroup.Global>.Parse("shotgun");

        private static void ClearData()
        {
            state = RecoilState.None;
            //recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            targetRotationXY = Vector2.zero;
            targetReturnXY = Vector2.zero;
            totalRotationXY = Vector2.zero;
            totalReturnCur = Vector3.zero;
            lastKickTime = 0f;
        }

        public static void InitRecoilSettings(IModGlobalSettings settings)
        {
            var capSetting = settings.GetTab("RecoilSettings").GetCategory("Capping");

            var recoilCompensationSetting = capSetting.GetSetting("RecoilCompensationSensitivityMultiplier") as ISliderGlobalSetting;
            recoilCompensationSensitivityMultiplier = float.Parse(recoilCompensationSetting.CurrentValue);
            recoilCompensationSetting.OnSettingChanged += static (setting, newValue) => recoilCompensationSensitivityMultiplier = float.Parse(newValue);

            var preRecoilCompensationSetting = capSetting.GetSetting("EnablePreRecoilCompensation") as ISwitchGlobalSetting;
            enablePreRecoilCompensation = preRecoilCompensationSetting.CurrentValue == "Enable";
            preRecoilCompensationSetting.OnSettingChanged += static (setting, newValue) => enablePreRecoilCompensation = newValue == "Enable";

            var enableCapSetting = capSetting.GetSetting("EnableCap") as ISwitchGlobalSetting;
            enableCap = enableCapSetting.CurrentValue == "Enable";
            enableCapSetting.OnSettingChanged += static (setting, newValue) =>
            {
                enableCap = newValue == "Enable";
                UpdateSettingState(setting.Category);
            };

            var recoilRemainSetting = capSetting.GetSetting("RecoilRemain") as ISliderGlobalSetting;
            recoilCapRemain = float.Parse(recoilRemainSetting.CurrentValue);
            recoilRemainSetting.OnSettingChanged += static (setting, newValue) => recoilCapRemain = float.Parse(newValue);

            var enableSoftCapSetting = capSetting.GetSetting("EnableSoftCap") as ISwitchGlobalSetting;
            enableSoftCap = enableSoftCapSetting.CurrentValue == "Enable";
            enableSoftCapSetting.OnSettingChanged += static (setting, newValue) => enableSoftCap = newValue == "Enable";

            var maxRecoilAngleSetting = capSetting.GetSetting("MaxRecoilAngle") as ISliderGlobalSetting;
            maxRecoilAngle = float.Parse(maxRecoilAngleSetting.CurrentValue);
            maxRecoilAngleSetting.OnSettingChanged += static (setting, newValue) => maxRecoilAngle = float.Parse(newValue);

            var enableDynamicCapSetting = capSetting.GetSetting("EnableDynamicCap") as ISwitchGlobalSetting;
            enableDynamicCap = enableDynamicCapSetting.CurrentValue == "Enable";
            enableDynamicCapSetting.OnSettingChanged += static (setting, newValue) =>
            {
                enableDynamicCap = newValue == "Enable";
                UpdateSettingState(setting.Category);
            };

            var maxDynamicRecoilCapShotsSetting = capSetting.GetSetting("MaxDynamicRecoilCapShots") as ISliderGlobalSetting;
            maxDynamicRecoilCapShots = int.Parse(maxDynamicRecoilCapShotsSetting.CurrentValue);
            maxDynamicRecoilCapShotsSetting.OnSettingChanged += static (setting, newValue) => maxDynamicRecoilCapShots = int.Parse(newValue);
            UpdateSettingState(capSetting);
        }

        private static void UpdateSettingState(IGlobalModSettingsCategory category)
        {
            category.GetSetting("EnableCap").Enabled = true;
            category.GetSetting("RecoilRemain").Enabled = enableCap;
            category.GetSetting("EnableSoftCap").Enabled = enableCap;
            category.GetSetting("MaxRecoilAngle").Enabled = enableCap && !enableDynamicCap;
            category.GetSetting("EnableDynamicCap").Enabled = enableCap;
            category.GetSetting("MaxDynamicRecoilCapShots").Enabled = enableCap && enableDynamicCap;
        }

        public static void InitPlayer(EntityPlayerLocal _player)
        {
            ClearData();
            player = _player;
            player.cameraTransform.AddMissingComponent<CameraShaker>();
        }

        public static void Cleanup()
        {
            ClearData();
            player = null;
        }

        private static float shakeFreq = 20;
        private static int shakeBounce = 5;
        public static void AddRecoil(Vector2 recoilRangeHor, Vector2 recoilRangeVer)
        {
            if (player == null) { return; }
            if (player.inventory?.holdingItemData?.actionData?[MultiActionManager.GetActionIndexForEntity(player)] is IModuleContainerFor<ActionModuleProceduralRecoil.EFTProceduralRecoilData>)
            {
                return;
            }
            state = RecoilState.Recoil;
            //recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            //returnScaledDelta = 0;
            float cap = 0f;
            if (enableCap)
            {
                if (enableDynamicCap)
                {
                    cap = Mathf.Abs(recoilRangeVer.y) * maxDynamicRecoilCapShots;
                }
                else
                {
                    cap = maxRecoilAngle;
                }
            }
            float cameraShakeStrength = EffectManager.GetValue(CustomEnums.RecoilCameraShakeStrength, player.inventory.holdingItemItemValue, 0.12f, player);
            float targetRotationX = player.rand.RandomRange(recoilRangeVer.x, recoilRangeVer.y);
            float targetRotationY = player.rand.RandomRange(recoilRangeHor.x, recoilRangeHor.y);
            if (enableCap)
            {
                if (Mathf.Abs(totalRotationXY.x) >= Mathf.Abs(cap))
                {
                    targetRotationX *= recoilCapRemain;
                }
                else if (enableSoftCap)
                {
                    targetRotationX *= Mathf.Lerp(recoilCapRemain, 1f, 1 - Mathf.InverseLerp(0, Mathf.Abs(cap), Mathf.Abs(totalRotationXY.x)));
                    //targetRotationX *= Mathf.Lerp(recoilCapRemain, 1f, Mathf.Cos(Mathf.PI * .5f * Mathf.InverseLerp(0, Mathf.Abs(cap), Mathf.Abs(totalRotationXY.x))));
                }
            }
            if (!player.AimingGun)
            {
                targetRotationXY += new Vector2(targetRotationX, targetRotationY) * 2f;
                totalRotationXY += new Vector2(targetRotationX, targetRotationY) * 2f;
                CameraShaker.Presets.ShortShake3D(cameraShakeStrength * 1.2f, shakeFreq, shakeBounce);
            }
            else
            {
                targetRotationXY += new Vector2(targetRotationX, targetRotationY);
                totalRotationXY += new Vector2(targetRotationX, targetRotationY);
                CameraShaker.Presets.ShortShake3D(cameraShakeStrength, shakeFreq, shakeBounce);
            }
            lastKickTime = Time.time;
            //if (enableCap)
            //{
            //    float totalRotationXCapped = Mathf.Clamp(totalRotationXY.x, -cap, cap);
            //    targetRotationXY.x = Mathf.Clamp(targetRotationXY.x + totalRotationXCapped - totalRotationXY.x, -cap, cap);
            //    totalRotationXY.x = totalRotationXCapped;
            //}
        }

        public static float CompensateX(float movedX)
        {
            if (!enablePreRecoilCompensation)
            {
                if (targetReturnXY.x * movedX < 0)
                    targetReturnXY.x = Mathf.Max(0, targetReturnXY.x + movedX);
                return movedX;
            }
            float targetX = targetRotationXY.x;
            float returnX = targetReturnXY.x;
            float res = Compensate(movedX, player.movementInput.rotation.x, ref targetX, ref returnX);
            targetRotationXY.x = targetX;
            targetReturnXY.x = returnX;
            return res;
        }

        public static float CompensateY(float movedY)
        {
            if (!enablePreRecoilCompensation)
            {
                if (targetReturnXY.y * movedY < 0)
                    targetReturnXY.y = Mathf.Max(0, targetReturnXY.y + movedY);
                return movedY;
            }
            float targetY = targetRotationXY.y;
            float returnY = targetReturnXY.y;
            float res = Compensate(movedY, player.movementInput.rotation.y, ref targetY, ref returnY);
            targetRotationXY.y = targetY;
            targetReturnXY.y = returnY;
            return res;
        }

        private static float Compensate(float moved, float original, ref float targetRotation, ref float targetReturn)
        {
            float dsScale = 1;
            if (player.AimingGun)
            {
                dsScale = Mathf.Lerp(1, 1 / PlayerMoveController.Instance.mouseZoomSensitivity, recoilCompensationSensitivityMultiplier);
                //dsScale = 1 / GamePrefs.GetFloat(EnumGamePrefs.OptionsZoomSensitivity);
                //if (player.inventory.holdingItemData.actionData[1] is IModuleContainerFor<ActionModuleDynamicSensitivity.DynamicSensitivityData> dsDataContainer && dsDataContainer.Instance.activated)
                //{
                //    dsScale *= Mathf.Sqrt(dsDataContainer.Instance.ZoomRatio);
                //}
            }
            float modified = moved * dsScale - original;
            float target = ApplyOppositeCompensation(targetRotation, modified, out modified);
            modified /= dsScale;
            float compensated = target - targetRotation;
            //if (compensated < 0)
            //{
            //    Log.Out($"compensated {compensated} prev {targetRotation} cur {target}");
            //}
            targetRotation = target;
            float @return = targetReturn + (modified * targetReturn < 0 ? modified : 0);
            //Log.Out($"return {@return} targetReturn {targetReturn} compensated {compensated} modified {modified}");
            if (@return * targetReturn > 0)
            {
                targetReturn = @return;
            }
            else
            {
                targetReturn = 0;
            }
            return original + modified;
        }

        public static void ApplyRecoil()
        {
            if (player == null)
                return;
            if (state == RecoilState.Recoil)
            {
                //Log.Out($"target rotation {targetRotationXY}");
                //if (targetRotationXY.sqrMagnitude <= 1e-6)
                //{
                //    targetRotationXY = Vector3.zero;
                //    recoilScaledDelta = 1;
                //    returnSpeedCur = Vector3.zero;
                //    state = RecoilState.Return;
                //    return;
                //}
                //returnScaledDelta = 0;

                FastTags<TagGroup.Global> actionTags = player.inventory.holdingItemItemValue.ItemClass.ItemTags;
                MultiActionManager.ModifyItemTags(player.inventory.holdingItemItemValue, player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(player)], ref actionTags);
                float snappinessDefault;
                if (actionTags.Test_AnySet(PistolTag))
                {
                    snappinessDefault = DEFAULT_SNAPPINESS_PISTOL;
                }
                else if (actionTags.Test_AnySet(ShotgunTag))
                {
                    snappinessDefault = DEFAULT_SNAPPINESS_SHOTGUN;
                }
                else
                {
                    snappinessDefault = DEFAULT_SNAPPINESS_RIFLE;
                }
                float snappiness = EffectManager.GetValue(CustomEnums.RecoilSnappiness, player.inventory.holdingItemItemValue, snappinessDefault, player);
                //targetRotationXY = Vector2.Lerp(targetRotationXY, Vector2.zero, returnSpeed * Time.deltaTime);
                float scaledDeltaTime = (Time.time - lastKickTime) * snappiness * 3;
                Vector3 result = Vector3.Lerp(Vector3.zero, new Vector3(targetRotationXY.x, targetRotationXY.y), Mathf.Sin(Mathf.PI * .5f * Mathf.Lerp(0, 1, scaledDeltaTime)));
                targetRotationXY -= new Vector2(result.x, result.y);
                targetReturnXY += new Vector2(result.x, result.y);
                player.movementInput.rotation += result;
                if (scaledDeltaTime >= 1)
                {
                    targetRotationXY = Vector3.zero;
                    returnSpeedCur = Vector3.zero;
                    state = RecoilState.Return;
                }
            }
            else if (state == RecoilState.Return)
            {
                //Log.Out($"target return {targetReturnXY}");
                //if (targetReturnXY.sqrMagnitude <= 1e-6 && totalRotationXY.sqrMagnitude <= 1e-6)
                //{
                //    targetReturnXY = Vector3.zero;
                //    totalRotationXY = Vector2.zero;
                //    returnSpeedCur = Vector3.zero;
                //    totalReturnCur = Vector3.zero;
                //    //returnScaledDelta = 1;
                //    state = RecoilState.None;
                //    return;
                //}
                FastTags<TagGroup.Global> actionTags = player.inventory.holdingItemItemValue.ItemClass.ItemTags;
                MultiActionManager.ModifyItemTags(player.inventory.holdingItemItemValue, player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(player)], ref actionTags);
                float returnSpeedDefault;
                if (actionTags.Test_AnySet(PistolTag))
                {
                    returnSpeedDefault = DEFAULT_RETURN_SPEED_PISTOL;
                }
                else if (actionTags.Test_AnySet(ShotgunTag))
                {
                    returnSpeedDefault = DEFAULT_RETURN_SPEED_SHOTGUN;
                }
                else
                {
                    returnSpeedDefault = DEFAULT_RETURN_SPEED_RIFLE;
                }

                float returnSpeed = EffectManager.GetValue(CustomEnums.RecoilReturnSpeed, player.inventory.holdingItemItemValue, returnSpeedDefault, player);
                //returnScaledDelta += returnSpeed * Time.deltaTime;
                Vector3 result = Vector3.SmoothDamp(Vector3.zero, new Vector3(targetReturnXY.x, targetReturnXY.y), ref returnSpeedCur, 1 / returnSpeed);
                targetReturnXY -= new Vector2(result.x, result.y);
                player.movementInput.rotation -= result;
                if (enableCap)
                {
                    result = Vector3.SmoothDamp(Vector3.zero, new Vector3(totalRotationXY.x, totalRotationXY.y), ref totalReturnCur, 4 / returnSpeed);
                    totalRotationXY -= new Vector2(result.x, result.y);
                }
                if (targetReturnXY.sqrMagnitude <= 1e-6 && totalRotationXY.sqrMagnitude <= 1e-6 && Time.time - lastKickTime >= 1 / returnSpeed)
                {
                    targetReturnXY = Vector3.zero;
                    totalRotationXY = Vector2.zero;
                    returnSpeedCur = Vector3.zero;
                    totalReturnCur = Vector3.zero;
                    state = RecoilState.None;
                }
            }
            else
            {
                ClearData();
            }
        }

        private static float ApplyOppositeCompensation(float target, float mod, out float modRes)
        {
            //mouse movement come in with the same direction as recoil
            if (mod * target >= 0)
            {
                modRes = mod;
                return target;
            }
            float res = target + mod;
            //is mouse movement enough to compensate the recoil?
            if (res * target >= 0)
            {
                modRes = 0;
                return res;
            }
            else
            {
                modRes = res;
                return 0;
            }
        }
    }
}

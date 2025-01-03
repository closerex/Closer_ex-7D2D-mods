using UnityEngine;

namespace KFCommonUtilityLib.Scripts.StaticManagers
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
        private static float recoilScaledDelta = 0f;
        //private static float returnScaledDelta = 0f;
        private static Vector2 targetRotationXY = Vector2.zero;
        private static Vector2 targetReturnXY = Vector2.zero;
        private static Vector3 returnSpeedCur = Vector3.zero;
        private static Vector2 totalRotationXY = Vector2.zero;
        private static Vector3 totalReturnCur = Vector3.zero;
        private static EntityPlayerLocal player;
        // reserved
        public static bool enableCap = true;
        private const float MAX_RECOIL_ANGLE = 15;
        private const float DEFAULT_SNAPPINESS_PISTOL = 6f;
        private const float DEFAULT_SNAPPINESS_RIFLE = 3.6f;
        private const float DEFAULT_SNAPPINESS_SHOTGUN = 8f;
        private const float DEFAULT_RETURN_SPEED_PISTOL = 8f;
        private const float DEFAULT_RETURN_SPEED_RIFLE = 4f;
        private const float DEFAULT_RETURN_SPEED_SHOTGUN = 4f;
        private static readonly FastTags<TagGroup.Global> PistolTag = FastTags<TagGroup.Global>.Parse("pistol");
        private static readonly FastTags<TagGroup.Global> ShotgunTag = FastTags<TagGroup.Global>.Parse("shotgun");

        private static void ClearData()
        {
            state = RecoilState.None;
            recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            targetRotationXY = Vector2.zero;
            targetReturnXY = Vector2.zero;
            totalRotationXY = Vector2.zero;
            totalReturnCur = Vector3.zero;
        }

        public static void InitPlayer(EntityPlayerLocal _player)
        {
            ClearData();
            player = _player;
        }

        public static void Cleanup()
        {
            ClearData();
            player = null;
        }

        public static void AddRecoil(Vector2 recoilRangeHor, Vector2 recoilRangeVer)
        {
            if (player == null) { return; }
            state = RecoilState.Recoil;
            recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            //returnScaledDelta = 0;
            float targetRotationX = player.rand.RandomRange(recoilRangeVer.x, recoilRangeVer.y);
            float targetRotationY = player.rand.RandomRange(recoilRangeHor.x, recoilRangeHor.y);
            if (!player.AimingGun)
            {
                targetRotationXY += new Vector2(targetRotationX, targetRotationY) * 2f;
                totalRotationXY += new Vector2(targetRotationX, targetRotationY) * 2f;
            }
            else
            {
                targetRotationXY += new Vector2(targetRotationX, targetRotationY);
                totalRotationXY += new Vector2(targetRotationX, targetRotationY);
            }
            if (enableCap)
            {
                float targetReturnXCapped = Mathf.Clamp(totalRotationXY.x, -MAX_RECOIL_ANGLE, MAX_RECOIL_ANGLE);
                targetRotationXY.x = Mathf.Clamp(targetRotationXY.x + targetReturnXCapped - totalRotationXY.x, -MAX_RECOIL_ANGLE, MAX_RECOIL_ANGLE);
                totalRotationXY.x = targetReturnXCapped;
            }
        }

        public static float CompensateX(float movedX)
        {
            float targetX = targetRotationXY.x;
            float returnX = targetReturnXY.x;
            float res = Compensate(movedX, player.movementInput.rotation.x, ref targetX, ref returnX);
            targetRotationXY.x = targetX;
            targetReturnXY.x = returnX;
            return res;
        }

        public static float CompensateY(float movedY)
        {
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
                dsScale = 1 / PlayerMoveController.Instance.mouseZoomSensitivity;
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
            if (state == RecoilState.Recoil && recoilScaledDelta < 1)
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
                recoilScaledDelta += snappiness * 3 * Time.deltaTime;
                Vector3 result = Vector3.Lerp(Vector3.zero, new Vector3(targetRotationXY.x, targetRotationXY.y), recoilScaledDelta);
                targetRotationXY -= new Vector2(result.x, result.y);
                targetReturnXY += new Vector2(result.x, result.y);
                player.movementInput.rotation += result;
                if (recoilScaledDelta >= 1)
                {
                    targetRotationXY = Vector3.zero;
                    returnSpeedCur = Vector3.zero;
                    state = RecoilState.Return;
                }
            }
            else if (state == RecoilState.Return && targetReturnXY.sqrMagnitude > 1e-6)
            {
                //Log.Out($"target return {targetReturnXY}");
                if (targetReturnXY.sqrMagnitude <= 1e-6 && totalRotationXY.sqrMagnitude <= 1e-6)
                {
                    targetReturnXY = Vector3.zero;
                    totalRotationXY = Vector2.zero;
                    returnSpeedCur = Vector3.zero;
                    totalReturnCur = Vector3.zero;
                    //returnScaledDelta = 1;
                    state = RecoilState.None;
                    return;
                }
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
                    result = Vector3.SmoothDamp(Vector3.zero, new Vector3(totalRotationXY.x, totalRotationXY.y), ref totalReturnCur, 2 / returnSpeed);
                    totalRotationXY -= new Vector2(result.x, result.y);
                }
                if (targetReturnXY.sqrMagnitude <= 1e-6 && totalRotationXY.sqrMagnitude <= 1e-6)
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

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
        private static Vector3 returnSpeedCur = Vector3.zero;
        private static Vector2 targetRotationXY = Vector2.zero;
        private static Vector2 targetReturnXY = Vector2.zero;
        private static EntityPlayerLocal player;
        private const float MAX_RECOIL_ANGLE = 15;

        public static void InitPlayer(EntityPlayerLocal _player)
        {
            recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            targetRotationXY = Vector2.zero;
            targetReturnXY = Vector2.zero;
            player = _player;
        }

        public static void Cleanup()
        {
            recoilScaledDelta = 0;
            returnSpeedCur = Vector3.zero;
            targetRotationXY = Vector2.zero;
            targetReturnXY = Vector2.zero;
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
                targetReturnXY += new Vector2(targetRotationX, targetRotationY) * 2f;
            }
            else
            {
                targetRotationXY += new Vector2(targetRotationX, targetRotationY);
                targetReturnXY += new Vector2(targetRotationX, targetRotationY);
            }
            float targetReturnXCapped = Mathf.Clamp(targetReturnXY.x, -MAX_RECOIL_ANGLE, MAX_RECOIL_ANGLE);
            targetRotationXY.x = Mathf.Clamp(targetRotationXY.x + targetReturnXCapped - targetReturnXY.x, -MAX_RECOIL_ANGLE, MAX_RECOIL_ANGLE);
            targetReturnXY.x = targetReturnXCapped;
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
            float modified = moved - original;
            float target = ApplyOppositeCompensation(targetRotation, modified, out modified);
            float compensated = target - targetRotation;
            targetRotation = target;
            float @return = targetReturn + compensated + (modified * targetReturn < 0 ? modified : 0);
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
                Log.Out($"target rotation {targetRotationXY}");
                if (targetRotationXY.sqrMagnitude <= 1e-6)
                {
                    targetRotationXY = Vector3.zero;
                    recoilScaledDelta = 1;
                    returnSpeedCur = Vector3.zero;
                    state = RecoilState.Return;
                    return;
                }
                //returnScaledDelta = 0;
                float snappiness = EffectManager.GetValue(CustomEnums.RecoilSnappiness, player.inventory.holdingItemItemValue, 2.4f, player);
                //targetRotationXY = Vector2.Lerp(targetRotationXY, Vector2.zero, returnSpeed * Time.deltaTime);
                recoilScaledDelta += snappiness * Time.deltaTime;
                Vector3 result = Vector3.Slerp(Vector3.zero, new Vector3(targetRotationXY.x, targetRotationXY.y), recoilScaledDelta);
                targetRotationXY -= new Vector2(result.x, result.y);
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
                Log.Out($"target return {targetReturnXY}");
                if (targetReturnXY.sqrMagnitude <= 1e-6)
                {
                    targetReturnXY = Vector3.zero;
                    returnSpeedCur = Vector3.zero;
                    //returnScaledDelta = 1;
                    state = RecoilState.None;
                    return;
                }
                float returnSpeed = EffectManager.GetValue(CustomEnums.RecoilReturnSpeed, player.inventory.holdingItemItemValue, 3.6f, player);
                //returnScaledDelta += returnSpeed * Time.deltaTime;
                Vector3 result = Vector3.SmoothDamp(Vector3.zero, new Vector3(targetReturnXY.x, targetReturnXY.y), ref returnSpeedCur, 1 / returnSpeed);
                targetReturnXY -= new Vector2(result.x, result.y);
                player.movementInput.rotation -= result;
                if (targetReturnXY.sqrMagnitude <= 1e-6)
                {
                    targetReturnXY = Vector3.zero;
                    returnSpeedCur = Vector3.zero;
                    state = RecoilState.None;
                }
            }
            else
            {
                state = RecoilState.None;
                recoilScaledDelta = 0;
                returnSpeedCur = Vector3.zero;
                //returnScaledDelta = 0;
                targetRotationXY = Vector3.zero;
                targetReturnXY = Vector3.zero;
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

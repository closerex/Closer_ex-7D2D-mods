using UnityEngine;

namespace KFCommonUtilityLib.Scripts.StaticManagers
{
    public static class RecoilManager
    {
        private static Vector2 targetRotationXY = Vector2.zero;
        private static EntityPlayerLocal player;

        public static void InitPlayer(EntityPlayerLocal _player)
        {
            targetRotationXY = Vector2.zero;
            player = _player;
        }

        public static void Cleanup()
        {
            targetRotationXY = Vector2.zero;
            player = null;
        }

        public static void AddRecoil(Vector2 recoilRangeHor, Vector2 recoilRangeVer)
        {
            if (player == null) { return; }
            if (!player.AimingGun)
            {
                targetRotationXY.x += player.rand.RandomRange(recoilRangeVer.x, recoilRangeVer.y) * 2f;
                targetRotationXY.y += player.rand.RandomRange(recoilRangeHor.x, recoilRangeHor.y) * 2f;
                return;
            }
            targetRotationXY.x += player.rand.RandomRange(recoilRangeVer.x, recoilRangeVer.y);
            targetRotationXY.y += player.rand.RandomRange(recoilRangeHor.x, recoilRangeHor.y);
        }

        public static float CompensateX(float movedX)
        {
            float originalX = player.movementInput.rotation.x;
            float modifiedX = movedX - originalX;
            if (targetRotationXY.x * modifiedX >= 0)
                return movedX;
            targetRotationXY.x = ApplyOppositeCompensation(targetRotationXY.x, modifiedX, out modifiedX);
            return originalX + modifiedX;
        }

        public static float CompensateY(float movedY)
        {
            float originalY = player.movementInput.rotation.y;
            float modifiedY = movedY - originalY;
            if (targetRotationXY.y * modifiedY >= 0)
                return movedY;
            targetRotationXY.y = ApplyOppositeCompensation(targetRotationXY.y, modifiedY, out modifiedY);
            return originalY + modifiedY;
        }

        public static void ApplyRecoil()
        {
            if (player == null || targetRotationXY.sqrMagnitude <= 1e-6)
                return;
            float snappiness = EffectManager.GetValue(CustomEnums.RecoilSnappiness, player.inventory.holdingItemItemValue, 6, player);
            float returnSpeed = EffectManager.GetValue(CustomEnums.RecoilReturnSpeed, player.inventory.holdingItemItemValue, 2, player);
            //targetRotationXY = Vector2.Lerp(targetRotationXY, Vector2.zero, returnSpeed * Time.deltaTime);
            Vector3 result = Vector3.Slerp(Vector3.zero, new Vector3(targetRotationXY.x, targetRotationXY.y), snappiness * Time.fixedDeltaTime);
            targetRotationXY -= new Vector2(result.x, result.y);
            player.movementInput.rotation += result;
        }

        private static float ApplyOppositeCompensation(float target, float mod, out float modRes)
        {
            if (mod * target >= 0)
            {
                modRes = mod;
                return target;
            }
            float res = target + mod;
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

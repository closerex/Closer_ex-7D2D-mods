using System.Collections;
using UnityEngine;

[AddComponentMenu("KFAttachments/Weapon Display Controllers/Weapon Label Controller Charge Up")]
public class WeaponLabelControllerChargeUp : ApexWeaponHudControllerBase
{
    [SerializeField, Range(0.001f, 1f)]
    protected float tickTime;
    [SerializeField, Range(1, 1000)]
    protected int updateTicks = 1;
    protected Coroutine curChargeProc;
    protected bool isChargeRunning = false;
    internal void StartChargeUp()
    {
        if (shaderEnabled && (curChargeProc == null || !isChargeRunning))
            curChargeProc = StartCoroutine(ChargeUp());
    }

    internal void StopChargeUp()
    {
        if (shaderEnabled && curChargeProc != null)
        {
            StopCoroutine(curChargeProc);
            curChargeProc = null;
            isChargeRunning = false;
        }
    }

    protected override void OnDisable()
    {
        StopChargeUp();
    }

    private IEnumerator ChargeUp()
    {
        isChargeRunning = true;
        float chargeLeap = (float)dataArray[0] / updateTicks;
        int[] chargeArray = new int[4];
        float curChargeCount = 0;
        dataArray.CopyTo(chargeArray, 0);
        int max = dataArray[1];
        chargeArray[1] = (int)curChargeCount;
        while (chargeArray[1] <= max)
        {
            Dispatch(chargeArray);
            if (chargeArray[1] == max)
                break;
            yield return new WaitForSecondsRealtime(tickTime);
            max = dataArray[1];
            curChargeCount += chargeLeap;
            chargeArray[1] = Mathf.Min((int)curChargeCount, max);
        }
        isChargeRunning = false;
        yield break;
    }

    protected override bool CanDispatch()
    {
        return base.CanDispatch() && !isChargeRunning;
    }
}

using UnityEngine;

public class VPVerticalWeaponRotator : VehicleWeaponRotatorBase
{
    protected VehicleWeaponRotatorBase horRotator;
    public override Transform HorRotTrans => horRotator?.HorRotTrans;
    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        string str = null;
        properties.ParseString("horizontalRotator", ref str);
        if (!string.IsNullOrEmpty(str))
            horRotator = vehicle.FindPart(str) as VehicleWeaponRotatorBase;
    }
    protected override void CalcCurRotation(float _dt)
    {
        Vector3 dir = inputRay.direction;
        Vector3 lookRot = (Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir)).eulerAngles;
        nextVerRot = AngleToLimited(AngleToInferior(lookRot.x), -verticleMaxRotation, -verticleMinRotation);
    }

    protected internal override bool IsOnTarget()
    {
        return (verRotTrans == null || FuzzyEqualAngle(nextVerRot, AngleToInferior(verRotTrans.localEulerAngles.x), 0.5f)) && (horRotator == null || horRotator.IsOnTarget());
    }
}

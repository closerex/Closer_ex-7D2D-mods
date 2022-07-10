using UnityEngine;

public class VPHorizontalWeaponRotator : VehicleWeaponRotatorBase
{
    protected VehicleWeaponRotatorBase verRotator;
    public override Transform VerRotTrans => verRotator?.VerRotTrans;
    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        string str = null;
        properties.ParseString("verticalRotator", ref str);
        if (!string.IsNullOrEmpty(str))
            verRotator = vehicle.FindPart(str) as VehicleWeaponRotatorBase;
    }
    protected override void CalcCurRotation(float _dt)
    {
        Vector3 dir = inputRay.direction;
        Vector3 lookRot = (Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir)).eulerAngles;
        nextHorRot = AngleToLimited(AngleToInferior(lookRot.y), horizontalMinRotation, horizontalMaxRotation);
    }

    protected internal override bool IsOnTarget()
    {
        return (horRotTrans == null || FuzzyEqualAngle(nextHorRot, AngleToInferior(horRotTrans.localEulerAngles.y), 1f)) && (verRotator == null || verRotator.IsOnTarget());
    }
}

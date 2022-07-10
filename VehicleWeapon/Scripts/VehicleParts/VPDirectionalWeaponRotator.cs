using UnityEngine;

public class VPDirectionalWeaponRotator : VehicleWeaponRotatorBase
{
    protected override void CalcCurRotation(float _dt)
    {
        Vector3 dir = inputRay.direction;
        Vector3 lookRot = (Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir)).eulerAngles;
        nextHorRot = AngleToLimited(AngleToInferior(lookRot.y), horizontalMinRotation, horizontalMaxRotation);
        nextVerRot = AngleToLimited(AngleToInferior(lookRot.x), -verticleMaxRotation, -verticleMinRotation);
    }
}


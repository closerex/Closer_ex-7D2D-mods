using UnityEngine;

public class VPParticleWeaponRotator : VehicleWeaponHitposPreviewRotatorBase
{
    public override void SetWeapon(VehicleWeaponBase weapon)
    {
        base.SetWeapon(weapon);
        if(weapon is VPParticleWeapon particleWeapon)
        {
            var main = particleWeapon.WeaponSystem.main;
            if (main.startSpeed.mode == ParticleSystemCurveMode.Constant)
                projectileSpeed = main.startSpeed.constant;
            if (main.gravityModifier.mode == ParticleSystemCurveMode.Constant)
                gravity = main.gravityModifier.constant * Physics.gravity.y;
        }
    }

    protected override void DoCalcCurRotation(out float targetHorAngle, out float targetVerAngle)
    {
        Vector3 aimAt = Quaternion.LookRotation(hitPos - (weapon as VPParticleWeapon).WeaponSystem.transform.position).eulerAngles;
        aimAt.x = -Angle(hitPos, (weapon as VPParticleWeapon).WeaponSystem.transform.position, projectileSpeed, gravity);
        aimAt = (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(aimAt)).eulerAngles;
        targetHorAngle = AngleToLimited(AngleToInferior(aimAt.y), horizontalMinRotation, horizontalMaxRotation);
        targetVerAngle = AngleToLimited(AngleToInferior(aimAt.x), -verticleMaxRotation, -verticleMinRotation);
    }
}


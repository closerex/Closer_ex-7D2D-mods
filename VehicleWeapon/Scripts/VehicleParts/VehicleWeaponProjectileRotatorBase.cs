using UnityEngine;

public abstract class VehicleWeaponProjectileRotatorBase : VehicleWeaponRotatorBase
{
    protected float gravity = 1f;
    protected float projectileSpeed = 0f;

    public override void SetWeapon(VehicleWeaponBase weapon)
    {
        base.SetWeapon(weapon);

        properties.ParseFloat("projectileSpeed", ref projectileSpeed);
        properties.ParseFloat("gravity", ref gravity);
        gravity *= Physics.gravity.y;
    }

    protected float Angle(Vector3 target, Vector3 origin, float projectileSpeed, float gravity)
    {
        if (projectileSpeed <= 0)
            return 0;
        float distX = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(origin.x, origin.z));
        float posBase = (gravity * Mathf.Pow(distX, 2.0f)) / (2.0f * Mathf.Pow(projectileSpeed, 2.0f));
        if (posBase == 0f)
            return 0;
        float distY = target.y - origin.y;
        float posX = distX / posBase;
        float posY = (Mathf.Pow(posX, 2.0f) / 4.0f) - ((posBase - distY) / posBase);
        float angleX = posY >= 0.0f ? Mathf.Rad2Deg * Mathf.Atan(-posX / 2.0f - Mathf.Pow(posY, 0.5f)) : 45f;
        return angleX;
    }
}


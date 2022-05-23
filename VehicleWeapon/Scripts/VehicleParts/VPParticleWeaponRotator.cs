using System;
using UnityEngine;

public class VPParticleWeaponRotator : VehicleWeaponRotatorBase
{
    protected float gravity = 1f;
    protected float projectileSpeed = 0f;
    protected Transform explPreviewTransEntity = null;
    protected Transform explPreviewTransBlock = null;
    protected Color previewColorEntityOnTarget;
    protected Color previewColorEntityAiming;
    protected float previewScaleEntity = 0;
    protected PrimitiveType previewTypeEntity = PrimitiveType.Sphere;
    protected Color previewColorBlockOnTarget;
    protected Color previewColorBlockAiming;
    protected float previewScaleBlock = 0;
    protected PrimitiveType previewTypeBlock = PrimitiveType.Sphere;
    protected Vector3 hitPos;
    protected static readonly int colorId = Shader.PropertyToID("_Color");

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        previewColorEntityOnTarget = Color.clear;
        previewColorEntityAiming = Color.clear;
        previewColorBlockOnTarget = Color.clear;
        previewColorBlockAiming = Color.clear;
        string str = null;
        _properties.ParseString("previewColorEntityOnTarget", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorEntityOnTarget);
        str = null;
        _properties.ParseString("previewColorEntityAiming", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorEntityAiming);
        str = null;
        _properties.ParseString("previewColorBlockOnTarget", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorBlockOnTarget);
        str = null;
        _properties.ParseString("previewColorBlockAiming", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorBlockAiming);
        str = null;
        _properties.ParseString("previewTypeEntity", ref str);
        if (!string.IsNullOrEmpty(str) && !Enum.TryParse<PrimitiveType>(str, out previewTypeEntity))
            previewTypeEntity = PrimitiveType.Sphere;
        str = null;
        _properties.ParseString("previewTypeBlock", ref str);
        if (!string.IsNullOrEmpty(str) && !Enum.TryParse<PrimitiveType>(str, out previewTypeBlock))
            previewTypeBlock = PrimitiveType.Sphere;
    }

    public override void SetWeapon(VehicleWeaponBase weapon)
    {
        base.SetWeapon(weapon);
        if(weapon is VPParticleWeapon hornWeapon)
        {
            var component = hornWeapon.Component;
            previewScaleEntity = component.BoundExplosionData.EntityRadius;
            previewScaleBlock = component.BoundExplosionData.BlockRadius;

            var main = hornWeapon.WeaponSystem.main;
            if (main.startSpeed.mode == ParticleSystemCurveMode.Constant)
                projectileSpeed = main.startSpeed.constant;
            if (main.gravityModifier.mode == ParticleSystemCurveMode.Constant)
                gravity = main.gravityModifier.constant;
        }

        properties.ParseFloat("projectileSpeed", ref projectileSpeed);
        properties.ParseFloat("gravity", ref gravity);
        gravity *= Physics.gravity.y;
        properties.ParseFloat("previewScaleEntity", ref previewScaleEntity);
        properties.ParseFloat("previewScaleBlock", ref previewScaleBlock);
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);

        bool onTarget = FuzzyEqualAngle(nextHorRot, AngleToInferior(horRotTrans.localEulerAngles.y), 1f) && FuzzyEqualAngle(nextVerRot, AngleToInferior(verRotTrans.localEulerAngles.x), 0.5f);
        if (onTarget != lastOnTarget)
        {
            SetPreviewColor(onTarget);
            lastOnTarget = onTarget;
        }
    }

    protected override void CalcCurRotation(float _dt)
    {
        nextHorRot = horRotTrans.localEulerAngles.y;
        nextVerRot = verRotTrans.localEulerAngles.x;

        if (DoRaycast(out RaycastHit hitInfo))
        {
            DoCalcCurRotation(hitInfo, out nextHorRot, out nextVerRot);
        }
    }

    protected virtual bool DoRaycast(out RaycastHit hitInfo)
    {
        Ray lookRay = player.playerCamera.ScreenPointToRay(Input.mousePosition);
        lookRay.origin = hasRaycastTransform ? hitRayTrans.position : hitRayTrans.position + Vector3.up * 2;
        return Physics.Raycast(lookRay, out hitInfo);
    }

    protected virtual void DoCalcCurRotation(RaycastHit hitInfo, out float targetHorAngle, out float targetVerAngle)
    {
        hitPos = hitInfo.point;
        Vector3 aimAt = Quaternion.LookRotation(hitPos - horRotTrans.position).eulerAngles;
        aimAt.x = -AngleToLimited(Angle(hitPos, (weapon as VPParticleWeapon).WeaponSystem.transform.position), verticleMinRotation, verticleMaxRotation);
        aimAt = (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(aimAt)).eulerAngles;
        aimAt.x = AngleToInferior(aimAt.x);
        aimAt.y = AngleToInferior(aimAt.y);
        aimAt.y = AngleToLimited(aimAt.y, horizontalMinRotation, horizontalMaxRotation);
        targetHorAngle = aimAt.y;
        targetVerAngle = aimAt.x;
    }

    protected override void DoRotateTowards(float _dt)
    {
        bool updatePreview = false;
        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        if (!FuzzyEqualAngle(curHorAngle, nextHorRot, 0.01f))
        {
            HorRotateTowards(_dt);
            updatePreview = true;
        }
        if (!FuzzyEqualAngle(curVerAngle, nextVerRot, 0.01f))
        {
            VerRotateTowards(_dt);
            updatePreview = true;
        }
        if (updatePreview)
            UpdatePreviewPos(hitPos);
    }

    public override void CreatePreview()
    {
        DestroyPreview();
        if (previewScaleEntity > 0 && previewColorEntityOnTarget.a > 0)
        {
            explPreviewTransEntity = GameObject.CreatePrimitive(previewTypeEntity).transform;
            explPreviewTransEntity.localScale *= previewScaleEntity;
            GameObject.Destroy(explPreviewTransEntity.GetComponent<Collider>());
            Material mat = explPreviewTransEntity.GetComponent<Renderer>().material;
            mat.SetColor("_Color", previewColorEntityAiming);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
        if (previewScaleBlock > 0 && previewColorBlockOnTarget.a > 0)
        {
            explPreviewTransBlock = GameObject.CreatePrimitive(previewTypeBlock).transform;
            explPreviewTransBlock.localScale *= previewScaleBlock;
            GameObject.Destroy(explPreviewTransBlock.GetComponent<Collider>());
            Material mat = explPreviewTransBlock.GetComponent<Renderer>().material;
            mat.SetColor("_Color", previewColorBlockAiming);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
    }

    public override void DestroyPreview()
    {
        if (explPreviewTransEntity != null)
        {
            GameObject.Destroy(explPreviewTransEntity.gameObject);
            explPreviewTransEntity = null;
        }
        if (explPreviewTransBlock != null)
        {
            GameObject.Destroy(explPreviewTransBlock.gameObject);
            explPreviewTransBlock = null;
        }
    }

    protected override void UpdatePreviewPos(Vector3 position)
    {
        if (explPreviewTransEntity != null)
            explPreviewTransEntity.position = position;
        if (explPreviewTransBlock != null)
            explPreviewTransBlock.position = position;
    }

    protected virtual void SetPreviewColor(bool onTarget)
    {
        if (explPreviewTransEntity != null)
            explPreviewTransEntity.GetComponent<Renderer>().material.SetColor(colorId, onTarget ? previewColorEntityOnTarget : previewColorEntityAiming);
        if (explPreviewTransBlock != null)
            explPreviewTransBlock.GetComponent<Renderer>().material.SetColor(colorId, onTarget ? previewColorBlockOnTarget : previewColorBlockAiming);
    }

    protected float Angle(Vector3 target, Vector3 origin)
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


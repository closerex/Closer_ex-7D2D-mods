using System;
using UnityEngine;

public abstract class VehicleWeaponHitposPreviewRotatorBase : VehicleWeaponProjectileRotatorBase
{
    protected Transform hitRayTrans = null;
    protected bool hasRaycastTransform = false;
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
    protected float indicatorOffsetY = 0;
    protected Vector3 hitPos;
    protected static readonly int colorId = Shader.PropertyToID("_Color");

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        string str = null;
        previewColorEntityOnTarget = Color.clear;
        properties.ParseString("previewColorEntityOnTarget", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewColorEntityOnTarget", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorEntityOnTarget);

        str = null;
        previewColorEntityAiming = Color.clear;
        properties.ParseString("previewColorEntityAiming", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewColorEntityAiming", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorEntityAiming);

        str = null;
        previewColorBlockOnTarget = Color.clear;
        properties.ParseString("previewColorBlockOnTarget", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewColorBlockOnTarget", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorBlockOnTarget);

        str = null;
        previewColorBlockAiming = Color.clear;
        properties.ParseString("previewColorBlockAiming", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewColorBlockAiming", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorBlockAiming);

        str = null;
        previewTypeEntity = PrimitiveType.Sphere;
        properties.ParseString("previewTypeEntity", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewTypeEntity", str);
        if (!string.IsNullOrEmpty(str))
            Enum.TryParse<PrimitiveType>(str, out previewTypeEntity);

        str = null;
        previewTypeBlock = PrimitiveType.Sphere;
        properties.ParseString("previewTypeBlock", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewTypeBlock", str);
        if (!string.IsNullOrEmpty(str))
            Enum.TryParse<PrimitiveType>(str, out previewTypeBlock);

        indicatorOffsetY = 0;
        properties.ParseFloat("indicatorOffsetY", ref indicatorOffsetY);
        indicatorOffsetY = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "indicatorOffsetY", indicatorOffsetY.ToString()));
        previewScaleEntity = 0;
        properties.ParseFloat("previewScaleEntity", ref previewScaleEntity);
        previewScaleEntity = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewScaleEntity", previewScaleEntity.ToString()));
        previewScaleBlock = 0;
        properties.ParseFloat("previewScaleBlock", ref previewScaleBlock);
        previewScaleBlock = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "previewScaleBlock", previewScaleBlock.ToString()));
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        hitRayTrans = GetTransform("hitRaycastTransform");
        if (!hitRayTrans)
            hitRayTrans = transform;
        else
            hasRaycastTransform = true;
    }

    protected override void SetInputRay()
    {
        base.SetInputRay();
        inputRay.origin = hasRaycastTransform ? hitRayTrans.position : hitRayTrans.position + Vector3.up * 2;
    }

    protected override void CalcCurRotation(float _dt)
    {
        nextHorRot = horRotTrans.localEulerAngles.y;
        nextVerRot = verRotTrans.localEulerAngles.x;

        if (DoRaycast(out RaycastHit hitInfo))
        {
            hitPos = hitInfo.point;
            DoCalcCurRotation(out nextHorRot, out nextVerRot);
        }
    }

    protected virtual bool DoRaycast(out RaycastHit hitInfo)
    {
        return Physics.Raycast(inputRay, out hitInfo);
    }

    protected abstract void DoCalcCurRotation(out float targetHorAngle, out float targetVerAngle);

    protected override bool DoRotateTowards(float _dt, bool forced = false)
    {
        if (base.DoRotateTowards(_dt, forced))
        {
            UpdatePreviewPos(hitPos);
            return true;
        }
        return false;
    }

    public override void CreatePreview()
    {
        base.CreatePreview();
        if (previewScaleEntity > 0 && previewColorEntityOnTarget.a > 0)
        {
            explPreviewTransEntity = GameObject.CreatePrimitive(previewTypeEntity).transform;
            explPreviewTransEntity.localScale *= previewScaleEntity;
            GameObject.Destroy(explPreviewTransEntity.GetComponent<Collider>());
            Material mat = explPreviewTransEntity.GetComponent<Renderer>().material;
            mat.enableInstancing = true;
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
            mat.enableInstancing = true;
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
        base.DestroyPreview();
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
        if (indicatorTrans != null)
            indicatorTrans.position = position + Vector3.up * indicatorOffsetY;
    }

    protected override void SetPreviewColor(bool onTarget)
    {
        base.SetPreviewColor(onTarget);
        if (explPreviewTransEntity != null)
            explPreviewTransEntity.GetComponent<Renderer>().material.SetColor(colorId, onTarget ? previewColorEntityOnTarget : previewColorEntityAiming);
        if (explPreviewTransBlock != null)
            explPreviewTransBlock.GetComponent<Renderer>().material.SetColor(colorId, onTarget ? previewColorBlockOnTarget : previewColorBlockAiming);
    }
}


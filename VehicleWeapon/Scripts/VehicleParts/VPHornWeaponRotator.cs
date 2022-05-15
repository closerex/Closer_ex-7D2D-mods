using System;
using UnityEngine;

public class VPHornWeaponRotator : VehiclePart
{
    protected Transform transform = null;
    protected Transform horRotTrans = null;
    protected Transform verRotTrans = null;
    protected Transform hitRayTrans = null;
    protected bool hasRaycastTransform = false;
    protected float gravity = 1f;
    protected float projectileSpeed = 30f;
    protected float verticleMaxRotation = 45f;
    protected float verticleMinRotation = 0f;
    protected float verticleRotSpeed = 360f;
    protected float horizontalMaxRotation = 180f;
    protected float horizontalMinRotation = -180f;
    protected float horizontalRotSpeed = 360f;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected Transform explPreviewTransEntity = null;
    protected Transform explPreviewTransBlock = null;
    protected Color previewColorEntityOnTarget;
    protected Color previewColorEntityAiming;
    protected float previewScaleEntity;
    protected PrimitiveType previewTypeEntity = PrimitiveType.Sphere;
    protected Color previewColorBlockOnTarget;
    protected Color previewColorBlockAiming;
    protected float previewScaleBlock;
    protected PrimitiveType previewTypeBlock = PrimitiveType.Sphere;
    protected bool lastOnTarget = false;
    protected bool fullCircleRotation = false;
    protected static readonly int colorId = Shader.PropertyToID("_Color");
    protected EntityPlayerLocal player = null;
    protected VPHornWeapon horn = null;

    public Transform HorRotTrans { get => horRotTrans; }
    public Transform VerRotTrans { get => verRotTrans; }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        _properties.ParseFloat("verticleMaxRotation", ref verticleMaxRotation);
        verticleMaxRotation = AngleToInferior(verticleMaxRotation);
        _properties.ParseFloat("verticleMinRotation", ref verticleMinRotation);
        verticleMinRotation = AngleToInferior(verticleMinRotation);
        _properties.ParseFloat("verticleRotationSpeed", ref verticleRotSpeed);
        verticleRotSpeed = Mathf.Abs(verticleRotSpeed);
        _properties.ParseFloat("horizontalMaxRotation", ref horizontalMaxRotation);
        horizontalMaxRotation = AngleToInferior(horizontalMaxRotation);
        _properties.ParseFloat("horizontalMinRotation", ref horizontalMinRotation);
        horizontalMinRotation = AngleToInferior(horizontalMinRotation);
        _properties.ParseFloat("horizontalRotationSpeed", ref horizontalRotSpeed);
        horizontalRotSpeed = Mathf.Abs(horizontalRotSpeed);
        fullCircleRotation = horizontalMaxRotation == 180f && horizontalMinRotation == -180f;

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

        properties.ParseFloat("projectileSpeed", ref projectileSpeed);
        properties.ParseFloat("gravity", ref gravity);
        gravity *= Physics.gravity.y;

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }
    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        horn = vehicle.FindPart("hornWeapon") as VPHornWeapon;
        if (horn == null)
            return;

        transform = GetTransform();
        horRotTrans = GetTransform("horRotationTransform");
        verRotTrans = GetTransform("verRotationTransform");
        hitRayTrans = GetTransform("hitRaycastTransform");
        if (!hitRayTrans)
            hitRayTrans = transform;
        else
            hasRaycastTransform = true;

        var component = horn.Component;
        previewScaleEntity = component.BoundExplosionData.EntityRadius;
        previewScaleBlock = component.BoundExplosionData.BlockRadius;
        properties.ParseFloat("previewScaleEntity", ref previewScaleEntity);
        properties.ParseFloat("previewScaleBlock", ref previewScaleBlock);
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (horn == null || !horn.HasOperator)
            return;
        CalcCurRotation(_dt);

        if (Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f || Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x), false, -1, player.entityId);
            else if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x));
            lastHorRot = horRotTrans.localEulerAngles.y;
            lastVerRot = verRotTrans.localEulerAngles.x;
        }
    }

    public void NetSyncUpdate(float horRot, float verRot)
    {
        horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, horRot, horRotTrans.localEulerAngles.z);
        verRotTrans.localEulerAngles = new Vector3(verRot, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }

    protected virtual void CalcCurRotation(float _dt)
    {
        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        float targetHorAngle = horRotTrans.localEulerAngles.y;
        float targetVerAngle = verRotTrans.localEulerAngles.x;

        if (DoRaycast(out RaycastHit hitInfo))
        {
            DoCalcCurRotation(hitInfo, out Vector3 hitPos, out targetHorAngle, out targetVerAngle);
            bool updatePreview = false;
            if (!FuzzyEqualAngle(curHorAngle, targetHorAngle, 0.01f))
            {
                HorRotateTowards(targetHorAngle, _dt);
                updatePreview = true;
            }
            if (!FuzzyEqualAngle(curVerAngle, targetVerAngle, 0.01f))
            {
                VerRotateTowards(targetVerAngle, _dt);
                updatePreview = true;
            }
            if (updatePreview)
                UpdatePreviewPos(hitPos);
        }

        bool onTarget = FuzzyEqualAngle(targetHorAngle, AngleToInferior(horRotTrans.localEulerAngles.y), 1f) && FuzzyEqualAngle(targetVerAngle, AngleToInferior(verRotTrans.localEulerAngles.x), 0.5f);
        if (onTarget != lastOnTarget)
        {
            SetPreviewColor(onTarget);
            lastOnTarget = onTarget;
        }
    }

    protected virtual bool DoRaycast(out RaycastHit hitInfo)
    {
        Ray lookRay = player.playerCamera.ScreenPointToRay(Input.mousePosition);
        lookRay.origin = hasRaycastTransform ? hitRayTrans.position : hitRayTrans.position + Vector3.up * 2;
        return Physics.Raycast(lookRay, out hitInfo);
    }

    protected virtual void DoCalcCurRotation(RaycastHit hitInfo, out Vector3 hitPos, out float targetHorAngle, out float targetVerAngle)
    {
        hitPos = hitInfo.point;
        Vector3 aimAt = Quaternion.LookRotation(hitPos - horRotTrans.position).eulerAngles;
        aimAt.x = -AngleToLimited(Angle(hitPos), verticleMinRotation, verticleMaxRotation);
        aimAt = (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(aimAt)).eulerAngles;
        aimAt.x = AngleToInferior(aimAt.x);
        aimAt.y = AngleToInferior(aimAt.y);
        aimAt.y = AngleToLimited(aimAt.y, horizontalMinRotation, horizontalMaxRotation);
        targetHorAngle = aimAt.y;
        targetVerAngle = aimAt.x;
    }

    protected virtual void HorRotateTowards(float targetHorAngle, float _dt)
    {
        //targetHorAngle = AngleToLimited(targetHorAngle, horizontalMinRotation, horizontalMaxRotation);
        float maxRotPerUpdate = horizontalRotSpeed * _dt;
        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        float nextHorAngle;
        if (!fullCircleRotation)
            nextHorAngle = targetHorAngle > curHorAngle ? Mathf.Min(curHorAngle + maxRotPerUpdate, targetHorAngle) : Mathf.Max(curHorAngle - maxRotPerUpdate, targetHorAngle);
        else
        {
            if (targetHorAngle > 0 && curHorAngle < 0)
            {
                if (targetHorAngle - curHorAngle > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if (nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Max(nextHorAngle, targetHorAngle);
                }
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if (nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, targetHorAngle);
                }
            }
            else if (targetHorAngle < 0 && curHorAngle > 0)
            {
                if (curHorAngle - targetHorAngle > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if (nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, targetHorAngle);
                }
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if (nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Max(nextHorAngle, targetHorAngle);
                }
            }
            else
                nextHorAngle = targetHorAngle > curHorAngle ? Mathf.Min(curHorAngle + maxRotPerUpdate, targetHorAngle) : Mathf.Max(curHorAngle - maxRotPerUpdate, targetHorAngle);
        }
        horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, nextHorAngle, horRotTrans.localEulerAngles.z);
    }

    protected virtual void VerRotateTowards(float targetVerAngle, float _dt)
    {
        //targetVerAngle = AngleToLimited(targetVerAngle, verticleMinRotation, verticleMaxRotation);
        float maxRotPerUpdate = verticleRotSpeed * _dt;
        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        float nextVerAngle = targetVerAngle > curVerAngle ? Mathf.Min(curVerAngle + maxRotPerUpdate, targetVerAngle) : Mathf.Max(curVerAngle - maxRotPerUpdate, targetVerAngle);
        verRotTrans.localEulerAngles = new Vector3(nextVerAngle, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }

    public virtual void CreatePreview()
    {
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

    public virtual void DestroyPreview()
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

    protected virtual void UpdatePreviewPos(Vector3 position)
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

    protected bool FuzzyEqualAngle(float angle1, float angle2, float fuzzy)
    {
        return Mathf.Abs(angle1 - angle2) <= fuzzy;
    }

    protected float Angle(Vector3 target)
    {
        float distX = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(horn.HornSystem.transform.position.x, transform.position.z));
        float distY = target.y - horn.HornSystem.transform.position.y;
        if(projectileSpeed <= 0)
        {
            Log.Out("invalid projectile speed: " + projectileSpeed);
            return 0;
        }
        float posBase = (gravity * Mathf.Pow(distX, 2.0f)) / (2.0f * Mathf.Pow(projectileSpeed, 2.0f));
        if(posBase == 0f)
        {
            Log.Out("dividing by zero!");
            return 0;
        }
        float posX = distX / posBase;
        float posY = (Mathf.Pow(posX, 2.0f) / 4.0f) - ((posBase - distY) / posBase);
        float angleX = posY >= 0.0f ? Mathf.Rad2Deg * Mathf.Atan(-posX / 2.0f - Mathf.Pow(posY, 0.5f)) : 45f;
        return angleX;
    }

    protected float AngleToInferior(float angle)
    {
        angle %= 360;
        angle = angle > 180 ? angle - 360 : angle;
        return angle;
    }

    protected float AngleToLimited(float angle, float min, float max)
    {
        float res = Mathf.Min(max, Mathf.Max(min, angle));
        return res;
    }
}


using System;
using System.Collections;
using UnityEngine;

public class VPHornWeapon : VehiclePart
{
    protected int burstCount = 1;
    protected int burstRepeat = 1;
    protected float burstInterval = 0f;
    protected float hornInterval = 1f;
    protected float hornCooldown = 0f;
    protected bool hasOperator = false;
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string hornEmptySound = string.Empty;
    protected string hornNotReadySound = string.Empty;
    protected string hornReloadSound = string.Empty;
    protected CustomParticleComponents component = null;
    protected Transform transform = null;
    protected Transform horRotTrans = null;
    protected Transform verRotTrans = null;
    protected Transform hitRayTrans = null;
    protected bool hasRaycastTransform = false;
    protected ParticleSystem hornSystem = null;
    protected SubExplosionInitializer initializer = null;
    protected EntityPlayerLocal player = null;
    protected float gravity = 1f;
    protected float projectileSpeed = 30f;
    protected float verticleMaxRotation = 45f;
    protected float verticleMinRotation = 0f;
    protected float horizontalMaxRotation = 180f;
    protected float horizontalMinRotation = -180f;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected bool isCoRunning = false;
    protected Transform explPreviewTransEntity = null;
    protected Transform explPreviewTransBlock = null;
    protected Color previewColorEntity;
    protected float previewScaleEntity;
    protected PrimitiveType previewTypeEntity = PrimitiveType.Sphere;
    protected Color previewColorBlock;
    protected float previewScaleBlock;
    protected PrimitiveType previewTypeBlock = PrimitiveType.Sphere;
    protected ItemValue ammoValue = ItemValue.None.Clone();

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseInt("burstCount", ref burstCount);
        _properties.ParseFloat("burstInterval", ref burstInterval);
        _properties.ParseInt("burstRepeat", ref burstRepeat);
        _properties.ParseFloat("hornInterval", ref hornInterval);
        hornCooldown = hornInterval;
        string str = null;
        _properties.ParseString("particleIndex", ref str);
        if (!string.IsNullOrEmpty(str))
            CustomParticleEffectLoader.GetCustomParticleComponents(PlatformIndependentHash.StringToUInt16(str), out component);
        _properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        _properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        _properties.ParseString("emptySound", ref hornEmptySound);
        _properties.ParseString("notReadySound", ref hornNotReadySound);
        _properties.ParseString("reloadSound", ref hornReloadSound);
        _properties.ParseFloat("verticleMaxRotation", ref verticleMaxRotation);
        _properties.ParseFloat("verticleMinRotation", ref verticleMinRotation);
        _properties.ParseFloat("horizontalMaxRotation", ref horizontalMaxRotation);
        _properties.ParseFloat("horizontalMinRotation", ref horizontalMinRotation);
        previewScaleEntity = component.BoundExplosionData.EntityRadius;
        previewScaleBlock = component.BoundExplosionData.BlockRadius;
        _properties.ParseFloat("previewScaleEntity", ref previewScaleEntity);
        _properties.ParseFloat("previewScaleBlock", ref previewScaleBlock);
        str = null;
        previewColorEntity = Color.clear;
        previewColorBlock = Color.clear;
        _properties.ParseString("previewColorEntity", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorEntity);
        str = null;
        _properties.ParseString("previewColorBlock", ref str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out previewColorBlock);
        str = null;
        _properties.ParseString("previewTypeEntity", ref str);
        if (!string.IsNullOrEmpty(str) && !Enum.TryParse<PrimitiveType>(str, out previewTypeEntity))
            previewTypeEntity = PrimitiveType.Sphere;
        str = null;
        _properties.ParseString("previewTypeBlock", ref str);
        if (!string.IsNullOrEmpty(str) && !Enum.TryParse<PrimitiveType>(str, out previewTypeBlock))
            previewTypeBlock = PrimitiveType.Sphere;
        str = null;
        _properties.ParseString("ammo", ref str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        transform = GetTransform();
        horRotTrans = GetTransform("horRotationTransform");
        verRotTrans = GetTransform("verRotationTransform");
        hitRayTrans = GetTransform("hitRaycastTransform");
        if (!hitRayTrans)
            hitRayTrans = transform;
        else
            hasRaycastTransform = true;
        Transform hornTrans = GetParticleTransform();
        if(hornTrans)
        {
            hornSystem = hornTrans.GetComponent<ParticleSystem>();
            if(hornSystem)
            {
                var emission = hornSystem.emission;
                emission.enabled = false;

                var main = hornSystem.main;
                if (main.startSpeed.mode == ParticleSystemCurveMode.Constant)
                    projectileSpeed = main.startSpeed.constant;
                if (main.gravityModifier.mode == ParticleSystemCurveMode.Constant)
                    gravity = main.gravityModifier.constant;

                properties.ParseFloat("projectileSpeed", ref projectileSpeed);
                properties.ParseFloat("gravity", ref gravity);
                var startSpeed = main.startSpeed;
                startSpeed = projectileSpeed;
                var gravityMod = main.gravityModifier;
                gravityMod = gravity;
                gravity *= Physics.gravity.y;
            }
        }
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        if (!hasOperator)
        {
            if (vehicle.entity.HasDriver && player && vehicle.entity.AttachedMainEntity.entityId == player.entityId)
            {
                OnPlayerEnter();
                CreatePreview();
            }
            else
                return;
        }

        if(!vehicle.entity.HasDriver)
        {
            OnPlayerDetach();
            DestroyPreview();
            return;
        }

        CalcCurRotation();

        if(Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f || Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x), false, -1, player.entityId);
            else if(SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x));
            lastHorRot = horRotTrans.localEulerAngles.y;
            lastVerRot = verRotTrans.localEulerAngles.x;
        }

        if (!isCoRunning && hornCooldown > 0)
            hornCooldown -= _dt;
    }

    public void NetSyncUpdate(float horRot, float verRot)
    {
        horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, horRot, horRotTrans.localEulerAngles.z);
        verRotTrans.localEulerAngles = new Vector3(verRot, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }

    public void DoHorn()
    {
        if(ammoValue.type > 0 && player.bag.GetItemCount(ammoValue) < burstRepeat)
        {
            vehicle.entity.PlayOneShot(hornEmptySound);
            return;
        }
        if(hornCooldown > 0)
        {
            vehicle.entity.PlayOneShot(hornNotReadySound);
            return;
        }

        if (burstInterval > 0)
            ThreadManager.StartCoroutine(DoHornCo());
        else
        {
            for(int i = 0; i < burstRepeat; ++i)
                DoHornServer(burstCount);
            vehicle.entity.PlayOneShot(hornReloadSound);
        }
        hornCooldown = hornInterval;
        return;
    }

    private IEnumerator DoHornCo()
    {
        isCoRunning = true;
        int curBurstCount = 0;
        while(curBurstCount < burstRepeat)
        {
            if (!hasOperator)
                yield break;
            DoHornServer(burstCount);
            ++curBurstCount;
            yield return new WaitForSeconds(burstInterval);
        }
        yield return null;
        vehicle.entity.PlayOneShot(hornReloadSound);
        isCoRunning = false;
        yield break;
    }

    public void DoHornServer(int count)
    {
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageHornWeaponFire>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x, count, seed));
        else
        {
            if(SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponFire>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x, count, seed));
            DoHornClient(count, seed);
        }
        UseHorn();
        if (ammoValue.type > 0)
            ConsumeAmmo(1);
    }

    protected virtual void ConsumeAmmo(int count)
    {
        player.bag.DecItem(ammoValue, count);
    }

    public virtual void DoHornClient(int count, uint seed)
    {
        if(hornSystem)
        {
            ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
            param.randomSeed = seed;
            hornSystem.Emit(param, count);
        }
    }

    protected float Angle(Vector3 target)
    {
        float distX = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(hornSystem.transform.position.x, hornSystem.transform.position.z));
        float distY = target.y - hornSystem.transform.position.y;
        float posBase = (gravity * Mathf.Pow(distX, 2.0f)) / (2.0f * Mathf.Pow(projectileSpeed, 2.0f));
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

    protected float AngleToLimited(float angle, float min, float max, out bool unchanged)
    {
        float res = Mathf.Min(max, Mathf.Max(min, angle));
        unchanged = res == angle;
        return res;
    }

    protected virtual void OnPlayerEnter()
    {
        hasOperator = true;
        initializer = hornSystem.gameObject.AddComponent<SubExplosionInitializer>();
        initializer.data = component.BoundExplosionData;
        initializer.entityAlive = vehicle.entity.AttachedMainEntity as EntityAlive;
        if (component.BoundItemClass != null)
            initializer.value = new ItemValue(component.BoundItemClass.Id);
        if (explodeOnDeath)
            initializer.SetExplodeOnDeath(explodeOnCollision);
    }

    protected virtual void OnPlayerDetach()
    {
        hasOperator = false;
        if (initializer)
        {
            GameObject.Destroy(initializer);
            initializer = null;
        }
    }

    protected virtual void CreatePreview()
    {
        if(previewScaleEntity > 0 && previewColorEntity.a > 0)
        {
            explPreviewTransEntity = GameObject.CreatePrimitive(previewTypeEntity).transform;
            explPreviewTransEntity.localScale *= previewScaleEntity;
            GameObject.Destroy(explPreviewTransEntity.GetComponent<Collider>());
            Material mat = explPreviewTransEntity.GetComponent<Renderer>().material;
            mat.SetColor("_Color", previewColorEntity);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
        if(previewScaleBlock > 0 && previewColorBlock.a > 0)
        {
            explPreviewTransBlock = GameObject.CreatePrimitive(previewTypeBlock).transform;
            explPreviewTransBlock.localScale *= previewScaleBlock;
            GameObject.Destroy(explPreviewTransBlock.GetComponent<Collider>());
            Material mat = explPreviewTransBlock.GetComponent<Renderer>().material;
            mat.SetColor("_Color", previewColorBlock);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
    }

    protected virtual void DestroyPreview()
    {
        if(explPreviewTransEntity != null)
        {
            GameObject.Destroy(explPreviewTransEntity.gameObject);
            explPreviewTransEntity = null;
        }
        if(explPreviewTransBlock != null)
        {
            GameObject.Destroy(explPreviewTransBlock.gameObject);
            explPreviewTransBlock = null;
        }
    }

    protected virtual void CalcCurRotation()
    {
        float curHorAngle = horRotTrans.localEulerAngles.y;
        float curVerAngle = verRotTrans.localEulerAngles.x;
        float targetHorAngle = curHorAngle;
        float targetVerAngle = curVerAngle;
        if (DoRaycast(out RaycastHit hitInfo))
        {
            DoCalcCurRotation(hitInfo, out Vector3 hitPos, out targetHorAngle, out targetVerAngle, out bool updatePreview);
            if(updatePreview)
            {
                if(explPreviewTransEntity != null)
                    explPreviewTransEntity.position = hitPos;
                if (explPreviewTransBlock != null)
                    explPreviewTransBlock.position = hitPos;
            }
        }

        if (targetHorAngle != curHorAngle)
            horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, targetHorAngle, horRotTrans.localEulerAngles.z);
        if (targetVerAngle != curVerAngle)
            verRotTrans.localEulerAngles = new Vector3(targetVerAngle, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }

    protected virtual bool DoRaycast(out RaycastHit hitInfo)
    {
        Ray lookRay = player.playerCamera.ScreenPointToRay(Input.mousePosition);
        lookRay.origin = hasRaycastTransform ? hitRayTrans.position : transform.position + Vector3.up * 2;
        return Physics.Raycast(lookRay, out hitInfo);
    }

    protected virtual void DoCalcCurRotation(RaycastHit hitInfo, out Vector3 hitPos, out float targetHorAngle, out float targetVerAngle, out bool updatePreview)
    {
        hitPos = hitInfo.point;
        Vector3 aimAt = Quaternion.LookRotation(hitPos - horRotTrans.position).eulerAngles;
        aimAt.x = Angle(hitPos);
        aimAt.x = -AngleToLimited(aimAt.x, verticleMinRotation, verticleMaxRotation, out updatePreview);
        aimAt = (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(aimAt)).eulerAngles;
        aimAt.y = AngleToInferior(aimAt.y);
        aimAt.y = AngleToLimited(aimAt.y, horizontalMinRotation, horizontalMaxRotation, out updatePreview);
        targetHorAngle = aimAt.y;
        targetVerAngle = aimAt.x;
    }

    protected virtual void UseHorn()
    {
        string hornSoundName = this.vehicle.GetHornSoundName();
        if (hornSoundName.Length > 0)
        {
            vehicle.entity.PlayOneShot(hornSoundName, false);
        }
    }
}

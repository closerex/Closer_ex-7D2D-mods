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
    protected float verticleRotSpeed = 360f;
    protected float horizontalMaxRotation = 180f;
    protected float horizontalMinRotation = -180f;
    protected float horizontalRotSpeed = 360f;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected bool isCoRunning = false;
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
    protected ItemValue ammoValue = ItemValue.None.Clone();
    protected bool lastOnTarget = false;
    protected bool fullCircleRotation = false;
    protected static readonly int colorId = Shader.PropertyToID("_Color");

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseInt("burstCount", ref burstCount);
        _properties.ParseFloat("burstInterval", ref burstInterval);
        _properties.ParseInt("burstRepeat", ref burstRepeat);
        _properties.ParseFloat("hornInterval", ref hornInterval);
        hornCooldown = 0;

        string str = null;
        _properties.ParseString("particleIndex", ref str);
        if (!string.IsNullOrEmpty(str))
            CustomParticleEffectLoader.GetCustomParticleComponents(PlatformIndependentHash.StringToUInt16(str), out component);
        _properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        _properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        str = null;
        _properties.ParseString("ammo", ref str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);

        _properties.ParseString("emptySound", ref hornEmptySound);
        _properties.ParseString("notReadySound", ref hornNotReadySound);
        _properties.ParseString("reloadSound", ref hornReloadSound);

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

        previewScaleEntity = component.BoundExplosionData.EntityRadius;
        previewScaleBlock = component.BoundExplosionData.BlockRadius;
        previewColorEntityOnTarget = Color.clear;
        previewColorEntityAiming = Color.clear;
        previewColorBlockOnTarget = Color.clear;
        previewColorBlockAiming = Color.clear;
        _properties.ParseFloat("previewScaleEntity", ref previewScaleEntity);
        _properties.ParseFloat("previewScaleBlock", ref previewScaleBlock);
        str = null;
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

        if (!isCoRunning && hornCooldown > 0)
            hornCooldown -= _dt;

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

        CalcCurRotation(_dt);

        if(Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f || Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x), false, -1, player.entityId);
            else if(SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
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

    public virtual void DoHorn()
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

    protected virtual IEnumerator DoHornCo()
    {
        isCoRunning = true;
        int curBurstCount = 0;
        while(curBurstCount < burstRepeat)
        {
            if (!hasOperator)
                break;
            DoHornServer(burstCount);
            ++curBurstCount;
            yield return new WaitForSeconds(burstInterval);
        }
        yield return null;
        vehicle.entity.PlayOneShot(hornReloadSound);
        isCoRunning = false;
        yield break;
    }

    protected virtual void DoHornServer(int count)
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
        hornCooldown = hornInterval;
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
        if(previewScaleEntity > 0 && previewColorEntityOnTarget.a > 0)
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
        if(previewScaleBlock > 0 && previewColorBlockOnTarget.a > 0)
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

    protected virtual void UpdatePreviewPos(Vector3 position)
    {
        if(explPreviewTransEntity != null)
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
        if(onTarget != lastOnTarget)
        {
            SetPreviewColor(onTarget);
            lastOnTarget = onTarget;
        }
    }

    protected bool FuzzyEqualAngle(float angle1, float angle2, float fuzzy)
    {
        return Mathf.Abs(angle1 - angle2) <= fuzzy;
    }

    protected virtual bool DoRaycast(out RaycastHit hitInfo)
    {
        Ray lookRay = player.playerCamera.ScreenPointToRay(Input.mousePosition);
        lookRay.origin = hasRaycastTransform ? hitRayTrans.position : hitRayTrans.position + Vector3.up * 2;
        return Physics.Raycast(lookRay, out hitInfo);
    }

    protected virtual void HorRotateTowards(float targetHorAngle, float _dt)
    {
        //targetHorAngle = AngleToLimited(targetHorAngle, horizontalMinRotation, horizontalMaxRotation);
        float maxRotPerUpdate = horizontalRotSpeed * _dt;
        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        float nextHorAngle;
        if(!fullCircleRotation)
            nextHorAngle = targetHorAngle > curHorAngle ? Mathf.Min(curHorAngle + maxRotPerUpdate, targetHorAngle) : Mathf.Max(curHorAngle - maxRotPerUpdate, targetHorAngle);
        else
        {
            if (targetHorAngle > 0 && curHorAngle < 0)
            {
                if (targetHorAngle - curHorAngle > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if(nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Max(nextHorAngle, targetHorAngle);
                }
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if(nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, targetHorAngle);
                }
            }
            else if (targetHorAngle < 0 && curHorAngle > 0)
            {
                if (curHorAngle - targetHorAngle > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if(nextHorAngle > 0 == targetHorAngle > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, targetHorAngle);
                }    
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if(nextHorAngle > 0 == targetHorAngle > 0)
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

    protected virtual void UseHorn()
    {
        string hornSoundName = this.vehicle.GetHornSoundName();
        if (hornSoundName.Length > 0)
        {
            vehicle.entity.PlayOneShot(hornSoundName, false);
        }
    }
    protected float Angle(Vector3 target)
    {
        float distX = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(hornSystem.transform.position.x, transform.position.z));
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

    protected float AngleToLimited(float angle, float min, float max)
    {
        float res = Mathf.Min(max, Mathf.Max(min, angle));
        return res;
    }
}

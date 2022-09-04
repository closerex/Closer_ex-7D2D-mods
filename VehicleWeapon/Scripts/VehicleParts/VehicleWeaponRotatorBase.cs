using UnityEngine;
public abstract class VehicleWeaponRotatorBase : VehicleWeaponPartBase
{
    protected Transform transform = null;
    protected Transform horRotTrans = null;
    protected Transform verRotTrans = null;
    protected Transform indicatorTrans;
    protected Transform leftHandbarTrans;
    protected Transform rightHandbarTrans;
    protected Color indicatorColorOnTarget;
    protected Color indicatorColorAiming;
    protected int indicatorColorProperty;
    protected float verticleMaxRotation;
    protected float verticleMinRotation;
    protected float verticleRotSpeed;
    protected float horizontalMaxRotation;
    protected float horizontalMinRotation;
    protected float horizontalRotSpeed;
    protected bool syncPlayerRotation = false;

    protected Quaternion leftHandbarInitialRotation;
    protected Quaternion rightHandbarInitialRotation;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected float nextHorRot = 0f;
    protected float nextVerRot = 0f;
    protected bool lastOnTarget = false;
    protected bool shouldUpdate = false;
    protected bool fullCircleRotation = false;
    protected EntityPlayerLocal player = null;
    protected VehicleWeaponBase weapon = null;

    protected Ray inputRay;
    protected bool varyingIndicatorColor = false;

    public virtual Transform HorRotTrans { get => horRotTrans; }
    public virtual Transform VerRotTrans { get => verRotTrans; }
    public Transform IndicatorTrans { get => indicatorTrans; }
    public bool OnTarget { get => lastOnTarget; }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        transform = GetTransform();
        horRotTrans = GetTransform("horRotationTransform");
        verRotTrans = GetTransform("verRotationTransform");
        _properties.ParseBool("syncPlayerRotation", ref syncPlayerRotation);
        leftHandbarTrans = GetTransform("leftHandbarTransform");
        if(leftHandbarTrans != null)
        {
            InitIKTarget(AvatarIKGoal.LeftHand, leftHandbarTrans);
            leftHandbarInitialRotation = leftHandbarTrans.localRotation;
        }
        rightHandbarTrans = GetTransform("rightHandbarTransform");
        if(rightHandbarTrans != null)
        {
            InitIKTarget(AvatarIKGoal.RightHand, rightHandbarTrans);
            rightHandbarInitialRotation = rightHandbarTrans.localRotation;
        }

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        verticleMaxRotation = 45f;
        properties.ParseFloat("verticleMaxRotation", ref verticleMaxRotation);
        verticleMaxRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "verticleMaxRotation", verticleMaxRotation.ToString()));
        verticleMaxRotation = AngleToInferior(verticleMaxRotation);

        verticleMinRotation = 0f;
        properties.ParseFloat("verticleMinRotation", ref verticleMinRotation);
        verticleMinRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "verticleMinRotation", verticleMinRotation.ToString()));
        verticleMinRotation = AngleToInferior(verticleMinRotation);

        verticleRotSpeed = 360f;
        properties.ParseFloat("verticleRotationSpeed", ref verticleRotSpeed);
        verticleRotSpeed = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "verticleRotSpeed", verticleRotSpeed.ToString()));
        verticleRotSpeed = Mathf.Abs(verticleRotSpeed);

        horizontalMaxRotation = 180f;
        properties.ParseFloat("horizontalMaxRotation", ref horizontalMaxRotation);
        horizontalMaxRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "horizontalMaxRotation", horizontalMaxRotation.ToString()));
        horizontalMaxRotation = AngleToInferior(horizontalMaxRotation);

        horizontalMinRotation = -180f;
        properties.ParseFloat("horizontalMinRotation", ref horizontalMinRotation);
        horizontalMinRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "horizontalMinRotation", horizontalMinRotation.ToString()));
        horizontalMinRotation = AngleToInferior(horizontalMinRotation);

        horizontalRotSpeed = 360f;
        properties.ParseFloat("horizontalRotationSpeed", ref horizontalRotSpeed);
        horizontalRotSpeed = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "horizontalRotSpeed", horizontalRotSpeed.ToString()));
        horizontalRotSpeed = Mathf.Abs(horizontalRotSpeed);
        fullCircleRotation = horizontalMaxRotation == 180f && horizontalMinRotation == -180f;

        indicatorTrans?.gameObject.SetActive(false);
        string str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "indicatorTransform", GetProperty("indicatorTransform"));
        if (!string.IsNullOrEmpty(str))
        {
            Transform mesh = vehicle.GetMeshTransform();
            indicatorTrans = mesh.Find(str);
        }

        str = null;
        indicatorColorOnTarget = Color.clear;
        properties.ParseString("indicatorColorOnTarget", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "indicatorColorOnTarget", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out indicatorColorOnTarget);

        str = null;
        indicatorColorAiming = Color.clear;
        properties.ParseString("indicatorColorAiming", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "indicatorColorAiming", str);
        if (!string.IsNullOrEmpty(str))
            ColorUtility.TryParseHtmlString(str, out indicatorColorAiming);

        str = null;
        properties.ParseString("indicatorColorProperty", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "indicatorColorProperty", str);
        indicatorColorProperty = Shader.PropertyToID(str);

        varyingIndicatorColor = indicatorColorOnTarget != Color.clear && indicatorColorAiming != Color.clear;
    }

    public virtual void SetWeapon(VehicleWeaponBase weapon)
    {
        this.weapon = weapon;
    }

    protected virtual void SetInputRay()
    {
        inputRay = weapon.LookRay;
    }

    public override void NoGUIUpdate(float _dt)
    {
        SetInputRay();
        CalcCurRotation(_dt);
    }

    public override void NoPauseUpdate(float _dt)
    {
        DoRotateTowards(_dt);

        shouldUpdate = (horRotTrans != null && Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f) || (verRotTrans != null && Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f);

        bool onTarget = IsOnTarget();
        if (onTarget != lastOnTarget)
        {
            SetPreviewColor(onTarget);
            lastOnTarget = onTarget;
        }
    }

    public override bool ShouldNetSyncUpdate()
    {
        return shouldUpdate;
    }

    protected internal virtual bool IsOnTarget()
    {
        return (horRotTrans == null || FuzzyEqualAngle(nextHorRot, AngleToInferior(horRotTrans.localEulerAngles.y), 1f)) && (verRotTrans == null || FuzzyEqualAngle(nextVerRot, AngleToInferior(verRotTrans.localEulerAngles.x), 0.5f));
    }

    public override void NetSyncWrite(PooledBinaryWriter _bw)
    {
        if (HorRotTrans != null)
        {
            lastHorRot = HorRotTrans.localEulerAngles.y;
            _bw.Write(lastHorRot);
        }
        else
            lastHorRot = 0;

        if (VerRotTrans != null)
        {
            lastVerRot = VerRotTrans.localEulerAngles.x;
            _bw.Write(lastVerRot);
        }
        else
            lastVerRot = 0;
    }

    public override void NetSyncRead(PooledBinaryReader _br)
    {
        if (HorRotTrans != null)
        {
            float horAngle = _br.ReadSingle();
            HorRotTrans.localEulerAngles = new Vector3(HorRotTrans.localEulerAngles.x, horAngle, HorRotTrans.localEulerAngles.z);
            if (syncPlayerRotation)
            {
                var entity = vehicle.entity.GetAttached(weapon.Seat);
                if(entity != null)
                {
                    Vector3 proj = Vector3.ProjectOnPlane(entity.ModelTransform.forward, transform.up);
                    float angle = Vector3.SignedAngle(transform.forward, proj, transform.up);
                    player.ModelTransform.RotateAround(HorRotTrans.position, transform.up, horAngle - angle);
                }
            }
        }

        if (VerRotTrans != null)
        {
            float verAngle = _br.ReadSingle();
            VerRotTrans.localEulerAngles = new Vector3(verAngle, VerRotTrans.localEulerAngles.y, VerRotTrans.localEulerAngles.z);

            if (leftHandbarTrans != null)
                leftHandbarTrans.localRotation = leftHandbarInitialRotation * Quaternion.AngleAxis(-verAngle, VerRotTrans.right);
            if (rightHandbarTrans != null)
                rightHandbarTrans.localRotation = rightHandbarInitialRotation * Quaternion.AngleAxis(-verAngle, VerRotTrans.right);
        }
    }

    protected abstract void CalcCurRotation(float _dt);

    public virtual void ForceUpdate(float _dt)
    {
        Log.Out($"next hor angle: {nextHorRot}, next ver angle: {nextVerRot}");
        DoRotateTowards(_dt, true);
    }

    protected virtual bool DoRotateTowards(float _dt, bool forced = false)
    {
        bool rotated = DoRotateTowardsHor(_dt, forced) | DoRotateTowardsVer(_dt, forced);
        return rotated;
    }

    protected bool DoRotateTowardsHor(float _dt, bool forced = false)
    {
        if (horRotTrans == null)
            return false;

        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        if (forced || !FuzzyEqualAngle(curHorAngle, nextHorRot, 0.01f))
        {
            HorRotateTowards(_dt);
            return true;
        }
        return false;
    }

    protected bool DoRotateTowardsVer(float _dt, bool forced = false)
    {
        if (verRotTrans == null)
            return false;

        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        if (forced || !FuzzyEqualAngle(curVerAngle, nextVerRot, 0.01f))
        {
            VerRotateTowards(_dt);
            return true;
        }
        return false;
    }

    protected virtual void HorRotateTowards(float _dt)
    {
        //targetHorAngle = AngleToLimited(targetHorAngle, horizontalMinRotation, horizontalMaxRotation);
        float maxRotPerUpdate = horizontalRotSpeed * _dt;
        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        float nextHorAngle;
        if (!fullCircleRotation)
            nextHorAngle = nextHorRot > curHorAngle ? Mathf.Min(curHorAngle + maxRotPerUpdate, nextHorRot) : Mathf.Max(curHorAngle - maxRotPerUpdate, nextHorRot);
        else
        {
            if (nextHorRot > 0 && curHorAngle < 0)
            {
                if (nextHorRot - curHorAngle > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if (nextHorAngle > 0 == nextHorRot > 0)
                        nextHorAngle = Mathf.Max(nextHorAngle, nextHorRot);
                }
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if (nextHorAngle > 0 == nextHorRot > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, nextHorRot);
                }
            }
            else if (nextHorRot < 0 && curHorAngle > 0)
            {
                if (curHorAngle - nextHorRot > 180)
                {
                    nextHorAngle = AngleToInferior(curHorAngle + maxRotPerUpdate);
                    if (nextHorAngle > 0 == nextHorRot > 0)
                        nextHorAngle = Mathf.Min(nextHorAngle, nextHorRot);
                }
                else
                {
                    nextHorAngle = AngleToInferior(curHorAngle - maxRotPerUpdate);
                    if (nextHorAngle > 0 == nextHorRot > 0)
                        nextHorAngle = Mathf.Max(nextHorAngle, nextHorRot);
                }
            }
            else
                nextHorAngle = nextHorRot > curHorAngle ? Mathf.Min(curHorAngle + maxRotPerUpdate, nextHorRot) : Mathf.Max(curHorAngle - maxRotPerUpdate, nextHorRot);
        }
        horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, nextHorAngle, horRotTrans.localEulerAngles.z);
        if(syncPlayerRotation)
        {
            Vector3 proj = Vector3.ProjectOnPlane(player.ModelTransform.forward, transform.up);
            float angle = Vector3.SignedAngle(transform.forward, proj, transform.up);
            player.ModelTransform.RotateAround(horRotTrans.position, transform.up, nextHorAngle - angle);
            if (_dt == 0)
                Log.Out($"angle:{angle}, next angle:{nextHorAngle}");
        }
    }

    protected virtual void VerRotateTowards(float _dt)
    {
        //targetVerAngle = AngleToLimited(targetVerAngle, verticleMinRotation, verticleMaxRotation);
        float maxRotPerUpdate = verticleRotSpeed * _dt;
        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        float nextVerAngle = nextVerRot > curVerAngle ? Mathf.Min(curVerAngle + maxRotPerUpdate, nextVerRot) : Mathf.Max(curVerAngle - maxRotPerUpdate, nextVerRot);
        verRotTrans.localEulerAngles = new Vector3(nextVerAngle, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
        if (leftHandbarTrans != null)
            leftHandbarTrans.localRotation = leftHandbarInitialRotation * Quaternion.AngleAxis(-nextVerAngle, verRotTrans.right);
        if (rightHandbarTrans != null)
            rightHandbarTrans.localRotation = rightHandbarInitialRotation * Quaternion.AngleAxis(-nextVerAngle, verRotTrans.right);
    }

    public virtual void CreatePreview()
    {
        DestroyPreview();
        if (indicatorTrans != null)
        {
            indicatorTrans.gameObject.SetActive(true);
            IndicatorTrans.GetComponent<Renderer>().material.SetColor(indicatorColorProperty, indicatorColorAiming);
        }
    }

    public virtual void DestroyPreview()
    {
        if (indicatorTrans != null)
            indicatorTrans.gameObject.SetActive(false);
    }

    protected virtual void UpdatePreviewPos(Vector3 position)
    {
    }

    protected virtual void SetPreviewColor(bool onTarget)
    {
        if (varyingIndicatorColor && indicatorTrans != null)
            indicatorTrans.GetComponent<Renderer>().material.SetColor(indicatorColorProperty, onTarget ? indicatorColorOnTarget : indicatorColorAiming);
    }

    protected bool FuzzyEqualAngle(float angle1, float angle2, float fuzzy)
    {
        return Mathf.Abs(angle1 - angle2) <= fuzzy;
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


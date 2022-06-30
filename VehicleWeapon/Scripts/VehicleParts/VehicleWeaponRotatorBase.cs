using UnityEngine;
public class VehicleWeaponRotatorBase : VehicleWeaponPartBase
{
    protected Transform transform = null;
    protected Transform horRotTrans = null;
    protected Transform verRotTrans = null;
    protected Transform indicatorTrans;
    protected float verticleMaxRotation = 45f;
    protected float verticleMinRotation = 0f;
    protected float verticleRotSpeed = 360f;
    protected float horizontalMaxRotation = 180f;
    protected float horizontalMinRotation = -180f;
    protected float horizontalRotSpeed = 360f;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected float nextHorRot = 0f;
    protected float nextVerRot = 0f;
    protected bool lastOnTarget = false;
    protected bool fullCircleRotation = false;
    protected EntityPlayerLocal player = null;
    protected VehicleWeaponBase weapon = null;
    protected static int dynamicScaleMode = 0;
    protected static float dynamicScaleOverride = 1;

    public virtual Transform HorRotTrans { get => horRotTrans; }
    public virtual Transform VerRotTrans { get => verRotTrans; }
    public bool OnTarget { get => lastOnTarget; }

    public static void OnVideoSettingChanged()
    {
        dynamicScaleMode = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxDynamicMode);
        if (dynamicScaleMode == 2)
            dynamicScaleOverride = GamePrefs.GetFloat(EnumGamePrefs.OptionsGfxDynamicScale);
    }

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

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        string name = GetModName();
        verticleMaxRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "verticleMaxRotation", verticleMaxRotation.ToString()));
        verticleMinRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "verticleMinRotation", verticleMinRotation.ToString()));
        verticleRotSpeed = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "verticleRotSpeed", verticleRotSpeed.ToString()));
        horizontalMaxRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "horizontalMaxRotation", horizontalMaxRotation.ToString()));
        horizontalMinRotation = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "horizontalMinRotation", horizontalMinRotation.ToString()));
        horizontalRotSpeed = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "horizontalRotSpeed", horizontalRotSpeed.ToString()));
        fullCircleRotation = horizontalMaxRotation == 180f && horizontalMinRotation == -180f;

        indicatorTrans?.gameObject.SetActive(false);
        string str = vehicleValue.GetVehicleWeaponPropertyOverride(name, "indicatorTransform", GetProperty("indicatorTransform"));
        if (!string.IsNullOrEmpty(str))
        {
            Transform mesh = vehicle.GetMeshTransform();
            indicatorTrans = mesh.Find(str);
        }
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();

        transform = GetTransform();
        horRotTrans = GetTransform("horRotationTransform");
        verRotTrans = GetTransform("verRotationTransform");
        indicatorTrans = GetTransform("indicatorTransform");
    }

    public virtual void SetWeapon(VehicleWeaponBase weapon)
    {
        this.weapon = weapon;
    }

    public override void NoGUIUpdate(float _dt)
    {
        CalcCurRotation(_dt);
    }

    public override void NoPauseUpdate(float _dt)
    {
        DoRotateTowards(_dt);

        if ((horRotTrans != null && Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f) || (verRotTrans != null && Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f))
        {
            lastHorRot = horRotTrans != null ? horRotTrans.localEulerAngles.y : 0;
            lastVerRot = verRotTrans != null ? verRotTrans.localEulerAngles.x : 0;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageParticleWeaponUpdate>().Setup(vehicle.entity.entityId, lastHorRot, lastVerRot, weapon.Seat, weapon.Slot), false, -1, player.entityId);
            else if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageParticleWeaponUpdate>().Setup(vehicle.entity.entityId, lastHorRot, lastVerRot, weapon.Seat, weapon.Slot));
        }

        bool onTarget = (horRotTrans == null || FuzzyEqualAngle(nextHorRot, AngleToInferior(horRotTrans.localEulerAngles.y), 1f)) && (verRotTrans == null || FuzzyEqualAngle(nextVerRot, AngleToInferior(verRotTrans.localEulerAngles.x), 0.5f));
        if (onTarget != lastOnTarget)
        {
            SetPreviewColor(onTarget);
            lastOnTarget = onTarget;
        }
    }

    public void NetSyncUpdate(float horRot, float verRot)
    {
        if(horRotTrans != null)
            horRotTrans.localEulerAngles = new Vector3(horRotTrans.localEulerAngles.x, horRot, horRotTrans.localEulerAngles.z);
        if(verRotTrans != null)
            verRotTrans.localEulerAngles = new Vector3(verRot, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }

    protected virtual void CalcCurRotation(float _dt)
    {
    }

    protected virtual bool DoRotateTowards(float _dt)
    {
        bool updatePreview = DoRotateTowardsHor(_dt) | DoRotateTowardsVer(_dt);
        return updatePreview;
    }

    protected bool DoRotateTowardsHor(float _dt)
    {
        if (horRotTrans == null)
            return false;

        float curHorAngle = AngleToInferior(horRotTrans.localEulerAngles.y);
        if (!FuzzyEqualAngle(curHorAngle, nextHorRot, 0.01f))
        {
            HorRotateTowards(_dt);
            return true;
        }
        return false;
    }

    protected bool DoRotateTowardsVer(float _dt)
    {
        if (verRotTrans == null)
            return false;

        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        if (!FuzzyEqualAngle(curVerAngle, nextVerRot, 0.01f))
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
    }

    protected virtual void VerRotateTowards(float _dt)
    {
        //targetVerAngle = AngleToLimited(targetVerAngle, verticleMinRotation, verticleMaxRotation);
        float maxRotPerUpdate = verticleRotSpeed * _dt;
        float curVerAngle = AngleToInferior(verRotTrans.localEulerAngles.x);
        float nextVerAngle = nextVerRot > curVerAngle ? Mathf.Min(curVerAngle + maxRotPerUpdate, nextVerRot) : Mathf.Max(curVerAngle - maxRotPerUpdate, nextVerRot);
        verRotTrans.localEulerAngles = new Vector3(nextVerAngle, verRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.z);
    }
    public virtual void CreatePreview()
    {
        if (indicatorTrans != null)
            indicatorTrans.gameObject.SetActive(true);
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

    protected Vector3 GetDynamicMousePosition()
    {
        Vector3 dynamicMousePos;

        if (!GameRenderManager.dynamicIsEnabled)
            dynamicMousePos = Input.mousePosition;
        else
        {
            float scale;
            if (dynamicScaleMode == 1)
                scale = (float)player.renderManager.GetDynamicRenderTexture().width / Screen.width;
            else
                scale = dynamicScaleOverride;
            dynamicMousePos = Input.mousePosition * scale;
        }

        return dynamicMousePos;
    }
}


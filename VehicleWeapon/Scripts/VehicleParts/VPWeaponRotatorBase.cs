using UnityEngine;
public class VPWeaponRotatorBase : VehiclePart
{
    protected Transform transform = null;
    protected Transform horRotTrans = null;
    protected Transform verRotTrans = null;
    protected Transform hitRayTrans = null;
    protected bool hasRaycastTransform = false;
    protected float verticleMaxRotation = 45f;
    protected float verticleMinRotation = 0f;
    protected float verticleRotSpeed = 360f;
    protected float horizontalMaxRotation = 180f;
    protected float horizontalMinRotation = -180f;
    protected float horizontalRotSpeed = 360f;
    protected float lastHorRot = 0f;
    protected float lastVerRot = 0f;
    protected bool lastOnTarget = false;
    protected bool fullCircleRotation = false;
    protected EntityPlayerLocal player = null;
    protected int seat = 0;
    protected int slot = -1;
    protected VPWeaponBase weapon = null;

    public Transform HorRotTrans { get => horRotTrans; }
    public Transform VerRotTrans { get => verRotTrans; }
    public int Seat { get => seat; }
    public bool OnTarget { get => lastOnTarget; }

    ~VPWeaponRotatorBase()
    {
        DestroyPreview();
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

        _properties.ParseInt("seat", ref seat);
        if (seat < 0)
        {
            Log.Error("seat can not be less than 0! setting to 0...");
            seat = 0;
        }

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
    }

    public void SetSlot(int slot)
    {
        this.slot = slot;
    }

    public virtual void SetWeapon(VPWeaponBase weapon)
    {
        this.weapon = weapon;
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (weapon == null || !weapon.HasOperator)
            return;
        CalcCurRotation(_dt);

        if ((horRotTrans != null && Mathf.Abs(lastHorRot - horRotTrans.localEulerAngles.y) > 1f) || (verRotTrans != null && Mathf.Abs(lastVerRot - verRotTrans.localEulerAngles.x) > 1f))
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x, seat, slot), false, -1, player.entityId);
            else if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageHornWeaponUpdate>().Setup(vehicle.entity.entityId, horRotTrans.localEulerAngles.y, verRotTrans.localEulerAngles.x, seat, slot));
            lastHorRot = horRotTrans.localEulerAngles.y;
            lastVerRot = verRotTrans.localEulerAngles.x;
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
    }

    public virtual void DestroyPreview()
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
}


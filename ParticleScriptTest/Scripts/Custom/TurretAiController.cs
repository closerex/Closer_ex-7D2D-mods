using System.Collections.Generic;
using UnityEngine;

public class TurretAiController : ReverseTrackedBehaviour<TurretAiController>
{
    private TurretFiring turret;
    private float range;
    private float deadzone;
    private float verticleMaxRotation;
    private TurretTargetSorter sorter;
    private int entityid;
    private EntityAlive initiator;
    private EntityAlive target;
    private Transform joint;
    private Transform verRotTrans;
    private Transform horRotTrans;
    private float delayScan = 0f;
    private float projectileSpeed;
    private float gravity;
    private bool belongToPlayer;
    private Vector3i chunkPos;
    private Rigidbody rg;
    private bool useGravity = false;
    private long cKey;
    private int clrIdx;
    private Vector3 lastPos;
    protected override void Awake()
    {
        turret = GetComponent<TurretFiring>();
        CustomParticleComponents component = CustomParticleEffectLoader.LastInitializedComponent;
        entityid = component.CurrentExplosionParams._playerId;
        clrIdx = component.CurrentExplosionParams._clrIdx;
        Vector3i blockPos = component.CurrentExplosionParams._blockPos;
        cKey = WorldChunkCache.MakeChunkKey(World.toChunkXZ(blockPos.x), World.toChunkXZ(blockPos.z), clrIdx);
        GameManager.Instance.World.ChunkClusters[clrIdx].OnChunkVisibleDelegates += OnChunkVisibleChanged;
        chunkPos = new Vector3i(World.toChunkXZ(blockPos.x), 0, World.toChunkXZ(blockPos.z));
        //Log.Out("chunkPos: " + chunkPos + " clrIdx: " + clrIdx);
        initiator = GameManager.Instance.World.GetEntity(entityid) as EntityAlive;
        belongToPlayer = initiator is EntityPlayer;
        if (!turret || !initiator)
        {
            Destroy(gameObject);
            return;
        }
        key = entityid;
        track = true;
        handleClientInfo = true;
        base.Awake();

        joint = turret.launcher.transform;
        range = turret.range;
        deadzone = turret.deadzone;
        verticleMaxRotation = turret.verticleMaxRotation;
        projectileSpeed = turret.launcher.main.startSpeed.constant;
        gravity = turret.launcher.main.gravityModifier.constant * Physics.gravity.y;
        horRotTrans = turret.horRotationTrans;
        verRotTrans = turret.verRotationTrans;
        rg = GetComponent<Rigidbody>();
        if(rg)
        {
            if (!isServer)
            {
                rg.detectCollisions = false;
                rg.useGravity = false;
                return;
            }
            useGravity = rg.useGravity;
        }
        if(useGravity)
        {
            Chunk chunk = GameManager.Instance.World.ChunkClusters[clrIdx].GetChunkSync(cKey);
            rg.useGravity = chunk.GetAvailable();
        }
        lastPos = transform.position;

        sorter = new TurretTargetSorter(joint, projectileSpeed, turret.horizontalRotationSpeed);
        sorter.selfDirection = Quaternion.identity;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if(GameManager.Instance.World != null)
            GameManager.Instance.World.ChunkClusters[clrIdx].OnChunkVisibleDelegates -= OnChunkVisibleChanged;
    }

    void OnChunkVisibleChanged(long key, bool flag)
    {
        //Log.Out("chunk visible changed: chunk key " + key + "current key " + cKey);
        if (useGravity && key == cKey)
        {
            //rg.useGravity = flag;
            rg.isKinematic = !flag;
            //rg.detectCollisions = flag;
        }
    }

    void FixedUpdate()
    {
        if (turret.IsDestroying || turret.IsActivating)
            return;

        if (target && !target.IsAlive())
            target = null;

        if (isServer)
        {
            bool hasClient = SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0;

            if(!initiator && !belongToPlayer)
            {
                Log.Out("Turret owner does not exist, destroying: " + (int)key);
                NetSyncDestroy(hasClient);
                return;
            }else if(initiator)
            {
                Vector3 entityPos = initiator.GetPosition() - Origin.position;
                Vector2 distance = new Vector2(entityPos.x - transform.position.x, entityPos.z - transform.position.z);
                //Log.Out(transform.position.ToString());
                //Vector3 distance = initiator.GetPosition() - Origin.position - transform.position;
                if(initiator.IsDead() || distance.sqrMagnitude > turret.SqrDeactivateRange)
                {
                    Log.Out("Turret owner is dead or out of range, destroying: " + (int)key + " turret position: " + transform.position + " owner position: " + initiator.GetPosition());
                    NetSyncDestroy(hasClient);
                    return;
                }
            }

            if (delayScan > 0)
                delayScan -= Time.fixedDeltaTime;

            if (delayScan <= 0 && !IsTargetValid(out Vector3 direction))
                FindTarget(direction);

            if (CalcNextAngle() && !turret.IsReloading)
            {
                turret.FireShot();
                if(hasClient)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMyTurretSyncFireShot>().Setup(explId, entityid, transform.position, transform.rotation, turret.CurrentHorAngle, turret.CurrentVerAngle, turret.ammoCount));
                }
            }else if((transform.position - lastPos).sqrMagnitude >= 0.004f)
            {
                if(hasClient)
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMyTurretSyncUpdate>().Setup(explId, entityid, transform.position, transform.rotation));
                lastPos = transform.position;
            }
        }else
            CalcNextAngle();
    }

    void FindTarget(Vector3 cur_dir)
    {
        sorter.selfDirection = Quaternion.Euler(0, Quaternion.LookRotation(cur_dir).eulerAngles.y, 0);
        List<Entity> list_entities = GameManager.Instance.World.GetEntitiesInBounds(typeof(EntityAlive), new Bounds(transform.position + Origin.position, Vector3.one * (range * 2f + 1f)), new List<Entity>());
        if(list_entities.Count <= 0)
        {
            delayScan = 1f;
            return;
        }
        bool foundTarget = false;
        list_entities.Sort(sorter);
        foreach(Entity entity in list_entities)
        {
            if (!ValidateTarget(entity))
                continue;
            target = entity as EntityAlive;
            if(IsTargetValid(out Vector3 dir))
            {
                foundTarget = true;
                break;
            }
        }

        if (!foundTarget)
        {
            target = null;
            delayScan = 1f;
        } else if (SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMyTurretSyncTarget>().Setup(explId, entityid, transform.position, transform.rotation, turret.CurrentHorAngle, turret.CurrentVerAngle, target.entityId));
    }

    public void NetSyncTarget(Vector3 position, float horRot, float verRot, int target, Quaternion rotation)
    {
        turret.forceAdjust(position, horRot, verRot, rotation);
        this.target = GameManager.Instance.World.GetEntity(target) as EntityAlive;
    }

    public void NetSyncFireShot(Vector3 position, float horRot, float verRot, int ammoleft, Quaternion rotation)
    {
        turret.forceAdjust(position, horRot, verRot, rotation);
        turret.NetFireShot(ammoleft);
    }

    public void NetSyncUpdate(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }

    public void NetSyncDestroy(bool hasClient = false)
    {
        if (isServer && hasClient)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageMyTurretSyncDestroy>().Setup(explId, entityid));
        turret.Destroy();
    }

    protected override void OnHandleClientInfo(ClientInfo info)
    {
        if (belongToPlayer)
        {
            if(entityid == info.entityId)
            {
                initiator = GameManager.Instance.World.GetEntity(entityid) as EntityAlive;
                if (initiator != null)
                    Log.Out("Turret owner reconnected: " + (initiator as EntityPlayer).entityId);
            }
        }
    }

    bool CalcNextAngle()
    {
        bool isOnIdealAngle = false;
        bool hasTarget = IsTargetValid(out Vector3 direction);
        float nextHorRotation;
        float nextVerRotation;
        if (hasTarget)
        {
            Vector3 radius = Vector3.ProjectOnPlane(joint.position - verRotTrans.position, transform.up);
            Vector3 aimAt = Quaternion.LookRotation(direction).eulerAngles;
            aimAt.x = -Angle(target.GetPosition() - Origin.position - Vector3.up * 0.5f);
            aimAt = (Quaternion.Inverse(transform.rotation) * Quaternion.Euler(aimAt)).eulerAngles;
            nextHorRotation = aimAt.y;
            nextVerRotation = aimAt.x + turret.StartVerOffset;
            isOnIdealAngle = Mathf.Abs(nextHorRotation - horRotTrans.localEulerAngles.y) <= 4
                          && Mathf.Abs(nextVerRotation - verRotTrans.localEulerAngles.z) <= 1;
        }else
        {
            nextHorRotation = horRotTrans.localEulerAngles.y + turret.horizontalRotationSpeed;
            nextVerRotation = turret.StartVerOffset;
        }
        turret.SetNextAngle(nextHorRotation, nextVerRotation, hasTarget);

        return isOnIdealAngle;
    }

    public bool IsTargetValid(out Vector3 direction)
    {
        if (!target || !target.IsAlive())
        {
            direction = horRotTrans.forward;
            return false;
        }

        direction = target.GetPosition() - horRotTrans.position - Origin.position;
        return direction.x * direction.x + direction.z * direction.z <= range * range
            && direction.x * direction.x + direction.z * direction.z > deadzone * deadzone;
    }
    float Angle(Vector3 target)
    {
        float angleX;
        float distX = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(joint.position.x, joint.position.z));
        float distY = target.y - joint.position.y;
        float posBase = (gravity * Mathf.Pow(distX, 2.0f)) / (2.0f * Mathf.Pow(projectileSpeed, 2.0f));
        float posX = distX / posBase;
        float posY = (Mathf.Pow(posX, 2.0f) / 4.0f) - ((posBase - distY) / posBase);
        if (posY >= 0.0f)
        {
            angleX = Mathf.Rad2Deg * Mathf.Atan(-posX / 2.0f - Mathf.Pow(posY, 0.5f));
        }
        else
        {
            angleX = 45.0f;
        }
        return Mathf.Min(angleX, verticleMaxRotation);
    }

    bool ValidateTarget(Entity other)
    {
        if (!(other is EntityAlive _other) || !_other.IsAlive())
            return false;
        if ((initiator as EntityEnemy != null && _other as EntityPlayer != null)
            ||(initiator as EntityPlayer != null && _other as EntityEnemy != null)
            ||(initiator as EntityEnemy != null && _other as EntityNPC != null)
            ||(initiator as EntityNPC != null && _other as EntityEnemy != null))
            return true;
        if (FactionManager.Instance != null)
        {
            if (initiator as EntityPlayer != null && _other as EntityPlayer != null)
            {
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Hate)
                {
                    return true;
                }
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Dislike)
                {
                    return true;
                }
            }
            if (initiator as EntityPlayer != null && _other as EntityNPC != null)
            {
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Hate)
                {
                    return true;
                }
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Dislike)
                {
                    return true;
                }
            }
            if (initiator as EntityNPC != null && _other as EntityPlayer != null)
            {
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Hate)
                {
                    return true;
                }
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Dislike)
                {
                    return true;
                }
            }
            if (initiator as EntityNPC != null && _other as EntityNPC != null)
            {
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Hate)
                {
                    return true;
                }
                if (FactionManager.Instance.GetRelationshipTier(initiator, _other) == FactionManager.Relationship.Dislike)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private class TurretTargetSorter : IComparer<Entity>
    {
        public Quaternion selfDirection;
        private Transform selfTransform;
        private float projectileVelocity;
        private float rotationSpeed;

        public TurretTargetSorter(Transform trans, float velocity, float angular)
        {
            selfTransform = trans;
            projectileVelocity = velocity;
            rotationSpeed = angular;
        }
        public int Compare(Entity entity1, Entity entity2)
        {
            Vector3 dir1 = entity1.GetPosition() - selfTransform.position - Origin.position;
            Vector3 dir2 = entity2.GetPosition() - selfTransform.position - Origin.position;
            float angle1 = Mathf.Abs(Quaternion.Angle(Quaternion.Euler(0, Quaternion.LookRotation(dir1).eulerAngles.y, 0), selfDirection));
            float angle2 = Mathf.Abs(Quaternion.Angle(Quaternion.Euler(0, Quaternion.LookRotation(dir2).eulerAngles.y, 0), selfDirection));
            float time1 = dir1.magnitude / projectileVelocity + angle1 / rotationSpeed;
            float time2 = dir2.magnitude / projectileVelocity + angle2 / rotationSpeed;
            return (int)Mathf.Sign(time1 - time2);
        }
    }
}

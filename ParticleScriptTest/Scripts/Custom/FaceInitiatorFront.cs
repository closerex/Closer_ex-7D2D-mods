using UnityEngine;

class FaceInitiatorFront : TrackedBehaviourBase
{
    protected override void Awake()
    {
        int entityId = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._playerId;
        EntityAlive entityAlive = GameManager.Instance.World.GetEntity(entityId) as EntityAlive;
        if (entityAlive != null)
        {
            syncOnInit = true;
            base.Awake();
            if (isServer)
            {
                Vector3 dir = entityAlive.GetForwardVector();
                dir.y = 0;
                transform.forward = dir.normalized;
            }
        }else
            Destroy(gameObject);
    }

    protected override void OnExplosionInitServer(PooledBinaryWriter _bw)
    {
        StreamUtilsCompressed.Write(_bw, transform.forward);
    }

    protected override void OnExplosionInitClient(PooledBinaryReader _br)
    {
        transform.forward = StreamUtilsCompressed.ReadHalfVector3(_br);
    }
}


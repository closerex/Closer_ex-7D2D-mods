using UnityEngine;

public class FaceExplosionPosition : TrackedBehaviourBase
{
    protected override void Awake()
    {
        int playerid = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._playerId;
        EntityAlive player = GameManager.Instance.World.GetEntity(playerid) as EntityAlive;
        if (player != null)
        {
            syncOnInit = true;
            base.Awake();
            if(isServer)
            {
                Vector3 position = player.GetPosition();
                Vector3 dir = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._worldPos - position;
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


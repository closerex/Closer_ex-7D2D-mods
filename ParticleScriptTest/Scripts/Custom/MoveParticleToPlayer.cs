using UnityEngine;

public class MoveParticleToPlayer : TrackedBehaviourBase
{
    protected override void Awake()
    {
        int playerid = CustomParticleEffectLoader.LastInitializedComponent.CurrentExplosionParams._playerId;
        EntityAlive player = GameManager.Instance.World.GetEntity(playerid) as EntityAlive;
        if (player != null)
        {
            syncOnInit = true;
            base.Awake();
            if(isServer)
                transform.position = player.getHeadPosition() + Vector3.up - Origin.position;
        }
        else
            Destroy(gameObject);
    }

    protected override void OnExplosionInitServer(PooledBinaryWriter _bw)
    {
        StreamUtils.Write(_bw, transform.position + Origin.position);
        Log.Out(transform.position.ToString());
    }

    protected override void OnExplosionInitClient(PooledBinaryReader _br)
    {
        transform.position = StreamUtils.ReadVector3(_br) - Origin.position;
        Log.Out(transform.position.ToString());
    }
}


using System;
using System.Collections.Generic;
using UnityEngine;

public class AutoRemove : TrackedBehaviourBase
{
    public float lifetime = -1;

    protected override void Awake()
    {
        CustomParticleComponents component = CustomParticleEffectLoader.LastInitializedComponent;
        lifetime = component.CurrentExplosionParams._explosionData.Duration;
        if(lifetime > 0)
            syncOnConnect = component.SyncOnConnect;
        base.Awake();
        if (lifetime > 0)
            Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if(lifetime > 0)
            lifetime -= Time.deltaTime;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CustomParticleEffectLoader.removeInitializedParticle(gameObject);
    }

    protected override void OnClientConnected(PooledBinaryWriter _bw)
    {
        _bw.Write(lifetime);
    }

    protected override void OnConnectedToServer(PooledBinaryReader _br)
    {
        lifetime = _br.ReadSingle();
        if (lifetime > 0)
            Destroy(gameObject, lifetime);
    }
}


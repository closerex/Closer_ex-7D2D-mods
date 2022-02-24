using System;
using System.Collections.Generic;
using UnityEngine;

class AutoRemove : MonoBehaviour
{
    private void Start()
    {
        if(lifetime >= 0)
            Destroy(gameObject, lifetime);
    }

    private void OnDestroy()
    {
        //Destroy(gameObject);
        CustomParticleEffectLoader.removeInitializedParticle(this.gameObject);
    }

    public float lifetime = -1;
}


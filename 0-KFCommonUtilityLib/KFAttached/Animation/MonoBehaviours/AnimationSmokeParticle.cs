using UnityEngine;

public class AnimationSmokeParticle : MonoBehaviour
{
    private ParticleSystem ps;

    private void Awake()
    {
        if (!TryGetComponent(out ps))
            Destroy(this);
    }

    private void OnEnable()
    {
        ps.Clear(true);
    }
}

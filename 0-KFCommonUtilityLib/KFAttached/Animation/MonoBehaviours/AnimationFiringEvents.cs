using UnityEngine;

public class AnimationFiringEvents : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem[] mainParticles;

    private void Awake()
    {
        if (mainParticles == null)
            return;
        foreach (var ps in mainParticles)
        {
            ps.gameObject.SetActive(true);
            var emission = ps.emission;
            emission.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
        }
    }

    public void Fire(int index)
    {
        if (mainParticles == null || index < 0 || index >= mainParticles.Length)
            return;
        GameObject root = mainParticles[index].gameObject;
        root.BroadcastMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
        mainParticles[index].Emit(1);
    }
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class RFX4_PerPlatformSettings : MonoBehaviour
{
    public bool DisableOnMobiles;
    public bool RenderMobileDistortion;
    [Range(0.1f, 1)] public float ParticleBudgetForMobiles = 0.5f;
    // Use this for initialization
    private bool isMobile;

        void Awake()
    {
        isMobile = IsMobilePlatform();
        if (isMobile)
        {
            if (DisableOnMobiles) gameObject.SetActive(false);
            else
            {
                if(ParticleBudgetForMobiles < 0.99f) ChangeParticlesBudget(ParticleBudgetForMobiles);
            }
        }
    }

    void OnEnable()
    {
        var cam = Camera.main;
        Legacy_Rendering_Check(cam);
    }

    void Update()
    {
        var cam = Camera.main;
        Legacy_Rendering_Check(cam);
    }

    void Legacy_Rendering_Check(Camera cam)
    {

        if (cam == null) return;
        if (RenderMobileDistortion && !DisableOnMobiles && isMobile)
        {
            var mobileDistortion = cam.GetComponent<RFX4_MobileDistortion>();
            if (mobileDistortion == null) mobileDistortion = cam.gameObject.AddComponent<RFX4_MobileDistortion>();
            mobileDistortion.IsActive = true;

        }

    }

    void OnDisable()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (RenderMobileDistortion && !DisableOnMobiles && isMobile)
        {

            var mobileDistortion = cam.GetComponent<RFX4_MobileDistortion>();
            if (mobileDistortion != null) mobileDistortion.IsActive = false;
        }

    }

    bool IsMobilePlatform()
    {
        bool isMobile = false;
#if UNITY_EDITOR
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
            || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
            || EditorUserBuildSettings.activeBuildTarget == BuildTarget.WSAPlayer)
            isMobile = true;
#endif
        if (Application.isMobilePlatform) isMobile = true;
        return isMobile;
    }

    void ChangeParticlesBudget(float particlesMul)
    {
        var particles = GetComponent<ParticleSystem>();
        if (particles == null) return;

        var main = particles.main;
        main.maxParticles = Mathf.Max(1, (int)(main.maxParticles * particlesMul));

        var emission = particles.emission;
        if (!emission.enabled) return;

        var rateOverTime = emission.rateOverTime;
        {
            if (rateOverTime.constantMin > 1) rateOverTime.constantMin *= particlesMul;
            if (rateOverTime.constantMax > 1) rateOverTime.constantMax *= particlesMul;
            emission.rateOverTime = rateOverTime;
        }

        var rateOverDistance = emission.rateOverDistance;
        if (rateOverDistance.constantMin > 1)
        {
            if (rateOverDistance.constantMin > 1) rateOverDistance.constantMin *= particlesMul;
            if (rateOverDistance.constantMax > 1) rateOverDistance.constantMax *= particlesMul;
            emission.rateOverDistance = rateOverDistance;
        }

        ParticleSystem.Burst[] burst = new ParticleSystem.Burst[emission.burstCount];
        emission.GetBursts(burst);
        for (var i = 0; i < burst.Length; i++)
        {

            if (burst[i].minCount > 1) burst[i].minCount = (short)(burst[i].minCount * particlesMul);
            if (burst[i].maxCount > 1) burst[i].maxCount = (short)(burst[i].maxCount * particlesMul);
        }
        emission.SetBursts(burst);
    }
}

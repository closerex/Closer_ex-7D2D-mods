using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class RFX4_EffectSettings : MonoBehaviour
{
    [Range(0.1f, 1)] public float ParticlesBudget = 1;
    public bool UseLightShadows;
    public bool UseFastFlatDecalsForMobiles = true;
    public bool UseCustomColor;
    public Color EffectColor = Color.red;

    public bool IsVisible = true;
    public float FadeoutTime = 1.5f;

    public bool UseCollisionDetection = true;
    public bool LimitMaxDistance;
    public float MaxDistnace = -1;
    public float Mass = 1;
    public float Speed = 10;
    public float AirDrag = 0.1f;
    public bool UseGravity = true;

    private const string distortionNamePC = "KriptoFX/RFX4/Distortion";
    private const string distortionNameMobile = "KriptoFX/RFX4/DistortionMobile";
    private bool isCheckedDistortion;
    private bool prevIsVisible;
    private float currentFadeoutTime;

    Renderer[] renderers;
    Renderer[] skinRenderers;
    Light[] lights;
    ParticleSystem[] particleSystems;
    private AudioSource[] audioSources;

    private void Awake()
    {
        prevIsVisible = IsVisible;
        CacheRenderers();
    }

    void OnEnable()
    {
        if(ParticlesBudget < 0.99f) ChangeParticlesBudget(ParticlesBudget);
        if(UseCustomColor) ChangeParticleColor();
        if (UseFastFlatDecalsForMobiles && IsMobilePlatform()) SetFlatDecals();
        if (!UseLightShadows || IsMobilePlatform()) DisableShadows();
    }

    void Update()
    {
        if (prevIsVisible != IsVisible)
        {
            prevIsVisible = IsVisible;
            if (!IsVisible)
                StartCoroutine(Fadeout());
            else Fadein();
        }
    }

    void ChangeParticlesBudget(float particlesMul)
    {
        var particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particles)
        {
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, (int) (main.maxParticles * particlesMul));

            var emission = ps.emission;
            if (!emission.enabled) continue;

            var rateOverTime = emission.rateOverTime;

            {
                if (rateOverTime.constantMin > 1) rateOverTime.constantMin *= particlesMul;
                if (rateOverTime.constantMax > 1) rateOverTime.constantMax *= particlesMul;
                emission.rateOverTime = rateOverTime;
            }

            var rateOverDistance = emission.rateOverDistance;
            if (rateOverDistance.constantMin > 1 )
            {
                if(rateOverDistance.constantMin > 1) rateOverDistance.constantMin *= particlesMul;
                if(rateOverDistance.constantMax > 1) rateOverDistance.constantMax *= particlesMul;
                emission.rateOverDistance = rateOverDistance;
            }
        }
    }

    public void ChangeParticleColor()
    {
        Debug.Log("ColorChanged");
        var hue = RFX4_ColorHelper.ColorToHSV(EffectColor).H;
        RFX4_ColorHelper.ChangeObjectColorByHUE(gameObject, hue);

        var physxMotion = GetComponentInChildren<RFX4_PhysicsMotion>();
        if (physxMotion != null) physxMotion.HUE = hue;

        var rayCastCollision = GetComponentInChildren<RFX4_RaycastCollision>();
        if (rayCastCollision != null) rayCastCollision.HUE = hue;
    }

    public void SetFlatDecals()
    {
        var decals = GetComponentsInChildren<RFX4_Decal>();
        foreach (var decal in decals)
        {
            decal.IsScreenSpace = false;
        }
    }

    public void DisableShadows()
    {
        var lights = GetComponentsInChildren<Light>();
        foreach (var customLight in lights)
        {
                var lightCurves = customLight.GetComponent<RFX4_LightCurves>();
                if (lightCurves != null && lightCurves.UseShadowsIfPossible)
                {
                    lightCurves.UseShadowsIfPossible = false;
                }
                customLight.shadows = LightShadows.None;
        }

        var psLights = GetComponentsInChildren<RFX4_ParticleLight>();
        foreach (var psLight in psLights)
        {
            psLight.UseShadows = false;
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

    #region Fadeout


    IEnumerator Fadeout()
    {
        currentFadeoutTime = Time.time;
        while ((Time.time - currentFadeoutTime) < FadeoutTime)
        {
            ChangeAlphaFade();
            yield return new WaitForSeconds(1f/30f);
        }
    }

    string[] colorProperties =
    {
        "_TintColor", "_Color",  "_MainColor"
    };

    void UpdateAlphaByProperties(Material mat, float overrideAlpha = -1)
    {
        foreach (var prop in colorProperties)
        {
            if (mat.HasProperty(prop))
            {
                var color = mat.GetColor(prop);
                if (overrideAlpha > -0.5f) color.a = overrideAlpha;
                else   color.a -= (1f / 30f) / FadeoutTime;
                mat.SetColor(prop, color);
            }
        }
    }



    void ChangeAlphaFade()
    {
        foreach (var rend in renderers)
        {
            if (rend.GetComponent<ParticleSystem>() != null) continue;
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                UpdateAlphaByProperties(mats[i]);
            }
        }

        foreach (var rend in skinRenderers)
        {
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                UpdateAlphaByProperties(mats[i]);
            }
        }

        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].intensity -= (1f / 30f) / FadeoutTime;
        }

        foreach (var ps in particleSystems)
        {
            if (!ps.isStopped) ps.Stop();
        }

        foreach (var audioSource in audioSources)
        {
            audioSource.volume -= (1f / 30f) / FadeoutTime;;
        }
    }

    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        skinRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        lights = GetComponentsInChildren<Light>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        audioSources = GetComponentsInChildren<AudioSource>();
    }

    void Fadein()
    {
        var allGO = gameObject.GetComponentsInChildren<Transform>();
        foreach (var go in allGO)
        {
            go.gameObject.SetActive(false);
            go.gameObject.SetActive(true);
        }

        foreach (var rend in renderers)
        {
            if (rend.GetComponent<ParticleSystem>() != null) continue;
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                UpdateAlphaByProperties(mats[i], 1);
            }
        }
        foreach (var audioSource in audioSources)
        {
            audioSource.volume = 1;
        }
    }
#endregion
}

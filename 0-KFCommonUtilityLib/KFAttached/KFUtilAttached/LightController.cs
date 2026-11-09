using System;
using UnityEngine;

public class LightController : MonoBehaviour
{
    [SerializeField, Range(0.1f, 10)] private float duration = 0.2f;
    [SerializeField] private AnimationCurve lightIntensityCurve;
    [SerializeField] private AnimationCurve lightRangeCurve;
    [SerializeField] private Gradient lightColorGradient;
    private Light lightSource;
    private float elapsedTime = 0f;
    private bool muzzleSettingsCached;
    private bool firstPersonMuzzle;
    private Vector3 authoredLocalPosition;
    private LightRenderMode authoredRenderMode;
    private float firstPersonRangeScale = 1f;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        if (!lightSource) Destroy(this);
    }

    private void OnEnable()
    {
        elapsedTime = 0f;
        if (lightSource) lightSource.enabled = true;
    }

    // Called by the existing muzzle spawn path, not by a scene-wide update or search.
    public void PlayMuzzleFlash(bool isFpv)
    {
        if (!lightSource) lightSource = GetComponent<Light>();
        if (!lightSource) return;
        if (!muzzleSettingsCached)
        {
            authoredLocalPosition = transform.localPosition;
            authoredRenderMode = lightSource.renderMode;
            float peakRange = 0f;
            foreach (var key in lightRangeCurve.keys) peakRange = Mathf.Max(peakRange, key.value);
            if (peakRange > 0f) firstPersonRangeScale = Mathf.Max(1f, 2.56f / peakRange);
            muzzleSettingsCached = true;
        }
        firstPersonMuzzle = isFpv && lightSource.type == LightType.Point;
        transform.localPosition = authoredLocalPosition;
        lightSource.renderMode = firstPersonMuzzle ? LightRenderMode.ForcePixel : authoredRenderMode;
        if (firstPersonMuzzle && authoredLocalPosition == Vector3.zero && transform.parent != null)
        {
            var parent = transform.parent;
            transform.localPosition = parent.InverseTransformVector(parent.up * 0.14f + parent.forward * 0.02f);
        }
        enabled = true;
        elapsedTime = 0f;
        lightSource.enabled = true;
        ApplyLight(0f);
    }

    private void OnDisable()
    {
        if (lightSource) lightSource.enabled = false;
    }

    private void ApplyLight(float normalizedTime)
    {
        lightSource.intensity = lightIntensityCurve.Evaluate(normalizedTime);
        lightSource.range = lightRangeCurve.Evaluate(normalizedTime) * (firstPersonMuzzle ? firstPersonRangeScale : 1f);
        lightSource.color = lightColorGradient.Evaluate(normalizedTime);
    }

    private void Update()
    {
        if (!lightSource || !lightSource.enabled) return;
        // Keep the sample written this frame alive through rendering, even when dt > duration.
        if (firstPersonMuzzle && elapsedTime > duration)
        {
            elapsedTime = 0f;
            enabled = false;
            return;
        }
        ApplyLight(elapsedTime / duration);
        elapsedTime += Time.deltaTime;
        if (!firstPersonMuzzle && elapsedTime > duration)
        {
            elapsedTime = 0f;
            enabled = false;
        }
    }
}

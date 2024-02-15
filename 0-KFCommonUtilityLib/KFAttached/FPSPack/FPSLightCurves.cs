using UnityEngine;

public class FPSLightCurves : MonoBehaviour
{
    public AnimationCurve LightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float GraphTimeMultiplier = 1, GraphIntensityMultiplier = 1;

    private bool canUpdate;
    private bool firstUpdate;
    private float startTime;
    private Light lightSource;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
    }

    private void OnEnable()
    {
        lightSource.intensity = LightCurve.Evaluate(0);
        if (firstUpdate)
        {
            firstUpdate = false;
            return;
        }
        startTime = Time.time;
        canUpdate = true;
    }

    private void OnDisable()
    {
        firstUpdate = true;
        canUpdate = false;
    }

    private void Update()
    {
        var time = Time.time - startTime;
        if (canUpdate)
        {
            var eval = LightCurve.Evaluate(time / GraphTimeMultiplier) * GraphIntensityMultiplier;
            lightSource.intensity = eval;
        }
        if (time >= GraphTimeMultiplier)
            canUpdate = false;
    }
}
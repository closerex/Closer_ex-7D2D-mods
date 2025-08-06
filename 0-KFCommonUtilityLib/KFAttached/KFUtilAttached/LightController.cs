using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class LightController : MonoBehaviour
{
    [SerializeField, Range(0.1f, 10)]
    private float duration = 0.2f;
    [SerializeField]
    private AnimationCurve lightIntensityCurve;
    [SerializeField]
    private AnimationCurve lightRangeCurve;
    [SerializeField]
    private Gradient lightColorGradient;
    private Light lightSource;
    private float elapsedTime = 0f;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        if (!lightSource)
        {
            Destroy(this);
            return;
        }
    }

    private void Update()
    {
        if (lightSource && lightSource.enabled)
        {
            float intensity = lightIntensityCurve.Evaluate(elapsedTime / duration);
            float range = lightRangeCurve.Evaluate(elapsedTime / duration);
            Color color = lightColorGradient.Evaluate(elapsedTime / duration);
            lightSource.intensity = intensity;
            lightSource.range = range;
            lightSource.color = color;

            elapsedTime += Time.deltaTime;
            if (elapsedTime > duration)
            {
                elapsedTime = 0f;
                lightSource.enabled = false;
                enabled = false;
            }
        }
    }
}

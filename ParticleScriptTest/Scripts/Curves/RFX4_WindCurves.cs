using UnityEngine;

public class RFX4_WindCurves : MonoBehaviour
{
    public AnimationCurve WindCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float GraphTimeMultiplier = 1, GraphIntensityMultiplier = 1;
    public bool IsLoop;

    private bool canUpdate;
    private float startTime;
    private WindZone windZone;

    private void Awake()
    {
        windZone = GetComponent<WindZone>();
        windZone.windMain = WindCurve.Evaluate(0);
#if UNITY_2018_1_OR_NEWER //thanks unity for one more fucking change of standard behaviour...
        windZone.windMain = -WindCurve.Evaluate(0);
#else
        windZone.windMain = WindCurve.Evaluate(0);
#endif
    }

    private void OnEnable()
    {
        startTime = Time.time;
        canUpdate = true;
    }

    private void Update()
    {
        var time = Time.time - startTime;
        if (canUpdate)
        {
            var eval = WindCurve.Evaluate(time / GraphTimeMultiplier) * GraphIntensityMultiplier;
#if UNITY_2018_1_OR_NEWER
            windZone.windMain = -eval;
#else
            windZone.windMain = eval;
#endif
        }
        if (time >= GraphTimeMultiplier)
        {
            if (IsLoop) startTime = Time.time;
            else canUpdate = false;
        }
    }
}
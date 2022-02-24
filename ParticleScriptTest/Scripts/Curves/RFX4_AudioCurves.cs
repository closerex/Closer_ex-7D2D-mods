using UnityEngine;
using System.Collections;

public class RFX4_AudioCurves : MonoBehaviour
{
    public AnimationCurve AudioCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float GraphTimeMultiplier = 1;
    public bool IsLoop;

    private bool canUpdate;
    private float startTime;
    private AudioSource audioSource;
    private float startVolume;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        startVolume = audioSource.volume;
        audioSource.volume = AudioCurve.Evaluate(0);
    }

    private void OnEnable()
    {
        startTime = Time.time;
        canUpdate = true;
    }

    private void Update()
    {
        var time = Time.time - startTime;
        if (canUpdate) {
            var eval = AudioCurve.Evaluate(time / GraphTimeMultiplier) * startVolume;
            audioSource.volume = eval;
        }
        if (time >= GraphTimeMultiplier) {
            if (IsLoop) startTime = Time.time;
            else canUpdate = false;
        }
    }
}
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("KFAttachments/Utils/Rig Weight Over Time")]
public class RigWeightOverTime : MonoBehaviour
{
    //[SerializeField]
    //private Transform source;
    //[SerializeField]
    //private Transform target;
    [SerializeReference]
    private Rig[] rigs;
    //[SerializeField]
    //private float distanceThreshold;
    //[SerializeField]
    //private float distanceMax;
    //private float distanceRange;

    private (Coroutine co, bool active) copair;
    ////[SerializeField]
    ////private bool logDistance = false;

    //private void Awake()
    //{
    //    distanceRange = distanceMax - distanceThreshold;
    //    if (distanceRange == 0)
    //    {
    //        throw new DivideByZeroException("Max distance is equal to threshold distance!");
    //    }
    //}

    public void OnEnable()
    {
        if (rigs != null)
        {
            SetWeight(1);
        }
    }

    public void OnDisable()
    {
        if (copair.co != null)
        {
            StopCoroutine(copair.co);
        }
        SetWeight(0);
    }

    public void SetRigWeight(AnimationEvent ev)
    {
        if (copair.co != null)
        {
            StopCoroutine(copair.co);
        }
        bool active = Convert.ToBoolean(ev.intParameter);
        copair = (StartCoroutine(UpdateWeight(ev.floatParameter, active)), active);
    }

    private IEnumerator UpdateWeight(float time, bool active)
    {
        if (rigs == null)
        {
            yield break;
        }

        if (time == 0)
        {
            SetWeight(active ? 1 : 0);
            yield break;
        }

        float curTime = 0;
        while (curTime < time)
        {
            float ratio = curTime / time;
            float weight = Mathf.Lerp(0, 1, active ? ratio : (1 - ratio));
            SetWeight(weight);
            curTime += Time.deltaTime;
            Log.Out("Set weight: " + weight);
            yield return null;
        }
        SetWeight(active ? 1 : 0);
    }

    public void SetWeight(float weight)
    {
        foreach (var rig in rigs)
        {
            rig.weight = weight;
        }
    }

    //    private void Update()
    //    {
    //        StartCoroutine(UpdateWeight());
    //    }

    //    private IEnumerator UpdateWeight()
    //    {
    //        if(distanceRange == 0 || rigs == null)
    //        {
    //            yield break;
    //        }
    //        yield return new WaitForEndOfFrame();
    //        float distance = Vector3.Distance(source.position, target.position);
    //        float weight = Mathf.Lerp(0, 1, (distanceMax - distance) / distanceRange);
    //        foreach (Rig rig in rigs)
    //        {
    //            rig.weight = Mathf.Lerp(rig.weight, weight, 0.5f);
    //            if(weight > 0 && weight < 1)
    //                Log.Out("ratio: " + ((distanceMax - distance) / distanceRange).ToString() + " weight: " + weight.ToString());
    //        }

    //#if UNITY_EDITOR
    //        if (logDistance)
    //        {
    //            Log.Out(Vector3.Distance(source.position, target.position).ToString());
    //        }
    //#endif
    //    }
}
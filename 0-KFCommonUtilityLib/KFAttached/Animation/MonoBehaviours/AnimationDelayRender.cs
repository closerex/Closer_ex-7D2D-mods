using System;
using System.Collections;
using System.Collections.Generic;
#if NotEditor
using UniLinq;
#else
using System.Linq;
#endif
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;

[AddComponentMenu("")]
public class AnimationDelayRender : MonoBehaviour
{
#if NotEditor
    [Serializable]
    public class TransformTargets
    {
        public Transform target;
        public bool includeChildren;
    }

    public struct TransformLocalData
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public TransformLocalData(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            this.localPosition = localPosition;
            this.localRotation = localRotation;
            this.localScale = localScale;
        }
    }

    private struct TransformRestoreJobs : IJobParallelForTransform
    {
        public NativeArray<TransformLocalData> data;
        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                transform.SetLocalPositionAndRotation(data[index].localPosition, data[index].localRotation);
                transform.localScale = data[index].localScale;
            }
        }
    }

    private struct TransformRestoreAndSaveJobs : IJobParallelForTransform
    {
        public NativeArray<TransformLocalData> data;
        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                TransformLocalData targetData = new TransformLocalData(transform.localPosition, transform.localRotation, transform.localScale);
                transform.SetLocalPositionAndRotation(data[index].localPosition, data[index].localRotation);
                transform.localScale = data[index].localScale;
                data[index] = targetData;
            }
        }
    }

    private struct TransformSaveJobs : IJobParallelForTransform
    {
        public NativeArray<TransformLocalData> data;
        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                data[index] = new TransformLocalData(transform.localPosition, transform.localRotation, transform.localScale);
            }
        }
    }

    [NonSerialized]
    private Transform[] delayTargets;

    private TransformLocalData[] posTargets;
    private EntityPlayerLocal player;

    private NativeArray<TransformLocalData> data;
    TransformAccessArray transArr;
    private JobHandle restoreJob, restoreAndSaveJob;
    private bool dataInitialized = false;
    private bool skipNextUpdate = false;

    public void SkipNextUpdate()
    {
        skipNextUpdate = true;
    }

    private void Awake()
    {
        player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null)
        {
            Destroy(this);
            return;
        }
    }

    internal void InitializeTarget()
    {
        var delayTargetsSet = new HashSet<Transform>();
        foreach (Transform child in transform.GetComponentsInChildren<Transform>(true).Skip(1))
        {
            delayTargetsSet.Add(child);
        }
        delayTargets = delayTargetsSet.ToArray();
        posTargets = new TransformLocalData[delayTargets.Length];
        ClearNative();
        data = new NativeArray<TransformLocalData>(delayTargets.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        transArr = new TransformAccessArray(delayTargets);
    }

    private void InitializeData()
    {
        for (int i = 0; i < delayTargets.Length; i++)
        {
            Transform target = delayTargets[i];
            if (target)
            {
                data[i] = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
            }
        }
        dataInitialized = true;
    }

    private void OnEnable()
    {
        skipNextUpdate = false;
        InitializeTarget();
        player.playerCamera?.gameObject.GetOrAddComponent<AnimationDelayRenderReference>().targets.Add(this);
        //var preAnimatorUpdateJob = new TransformRestoreJobs
        //{
        //    data = data
        //};
        //restoreJob = preAnimatorUpdateJob.Schedule(transArr);
        StartCoroutine(EndOfFrameCo());
        Log.Out($"Delay render target count: {delayTargets.Length}");
    }

    private void OnDisable()
    {
        ClearNative();
        player.playerCamera?.gameObject.GetOrAddComponent<AnimationDelayRenderReference>().targets.Remove(this);
        StopAllCoroutines();
        dataInitialized = false;
        skipNextUpdate = false;
    }

    private void Update()
    {
        //for (int i = 0; i < delayTargets.Length; i++)
        //{
        //    Transform target = delayTargets[i];
        //    if (target)
        //    {
        //        target.localPosition = posTargets[i].localPosition;
        //        target.localRotation = posTargets[i].localRotation;
        //        target.localScale = posTargets[i].localScale;
        //    }
        //    else
        //    {
        //        delayTargets[i] = null;
        //    }
        //}
        restoreJob.Complete();
    }

    private void LateUpdate()
    {
        if (!dataInitialized)
            return;
        if (skipNextUpdate)
        {
            var postAnimationUpdateJob = new TransformSaveJobs
            {
                data = data
            };
            restoreAndSaveJob = postAnimationUpdateJob.Schedule(transArr);
        }
        else
        {
            var postAnimationUpdateJob = new TransformRestoreAndSaveJobs
            {
                data = data
            };
            restoreAndSaveJob = postAnimationUpdateJob.Schedule(transArr);
        }

        //for (int i = 0; i < delayTargets.Length; i++)
        //{
        //    Transform target = delayTargets[i];
        //    if (target)
        //    {
        //        TransformLocalData targetData = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
        //        target.localPosition = posTargets[i].localPosition;
        //        target.localRotation = posTargets[i].localRotation;
        //        target.localScale = posTargets[i].localScale;
        //        posTargets[i] = targetData;
        //    }
        //    else
        //    {
        //        delayTargets[i] = null;
        //    }
        //}
    }

    internal void PreCullCallback()
    {
        restoreAndSaveJob.Complete();
    }

    private IEnumerator EndOfFrameCo()
    {
        yield return null;
        InitializeData();
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (skipNextUpdate)
            {
                skipNextUpdate = false;
                continue;
            }
            var eofUpdateJob = new TransformRestoreJobs { data = data };
            restoreJob = eofUpdateJob.Schedule(transArr);
        }
    }

    private void ClearNative()
    {
        if (data.IsCreated)
        {
            data.Dispose();
        }
        if (transArr.isCreated)
        {
            transArr.Dispose();
        }
    }
#endif
}
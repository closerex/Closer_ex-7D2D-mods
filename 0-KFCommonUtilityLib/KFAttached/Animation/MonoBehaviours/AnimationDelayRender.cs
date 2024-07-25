using System;
using System.Collections.Generic;
#if NotEditor
using UniLinq;
#else
using System.Linq;
#endif
using UnityEngine;

[AddComponentMenu("")]
public class AnimationDelayRender : MonoBehaviour
{
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

    //[SerializeField]
    //private List<TransformTargets> delayTargetsEditor;
    [NonSerialized]
    private Transform[] delayTargets;

    //[SerializeField, HideInInspector]
    //private List<Transform> list_targets;
    //[SerializeField, HideInInspector]
    //private List<bool> list_include_children;
    //[SerializeField, HideInInspector]
    //private int serializedCount;

    private TransformLocalData[] posTargets;
    //private TransformLocalData[] posAfterAnimation;

    //public void OnAfterDeserialize()
    //{
    //}

    //public void OnBeforeSerialize()
    //{
    //    if (delayTargetsEditor != null && delayTargetsEditor.Count > 0)
    //    {
    //        serializedCount = 0;
    //        list_targets = new List<Transform>();
    //        list_include_children = new List<bool>();
    //        for (int i = 0; i < delayTargetsEditor.Count; i++)
    //        {
    //            list_targets.Add(delayTargetsEditor[i].target);
    //            list_include_children.Add(delayTargetsEditor[i].includeChildren);
    //            serializedCount++;
    //        }
    //    }
    //}
#if NotEditor
    private EntityPlayerLocal player;
#endif

    private void Awake()
    {
#if NotEditor
        player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null)
        {
            Destroy(this);
            return;
        }
#endif

        //var delayTargetsSet = new HashSet<Transform>();
        //for (int i = 0; i < serializedCount; i++)
        //{
        //    if (list_include_children[i])
        //    {
        //        var targets = list_targets[i].GetComponentsInChildren<Transform>();
        //        foreach (var target in targets)
        //        {
        //            delayTargetsSet.Add(target);
        //        }
        //    }
        //    else
        //    {
        //        delayTargetsSet.Add(list_targets[i]);
        //    }
        //}
        //delayTargets = delayTargetsSet.ToArray();
        //posTargets = new TransformLocalData[delayTargets.Length];
        //posAfterAnimation = new TransformLocalData[delayTargets.Count];
    }

    internal void InitializeTarget(Transform target)
    {
        var delayTargetsSet = new HashSet<Transform>();
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true).Skip(1))
        {
            delayTargetsSet.Add(child);
        }
        delayTargets = delayTargetsSet.ToArray();
        posTargets = new TransformLocalData[delayTargets.Length];
    }

    private void OnEnable()
    {
        InitializeTarget(transform);
        GetComponent<Animator>().PlayInFixedTime(0, 0, Time.deltaTime);
        for (int i = 0; i < delayTargets.Length; i++)
        {
            Transform target = delayTargets[i];
            if (target)
            {
                posTargets[i] = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
            }
            else
            {
                delayTargets[i] = null;
            }
        }
        Log.Out($"Delay render target count: {delayTargets.Length}");
    }

    private void Update()
    {
        for (int i = 0; i < delayTargets.Length; i++)
        {
            Transform target = delayTargets[i];
            if (target)
            {
                target.localPosition = posTargets[i].localPosition;
                target.localRotation = posTargets[i].localRotation;
                target.localScale = posTargets[i].localScale;
            }
            else
            {
                delayTargets[i] = null;
            }
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < delayTargets.Length; i++)
        {
            Transform target = delayTargets[i];
            if (target)
            {
                TransformLocalData targetData = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
                target.localPosition = posTargets[i].localPosition;
                target.localRotation = posTargets[i].localRotation;
                target.localScale = posTargets[i].localScale;
                posTargets[i] = targetData;
            }
            else
            {
                delayTargets[i] = null;
            }
        }
    }
}
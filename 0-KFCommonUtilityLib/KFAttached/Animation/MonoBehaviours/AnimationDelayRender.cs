using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Animation Delay Render")]
public class AnimationDelayRender : MonoBehaviour, ISerializationCallbackReceiver
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

    [SerializeField]
    private List<TransformTargets> delayTargetsEditor;
    [NonSerialized]
    private HashSet<Transform> delayTargets;

    [SerializeField, HideInInspector]
    private List<Transform> list_targets;
    [SerializeField, HideInInspector]
    private List<bool> list_include_children;
    [SerializeField, HideInInspector]
    private int serializedCount;

    private TransformLocalData[] posTargets;
    //private TransformLocalData[] posAfterAnimation;

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
        if (delayTargetsEditor != null && delayTargetsEditor.Count > 0)
        {
            serializedCount = 0;
            list_targets = new List<Transform>();
            list_include_children = new List<bool>();
            for (int i = 0; i < delayTargetsEditor.Count; i++)
            {
                list_targets.Add(delayTargetsEditor[i].target);
                list_include_children.Add(delayTargetsEditor[i].includeChildren);
                serializedCount++;
            }
        }
    }
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

        delayTargets = new HashSet<Transform>();
        for (int i = 0; i < serializedCount; i++)
        {
            if (list_include_children[i])
            {
                var targets = list_targets[i].GetComponentsInChildren<Transform>();
                foreach (var target in targets)
                {
                    delayTargets.Add(target);
                }
            }
            else
            {
                delayTargets.Add(list_targets[i]);
            }
        }
        posTargets = new TransformLocalData[delayTargets.Count];
        //posAfterAnimation = new TransformLocalData[delayTargets.Count];
    }

    private void OnEnable()
    {
        int i = 0;
        foreach (var target in delayTargets)
        {
            posTargets[i] = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
            i++;
        }
    }

    private void Update()
    {
        int i = 0;
        foreach (var target in delayTargets)
        {
            target.localPosition = posTargets[i].localPosition;
            target.localRotation = posTargets[i].localRotation;
            target.localScale = posTargets[i].localScale;
            i++;
        }
    }

    private void LateUpdate()
    {
        int i = 0;
        foreach (var target in delayTargets)
        {
            TransformLocalData targetData = new TransformLocalData(target.localPosition, target.localRotation, target.localScale);
            target.localPosition = posTargets[i].localPosition;
            target.localRotation = posTargets[i].localRotation;
            target.localScale = posTargets[i].localScale;
            posTargets[i] = targetData;
            i++;
        }
    }
}
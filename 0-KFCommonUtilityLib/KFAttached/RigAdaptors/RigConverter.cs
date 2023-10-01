#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine.Animations.Rigging;
#endif
using UnityEngine;

[AddComponentMenu("KFAttachments/RigAdaptors/Rig Converter")]
public class RigConverter : MonoBehaviour
{
    public Transform targetRoot;

#if UNITY_EDITOR
    [ContextMenu("Convert Rig Constraints to Adaptors")]
    private void Convert()
    {
        foreach (var constraint in GetComponentsInChildren<IRigConstraint>(true))
        {
            var adaptorName = constraint.GetType().Name + "Adaptor,KFCommonUtilityLib";
            var adaptorType = Type.GetType(adaptorName);
            var adaptor = ((object)constraint as MonoBehaviour).transform.AddMissingComponent(adaptorType) as RigAdaptorAbs;
            adaptor.ReadRigData();
            adaptor.hideFlags = HideFlags.NotEditable;
            EditorUtility.SetDirty(adaptor);
        }
        Save();
    }

    [ContextMenu("Read Adaptor Value to Constraints")]
    private void Read()
    {
        Rebind();
        Save();
    }

    [ContextMenu("Remove All Adaptors")]
    private void RemoveAll()
    {
        var constraints = GetComponentsInChildren<RigAdaptorAbs>(true);
        foreach (var constraint in constraints)
        {
            DestroyImmediate(constraint);
        }
        Save();
    }

    private void Save()
    {
        var root = GetComponentInParent<RigTargets>(true).gameObject;
        if(PrefabUtility.IsOutermostPrefabInstanceRoot(root))
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
    }
#endif

    public void Rebind()
    {
        foreach (var adaptor in GetComponentsInChildren<RigAdaptorAbs>(true))
        {
            adaptor.targetRoot = targetRoot;
            adaptor.FindRigTargets();
        }
    }
}

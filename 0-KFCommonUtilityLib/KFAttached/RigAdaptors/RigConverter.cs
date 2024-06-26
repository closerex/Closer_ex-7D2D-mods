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

    [ContextMenu("Fix A22 Constraints")]
    private void Fix()
    {
        Rebind();
        foreach (var constraint in GetComponentsInChildren<IRigConstraint>())
        {
            string name = constraint.component.transform.name;
            if (char.IsDigit(name[^1]))
            {
                if (name.Contains("ArmRollCorrections") && constraint.component is MultiRotationConstraint mrcArm)
                {
                    int index = int.Parse(name.Substring(name.Length - 1, 1));
                    var source = mrcArm.data.sourceObjects;
                    source.SetWeight(0, index * 0.25f);
                    mrcArm.data.sourceObjects = source;
                    mrcArm.data.constrainedXAxis = true;
                    mrcArm.data.constrainedYAxis = false;
                    mrcArm.data.constrainedZAxis = false;
                }
                else if (name.Contains("FingerTarget") && name.Length == 13 && constraint.component is TwoBoneIKConstraint tbc)
                {
                    int index = int.Parse(name.Substring(name.Length - 1, 1));
                    string targetNameBase = tbc.data.root.transform.name;
                    targetNameBase = targetNameBase[..^1];
                    tbc.data.root = targetRoot.FindInAllChilds($"{targetNameBase}1");
                    tbc.data.mid = targetRoot.FindInAllChilds($"{targetNameBase}{(index == 5 ? 3 : 2)}");
                    tbc.data.tip = targetRoot.FindInAllChilds($"{targetNameBase}4");
                }
                else if (name.Contains("FingerTargetRollCorrection") && constraint.component is MultiRotationConstraint mrcFinger)
                {
                    int index = int.Parse(name.Substring(name.Length - 1, 1));
                    mrcFinger.data.constrainedXAxis = true;
                    mrcFinger.data.constrainedYAxis = false;
                    mrcFinger.data.constrainedZAxis = index == 1;
                }                
            }
        }
        Convert();
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

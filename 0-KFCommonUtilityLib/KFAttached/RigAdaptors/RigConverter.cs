#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine.Animations.Rigging;
#endif
using UnityEngine;
using System.Linq;

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
            if (constraint.component.TryGetComponent<RigConverterRole>(out var role) && role.role == RigConverterRole.Role.Ignore)
            {
                continue;
            }
            var adaptorName = constraint.GetType().Name;
            if (role && role.role == RigConverterRole.Role.Reverse)
            {
                adaptorName += "Reverse";
            }
            adaptorName += "Adaptor,KFCommonUtilityLib";
            var adaptorType = Type.GetType(adaptorName);
            var adaptor = (RigAdaptorAbs)constraint.component.transform.AddMissingComponent(adaptorType);
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
                    string targetNameBase = tbc.data.root.transform.name[..^1];
                    tbc.data.root = targetRoot.FindInAllChildren($"{targetNameBase}1");
                    tbc.data.mid = targetRoot.FindInAllChildren($"{targetNameBase}{(index == 5 ? 3 : 2)}");
                    tbc.data.tip = targetRoot.FindInAllChildren($"{targetNameBase}4");
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

    [ContextMenu("Convert To New Rig Setup")]
    private void Renew()
    {
        Rebind();
        string leftHandTransName = null, rightHandTransName = null;
        foreach (var tbik in GetComponentsInChildren<TwoBoneIKConstraint>())
        {
            if(tbik.data.tip.name.Contains("hand", StringComparison.OrdinalIgnoreCase))
            {
                Transform target = tbik.data.target;
                if(target.name.Contains("target", StringComparison.OrdinalIgnoreCase))
                {
                    target = target.parent;
                }
                if (tbik.data.tip.name.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
                {
                    leftHandTransName = target.name;
                }
                else
                {
                    rightHandTransName = target.name;
                }
            }
        }
        if(leftHandTransName == null || rightHandTransName == null)
        {
            Log.Error("Left/Right hand transform not found on weapon skeleton!");
            return;
        }
        foreach (var constraint in GetComponentsInChildren<IRigConstraint>())
        {
            Transform trans = constraint.component.transform;
            string name = trans.name;
            if (char.IsDigit(name[^1]) && name.StartsWith("FingerTarget") && !trans.parent.name.StartsWith("FingerTarget") && constraint is TwoBoneIKConstraint tbik)
            {
                int index = int.Parse(name.Substring(name.Length - 1, 1));
                if(index > 4)
                {
                    continue;
                }
                GameObject newConstraint = new(name);
                newConstraint.transform.SetParent(trans, true);
                newConstraint.transform.SetAsFirstSibling();
                newConstraint.transform.position = trans.position;
                EditorUtility.CopySerialized(tbik, newConstraint.AddComponent<TwoBoneIKConstraint>());
                DestroyImmediate(tbik);
                if (trans.GetComponent<TwoBoneIKConstraintAdaptor>() is TwoBoneIKConstraintAdaptor adaptor)
                {
                    DestroyImmediate(adaptor);
                }

                string targetNameBase = tbik.data.root.transform.name[..^1];
                bool isLeft = targetNameBase.StartsWith("Left");
                GameObject metacarpal = new($"MetacarpalAiming{index}");
                var aimConstraint = metacarpal.AddComponent<MultiAimConstraint>();
                aimConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{targetNameBase}0");
                aimConstraint.data.aimAxis = isLeft ? MultiAimConstraintData.Axis.X_NEG : MultiAimConstraintData.Axis.X;
                aimConstraint.data.upAxis = isLeft ? MultiAimConstraintData.Axis.Y : MultiAimConstraintData.Axis.Y_NEG;
                aimConstraint.data.worldUpType = MultiAimConstraintData.WorldUpType.None;
                aimConstraint.data.constrainedXAxis = false;
                aimConstraint.data.constrainedYAxis = false;
                aimConstraint.data.constrainedZAxis = true;
                Transform curSource = tbik.data.target;
                while(((isLeft && curSource.parent.parent.name != leftHandTransName) || (!isLeft && curSource.parent.parent.name != rightHandTransName)) && !curSource.parent.name.Contains("1"))
                {
                    curSource = curSource.parent;
                }
                var sourceArr = aimConstraint.data.sourceObjects;
                var source = new WeightedTransform(curSource.parent.name.Contains("1") ? curSource.parent : curSource, 1);
                sourceArr.Add(source);
                aimConstraint.data.sourceObjects = sourceArr;
                metacarpal.transform.SetParent(trans.transform);
                metacarpal.transform.SetAsFirstSibling();
            }
        }
        Convert();
    }

    public void CreateEmpty()
    {
        CreateEmptyForSide("Left");
        CreateEmptyForSide("Right");
    }

    private static string[] PhalangeBoneNames = new[]
    {
        "Thumb",
        "Index",
        "Middle",
        "Pinky",
        "Ring"
    };

    private void CreateEmptyForSide(string side)
    {
        WeightedTransformArray sourceObjects = new();

        Transform shoulderReposition = new GameObject($"{side}ShoulderReposition").transform;
        shoulderReposition.parent = transform;
        MultiPositionConstraint shoulderRepositionConstraint = shoulderReposition.gameObject.AddComponent<MultiPositionConstraint>();
        shoulderRepositionConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}Shoulder");
        shoulderRepositionConstraint.data.constrainedXAxis = shoulderRepositionConstraint.data.constrainedYAxis = shoulderRepositionConstraint.data.constrainedZAxis = true;

        Transform armRollCorrection = new GameObject($"{side}ArmRollCorrections").transform;
        armRollCorrection.parent = transform;
        for (int i = 1; i <= 4; i++)
        {
            Transform child = new GameObject($"{armRollCorrection.name}{i}").transform;
            child.parent = armRollCorrection;
            MultiRotationConstraint armRollCorrectionConstraint = child.gameObject.AddComponent<MultiRotationConstraint>();
            armRollCorrectionConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}ForeArmRoll{i}");
            armRollCorrectionConstraint.data.constrainedXAxis = true;
            armRollCorrectionConstraint.data.constrainedYAxis = false;
            armRollCorrectionConstraint.data.constrainedZAxis = false;
            sourceObjects.Add(new WeightedTransform(null, i * .25f));
            armRollCorrectionConstraint.data.sourceObjects = sourceObjects;
            sourceObjects.Clear();
        }
        
        Transform armTarget = new GameObject($"{side}ArmTarget").transform;
        armTarget.parent = transform;
        TwoBoneIKConstraint armTargetConstraint = armTarget.gameObject.AddComponent<TwoBoneIKConstraint>();
        armTargetConstraint.data.root = targetRoot.FindInAllChildren($"{side}Arm");
        armTargetConstraint.data.mid = targetRoot.FindInAllChildren($"{side}ForeArm");
        armTargetConstraint.data.tip = targetRoot.FindInAllChildren($"{side}Hand");

        bool isLeft = side == "Left";
        Transform handTargets = new GameObject($"{side}HandTargets").transform;
        handTargets.parent = transform;
        for (int i = 1; i <= 4; i++)
        {
            string fingerTargetName = $"FingerTarget{i}";
            Transform fingerTargetParent = new GameObject(fingerTargetName).transform;
            fingerTargetParent.parent = handTargets;

            Transform metacarpalAiming = new GameObject($"MetacarpalAiming{i}").transform;
            metacarpalAiming.parent = fingerTargetParent;
            MultiAimConstraint metacarpalAimingConstraint = metacarpalAiming.gameObject.AddComponent<MultiAimConstraint>();
            metacarpalAimingConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}Hand{PhalangeBoneNames[i]}0");
            metacarpalAimingConstraint.data.aimAxis = isLeft ? MultiAimConstraintData.Axis.X_NEG : MultiAimConstraintData.Axis.X;
            metacarpalAimingConstraint.data.upAxis = isLeft ? MultiAimConstraintData.Axis.Y : MultiAimConstraintData.Axis.Y_NEG;
            metacarpalAimingConstraint.data.worldUpType = MultiAimConstraintData.WorldUpType.None;
            metacarpalAimingConstraint.data.constrainedXAxis = false;
            metacarpalAimingConstraint.data.constrainedYAxis = false;
            metacarpalAimingConstraint.data.constrainedZAxis = true;

            Transform fingerTarget = new GameObject(fingerTargetName).transform;
            fingerTarget.parent = fingerTargetParent;
            TwoBoneIKConstraint fingerTargetConstraint = fingerTarget.gameObject.AddComponent<TwoBoneIKConstraint>();
            fingerTargetConstraint.data.root = targetRoot.FindInAllChildren($"{side}Hand{PhalangeBoneNames[i]}1");
            fingerTargetConstraint.data.mid = targetRoot.FindInAllChildren($"{side}Hand{PhalangeBoneNames[i]}2");
            fingerTargetConstraint.data.tip = targetRoot.FindInAllChildren($"{side}Hand{PhalangeBoneNames[i]}4");
        }

        Transform thumbTargetParent = new GameObject("FingerTarget5").transform;
        thumbTargetParent.parent = handTargets;

        Transform thumbTargetRollCorrection1 = new GameObject("FingerTargetRollCorrection1").transform;
        thumbTargetRollCorrection1.parent = thumbTargetParent;
        MultiRotationConstraint thumbTargetRollCorrectionConstraint = thumbTargetRollCorrection1.gameObject.AddComponent<MultiRotationConstraint>();
        thumbTargetRollCorrectionConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}HandThumb1");
        thumbTargetRollCorrectionConstraint.data.constrainedXAxis = thumbTargetRollCorrectionConstraint.data.constrainedYAxis = thumbTargetRollCorrectionConstraint.data.constrainedZAxis = true;
        sourceObjects.Add(new WeightedTransform(null, .5f));
        thumbTargetRollCorrectionConstraint.data.sourceObjects = sourceObjects;
        sourceObjects.Clear();
        
        Transform thumbTargetRollCorrection2 = new GameObject("FingerTargetRollCorrection2").transform;
        thumbTargetRollCorrection2.parent = thumbTargetParent;
        thumbTargetRollCorrectionConstraint = thumbTargetRollCorrection2.gameObject.AddComponent<MultiRotationConstraint>();
        thumbTargetRollCorrectionConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}HandThumb2");
        thumbTargetRollCorrectionConstraint.data.constrainedXAxis = true;
        thumbTargetRollCorrectionConstraint.data.constrainedYAxis = false;
        thumbTargetRollCorrectionConstraint.data.constrainedZAxis = false;
        sourceObjects.Add(new WeightedTransform(null, .3f));
        thumbTargetRollCorrectionConstraint.data.sourceObjects = sourceObjects;
        sourceObjects.Clear();
        
        Transform thumbTargetRollCorrection3 = new GameObject("FingerTargetRollCorrection3").transform;
        thumbTargetRollCorrection3.parent = thumbTargetParent;
        thumbTargetRollCorrectionConstraint = thumbTargetRollCorrection3.gameObject.AddComponent<MultiRotationConstraint>();
        thumbTargetRollCorrectionConstraint.data.constrainedObject = targetRoot.FindInAllChildren($"{side}HandThumb3");
        thumbTargetRollCorrectionConstraint.data.constrainedXAxis = true;
        thumbTargetRollCorrectionConstraint.data.constrainedYAxis = false;
        thumbTargetRollCorrectionConstraint.data.constrainedZAxis = false;
        sourceObjects.Add(new WeightedTransform(null, .2f));
        thumbTargetRollCorrectionConstraint.data.sourceObjects = sourceObjects;
        sourceObjects.Clear();

        Transform thumbTarget = new GameObject("FingerTarget5").transform;
        thumbTarget.parent = thumbTargetParent;
        TwoBoneIKConstraint thumbTargetConstraint = thumbTarget.gameObject.AddComponent<TwoBoneIKConstraint>();
        thumbTargetConstraint.data.root = targetRoot.FindInAllChildren($"{side}HandThumb1");
        thumbTargetConstraint.data.mid = targetRoot.FindInAllChildren($"{side}HandThumb3");
        thumbTargetConstraint.data.tip = targetRoot.FindInAllChildren($"{side}HandThumb4");
        thumbTargetConstraint.data.targetRotationWeight = 0;
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Animations.Rigging;
using UnityEngine.Animations;
using UnityEngine;

public class TwistCorrectionAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private Transform m_Source;
    [SerializeField]
    private TwistCorrectionData.Axis m_TwistAxis;
    [SerializeField]
    private (string name, float weight)[] m_TwistNodes;
    public override void FindRigTargets()
    {
        var constraint = GetComponent<TwistCorrection>();
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.sourceObject = m_Source;
        constraint.data.twistAxis = m_TwistAxis;
        constraint.data.twistNodes = new WeightedTransformArray(m_TwistNodes.Length);
        for (int i = 0; i < m_TwistNodes.Length; i++)
        {
            constraint.data.twistNodes.SetTransform(i, targetRoot.FindInAllChilds(m_TwistNodes[i].name));
            constraint.data.twistNodes.SetWeight(i, m_TwistNodes[i].weight);
        }
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<TwistCorrection>();
        weight = constraint.weight;
        m_Source = constraint.data.sourceObject;
        m_TwistAxis = constraint.data.twistAxis;
        m_TwistNodes = constraint.data.twistNodes.Select(n => (n.transform.gameObject?.name, n.weight)).ToArray();
    }
}

using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("")]
public class TwistCorrectionAdaptor : RigAdaptorAbs
{
    [SerializeField]
    private string m_Source;
    [SerializeField]
    private TwistCorrectionData.Axis m_TwistAxis;
    [SerializeField]
    private string[] m_TwistNodes;
    public override void FindRigTargets()
    {
        var constraint = GetComponent<TwistCorrection>();
        if (m_TwistNodes == null)
        {
            Log.Error("twist nodes array not serialized!");
            Component.Destroy(constraint);
            Component.Destroy(this);
            return;
        }
        constraint.Reset();
        constraint.weight = weight;
        constraint.data.sourceObject = targetRoot.FindInAllChildren(m_Source);
        constraint.data.twistAxis = m_TwistAxis;
        var twistNodes = new WeightedTransformArray(m_TwistNodes.Length);
        for (int i = 0; i < m_TwistNodes.Length; i++)
        {
            string[] node = m_TwistNodes[i].Split(';');
            if (node.Length == 2)
            {
                if (!string.IsNullOrEmpty(node[0]))
                    twistNodes.SetTransform(i, targetRoot.FindInAllChildren(node[0]));
                twistNodes.SetWeight(i, float.Parse(node[1]));
            }
        }
        constraint.data.twistNodes = twistNodes;
    }

    public override void ReadRigData()
    {
        var constraint = GetComponent<TwistCorrection>();
        weight = constraint.weight;
        m_Source = constraint.data.sourceObject.name;
        m_TwistAxis = constraint.data.twistAxis;
        m_TwistNodes = constraint.data.twistNodes.Select(n => (n.transform.gameObject?.name ?? "") + ';' + n.weight.ToString()).ToArray();
    }
}

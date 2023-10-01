using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib.RigAdaptors.Adaptors.Data
{
    [Serializable]
    public class TwistNode
    {
        [SerializeField]
        public string name;
        [SerializeField]
        public float weight;

        public TwistNode() { }

        public TwistNode(string name, float weight)
        {
            this.name = name;
            this.weight = weight;
        }
    }
}

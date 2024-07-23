using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

public class AnimationRigLayerController : StateMachineBehaviour, ISerializationCallbackReceiver
{
#if UNITY_EDITOR
    [Serializable]
    public struct State
    {
        public byte layer;
        public bool enable;
    }
    [SerializeField]
    public State[] layerStatesEditor;
#endif
    [SerializeField, HideInInspector]
    private int[] layers;

    public void OnAfterDeserialize()
    {

    }

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if(layerStatesEditor != null)
        {
            layers = new int[layerStatesEditor.Length];
            for (int i = 0; i < layerStatesEditor.Length; i++)
            {
                layers[i] = layerStatesEditor[i].layer | (layerStatesEditor[i].enable ? 0 : 0x8000);
            }
        }
#endif
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (layers == null)
            return;

        RigBuilder rigBuilder = animator.GetComponent<RigBuilder>();
        if (rigBuilder && rigBuilder.layers != null)
        {
            foreach (var layer in layers)
            {
                int realLayer = layer & 0x7fff;
                if (realLayer >= rigBuilder.layers.Count)
                {
                    continue;
                }
                rigBuilder.layers[realLayer].active = (layer & 0x8000) <= 0;
            }
        }
    }
}
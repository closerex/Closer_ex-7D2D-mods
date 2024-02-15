using UnityEngine;
using UnityEngine.Animations.Rigging;

[AddComponentMenu("KFAttachments/Binding Helpers/Rig Activation Binding")]
public class RigActivationBinding : MonoBehaviour
{
    [SerializeField]
    private RigLayer[] bindings;
    [SerializeField]
    private RigLayer[] inverseBindings;
    [SerializeField]
    private RigLayer[] enableOnDisable;
    [SerializeField]
    private RigLayer[] disableOnEnable;

    private void OnEnable()
    {
        //Log.Out(gameObject.name + " OnEnable!");
        if (bindings != null)
        {
            foreach (RigLayer t in bindings)
                if (t != null)
                    t.active = true;
        }
        if (inverseBindings != null)
        {
            foreach (RigLayer t in inverseBindings)
            {
                if (t != null)
                    t.active = false;
            }
        }
        if (disableOnEnable != null)
        {
            foreach (RigLayer t in disableOnEnable)
            {
                if (t != null)
                    t.active = false;
            }
        }
    }

    private void OnDisable()
    {
        //Log.Out(gameObject.name + " OnDisable!");
        if (bindings != null)
        {
            foreach (RigLayer t in bindings)
                if (t != null)
                    t.active = false;
        }
        if (inverseBindings != null)
        {
            foreach (RigLayer t in inverseBindings)
            {
                if (t != null)
                    t.active = true;
            }
        }
        if (enableOnDisable != null)
        {
            foreach (RigLayer t in enableOnDisable)
            {
                if (t != null)
                    t.active = true;
            }
        }
    }
}

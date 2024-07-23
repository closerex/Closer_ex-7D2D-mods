using UnityEngine;

[AddComponentMenu("KFAttachments/Binding Helpers/Transform Activation Binding")]
public class TransformActivationBinding : MonoBehaviour
{
    [SerializeField]
    private GameObject[] bindings;
    [SerializeField]
    private GameObject[] inverseBindings;
    [SerializeField]
    private GameObject[] enableOnDisable;
    [SerializeField]
    private GameObject[] disableOnEnable;
    [SerializeField]
    private GameObject[] enableOnEnable;
    [SerializeField]
    private GameObject[] disableOnDisable;
    [SerializeField]
    private string[] animatorParamBindings;
    internal Animator animator;

    private void OnEnable()
    {
        //Log.Out(gameObject.name + " OnEnable!");
        if (bindings != null)
        {
            foreach (GameObject t in bindings)
            {
                if (t != null)
                    t.SetActive(true);
            }
        }
        if (inverseBindings != null)
        {
            foreach (GameObject t in inverseBindings)
            {
                if (t != null)
                    t.SetActive(false);
            }
        }
        if (disableOnEnable != null)
        {
            foreach (GameObject t in disableOnEnable)
            {
                if (t != null)
                    t.SetActive(false);
            }
        }
        if (enableOnEnable != null)
        {
            foreach (GameObject t in enableOnEnable)
            {
                if (t != null)
                    t.SetActive(true);
            }
        }
        UpdateBool(true);
    }

    private void OnDisable()
    {
        //Log.Out(gameObject.name + " OnDisable!");
        if (bindings != null)
        {
            foreach (GameObject t in bindings)
                if (t != null)
                    t.SetActive(false);
        }
        if (inverseBindings != null)
        {
            foreach (GameObject t in inverseBindings)
            {
                if (t != null)
                    t.SetActive(true);
            }
        }
        if (enableOnDisable != null)
        {
            foreach (GameObject t in enableOnDisable)
            {
                if (t != null)
                    t.SetActive(true);
            }
        }
        if (disableOnDisable != null)
        {
            foreach (GameObject t in disableOnDisable)
            {
                if (t != null)
                    t.SetActive(true);
            }
        }
        UpdateBool(false);
    }

    internal void UpdateBool(bool enabled)
    {
        if (animatorParamBindings != null && animator != null)
        {
            foreach (string str in animatorParamBindings)
            {
                if (str != null)
                {
                    animator.SetBool(str, enabled);
                }
            }
        }
    }
}

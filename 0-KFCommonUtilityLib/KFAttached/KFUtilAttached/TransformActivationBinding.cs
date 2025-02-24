using System.Collections;
using UnityEngine;

[AddComponentMenu("KFAttachments/Binding Helpers/Transform Activation Binding")]
public class TransformActivationBinding : MonoBehaviour
{
    [SerializeField]
    internal GameObject[] bindings;
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
    internal AnimationTargetsAbs targets;

    private void OnEnable()
    {
        //Log.Out(gameObject.name + " OnEnable!");
        if (bindings != null)
        {
            foreach (GameObject t in bindings)
            {
                if (t)
                    t.SetActive(true);
            }
        }
        if (inverseBindings != null)
        {
            foreach (GameObject t in inverseBindings)
            {
                if (t)
                    t.SetActive(false);
            }
        }
        if (disableOnEnable != null)
        {
            foreach (GameObject t in disableOnEnable)
            {
                if (t)
                    t.SetActive(false);
            }
        }
        if (enableOnEnable != null)
        {
            foreach (GameObject t in enableOnEnable)
            {
                if (t)
                    t.SetActive(true);
            }
        }
#if NotEditor
        ThreadManager.StartCoroutine(UpdateBool(true));
#else
        UpdateBoolEditor(true);
#endif
    }

    private void OnDisable()
    {
        //Log.Out(gameObject.name + " OnDisable!");
        if (bindings != null)
        {
            foreach (GameObject t in bindings)
                if (t)
                    t.SetActive(false);
        }
        if (inverseBindings != null)
        {
            foreach (GameObject t in inverseBindings)
            {
                if (t)
                    t.SetActive(true);
            }
        }
        if (enableOnDisable != null)
        {
            foreach (GameObject t in enableOnDisable)
            {
                if (t)
                    t.SetActive(true);
            }
        }
        if (disableOnDisable != null)
        {
            foreach (GameObject t in disableOnDisable)
            {
                if (t)
                    t.SetActive(true);
            }
        }
#if NotEditor
        ThreadManager.StartCoroutine(UpdateBool(false));
#else
        UpdateBoolEditor(false);
#endif
    }

#if NotEditor
    internal IEnumerator UpdateBool(bool enabled)
    {
        yield return new WaitForEndOfFrame();
        if (animatorParamBindings != null && targets && targets.IsAnimationSet)
        {
            IAnimatorWrapper animator = targets.GraphBuilder.WeaponWrapper;
            if (animator == null || !animator.IsValid)
            {
                Log.Warning($"animator wrapper invalid!");
                yield break;
            }
            foreach (string str in animatorParamBindings)
            {
                if (str != null)
                {
                    animator.SetBool(str, enabled);
                }
            }
        }
    }
#else
    internal void UpdateBoolEditor(bool enabled)
    {
        if (animatorParamBindings != null && targets && targets.IsAnimationSet)
        {
            IAnimatorWrapper animator = targets.GraphBuilder.WeaponWrapper;
            if (animator == null || !animator.IsValid)
            {
                Log.Warning($"animator wrapper invalid!");
                return;
            }
            foreach (string str in animatorParamBindings)
            {
                if (str != null)
                {
                    animator.SetBool(str, enabled);
                }
            }
        }
    }
#endif
}

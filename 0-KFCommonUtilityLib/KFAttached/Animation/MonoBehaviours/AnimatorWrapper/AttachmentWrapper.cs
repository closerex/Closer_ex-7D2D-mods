using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AttachmentWrapper : IAnimatorWrapper
{
    public Animator[] animators;
    public AttachmentWrapper(Animator[] animators)
    {
        this.animators = animators;
    }

    public bool IsValid => animators != null && animators.Length > 0;

    public void CrossFade(string stateName, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateName, transitionDuration);
        }
    }

    public void CrossFade(string stateName, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateName, transitionDuration, layer);
        }
    }

    public void CrossFade(string stateName, float transitionDuration, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateName, transitionDuration, layer, normalizedTime);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateNameHash, transitionDuration);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateNameHash, transitionDuration, layer);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            animator.CrossFade(stateNameHash, transitionDuration, layer, normalizedTime);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateName, transitionDuration);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateName, transitionDuration, layer);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateName, transitionDuration, layer, fixedTime);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateNameHash, transitionDuration);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer, fixedTime);
        }
    }

    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex)
    {
        return animators[0].GetAnimatorTransitionInfo(layerIndex);
    }

    public bool GetBool(string name)
    {
        return GetBool(Animator.StringToHash(name));
    }

    public bool GetBool(int id)
    {
        foreach (var animator in animators)
        {
            if (animator.GetBool(id))
                return true;
        }
        return false;
    }

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex)
    {
        return animators[0].GetCurrentAnimatorClipInfo(layerIndex);
    }

    public void GetCurrentAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
    {
        animators[0].GetCurrentAnimatorClipInfo(layerIndex, clips);
    }

    public int GetCurrentAnimatorClipInfoCount(int layerIndex)
    {
        return animators[0].GetCurrentAnimatorClipInfoCount(layerIndex);
    }

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        return animators[0].GetCurrentAnimatorStateInfo(layerIndex);
    }

    public float GetFloat(string name)
    {
        return GetFloat(Animator.StringToHash(name));
    }

    public float GetFloat(int id)
    {
        foreach (var animator in animators)
        {
            float value = animator.GetFloat(id);
            if (value != 0)
            {
                return value;
            }
        }
        return 0;
    }

    public int GetInteger(string name)
    {
        return GetInteger(Animator.StringToHash(name));
    }

    public int GetInteger(int id)
    {
        foreach (var animator in animators)
        {
            int value = animator.GetInteger(id);
            if (value != 0)
            {
                return value;
            }
        }
        return 0;
    }

    public int GetLayerCount()
    {
        return animators[0].layerCount;
    }

    public int GetLayerIndex(string layerName)
    {
        return animators[0].GetLayerIndex(layerName);
    }

    public string GetLayerName(int layerIndex)
    {
        return animators[0].GetLayerName(layerIndex);
    }

    public float GetLayerWeight(int layerIndex)
    {
        return animators[0].GetLayerWeight(layerIndex);
    }

    public void GetNextAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
    {
        animators[0].GetNextAnimatorClipInfo(layerIndex, clips);
    }

    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex)
    {
        return animators[0].GetNextAnimatorClipInfo(layerIndex);
    }

    public int GetNextAnimatorClipInfoCount(int layerIndex)
    {
        return animators[0].GetNextAnimatorClipInfoCount(layerIndex);
    }

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
    {
        return animators[0].GetNextAnimatorStateInfo(layerIndex);
    }

    public AnimatorControllerParameter GetParameter(int index)
    {
        return animators[0].GetParameter(index);
    }

    public int GetParameterCount()
    {
        return animators[0].parameterCount;
    }

    public bool HasState(int layerIndex, int stateID)
    {
        return animators[0].HasState(layerIndex, stateID);
    }

    public bool IsInTransition(int layerIndex)
    {
        return animators[0].IsInTransition(layerIndex);
    }

    public bool IsParameterControlledByCurve(string name)
    {
        return animators[0].IsParameterControlledByCurve(name);
    }

    public bool IsParameterControlledByCurve(int id)
    {
        return animators[0].IsParameterControlledByCurve(id);
    }

    public void Play(string stateName)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateName);
        }
    }

    public void Play(string stateName, int layer)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateName, layer);
        }
    }

    public void Play(string stateName, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateName, layer, normalizedTime);
        }
    }

    public void Play(int stateNameHash)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateNameHash);
        }
    }

    public void Play(int stateNameHash, int layer)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateNameHash, layer);
        }
    }

    public void Play(int stateNameHash, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            animator.Play(stateNameHash, layer, normalizedTime);
        }
    }

    public void PlayInFixedTime(string stateName)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateName);
        }
    }

    public void PlayInFixedTime(string stateName, int layer)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateName, layer);
        }
    }

    public void PlayInFixedTime(string stateName, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateName, layer, fixedTime);
        }
    }

    public void PlayInFixedTime(int stateNameHash)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateNameHash);
        }
    }

    public void PlayInFixedTime(int stateNameHash, int layer)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateNameHash, layer);
        }
    }

    public void PlayInFixedTime(int stateNameHash, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            animator.PlayInFixedTime(stateNameHash, layer, fixedTime);
        }
    }

    public void ResetTrigger(string name)
    {
        foreach (var animator in animators)
        {
            animator.ResetTrigger(name);
        }
    }

    public void ResetTrigger(int id)
    {
        foreach (var animator in animators)
        {
            animator.ResetTrigger(id);
        }
    }

    public void SetBool(string name, bool value)
    {
        foreach (var animator in animators)
        {
            animator.SetBool(name, value);
        }
    }

    public void SetBool(int id, bool value)
    {
        foreach (var animator in animators)
        {
            animator.SetBool(id, value);
        }
    }

    public void SetFloat(string name, float value)
    {
        foreach (var animator in animators)
        {
            animator.SetFloat(name, value);
        }
    }

    public void SetFloat(int id, float value)
    {
        foreach (var animator in animators)
        {
            animator.SetFloat(id, value);
        }
    }

    public void SetInteger(string name, int value)
    {
        foreach (var animator in animators)
        {
            animator.SetInteger(name, value);
        }
    }

    public void SetInteger(int id, int value)
    {
        foreach (var animator in animators)
        {
            animator.SetInteger(id, value);
        }
    }

    public void SetLayerWeight(int layerIndex, float weight)
    {
        foreach (var animator in animators)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }

    public void SetTrigger(string name)
    {
        foreach (var animator in animators)
        {
            animator.SetTrigger(name);
        }
    }

    public void SetTrigger(int id)
    {
        foreach (var animator in animators)
        {
            animator.SetTrigger(id);
        }
    }
}

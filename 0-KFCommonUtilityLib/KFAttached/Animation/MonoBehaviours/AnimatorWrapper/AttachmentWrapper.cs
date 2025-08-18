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
            if (animator)
                animator.CrossFade(stateName, transitionDuration);
        }
    }

    public void CrossFade(string stateName, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFade(stateName, transitionDuration, layer);
        }
    }

    public void CrossFade(string stateName, float transitionDuration, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFade(stateName, transitionDuration, layer, normalizedTime);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFade(stateNameHash, transitionDuration);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFade(stateNameHash, transitionDuration, layer);
        }
    }

    public void CrossFade(int stateNameHash, float transitionDuration, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFade(stateNameHash, transitionDuration, layer, normalizedTime);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateName, transitionDuration);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateName, transitionDuration, layer);
        }
    }

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateName, transitionDuration, layer, fixedTime);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateNameHash, transitionDuration);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer);
        }
    }

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer, fixedTime);
        }
    }

    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex)
    {
        return animators.First(anim => anim).GetAnimatorTransitionInfo(layerIndex);
    }

    public bool GetBool(string name)
    {
        return GetBool(Animator.StringToHash(name));
    }

    public bool GetBool(int id)
    {
        foreach (var animator in animators)
        {
            if (animator && animator.GetBool(id))
                return true;
        }
        return false;
    }

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex)
    {
        return animators.First(anim => anim).GetCurrentAnimatorClipInfo(layerIndex);
    }

    public void GetCurrentAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
    {
        animators.First(anim => anim).GetCurrentAnimatorClipInfo(layerIndex, clips);
    }

    public int GetCurrentAnimatorClipInfoCount(int layerIndex)
    {
        return animators.First(anim => anim).GetCurrentAnimatorClipInfoCount(layerIndex);
    }

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
    {
        return animators.First(anim => anim).GetCurrentAnimatorStateInfo(layerIndex);
    }

    public float GetFloat(string name)
    {
        return GetFloat(Animator.StringToHash(name));
    }

    public float GetFloat(int id)
    {
        foreach (var animator in animators)
        {
            if (animator)
            {
                float value = animator.GetFloat(id);
                if (value != 0)
                {
                    return value;
                }
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
            if (animator)
            {
                int value = animator.GetInteger(id);
                if (value != 0)
                {
                    return value;
                }
            }

        }
        return 0;
    }

    public int GetLayerCount()
    {
        return animators.First(anim => anim).layerCount;
    }

    public int GetLayerIndex(string layerName)
    {
        return animators.First(anim => anim).GetLayerIndex(layerName);
    }

    public string GetLayerName(int layerIndex)
    {
        return animators.First(anim => anim).GetLayerName(layerIndex);
    }

    public float GetLayerWeight(int layerIndex)
    {
        return animators.First(anim => anim).GetLayerWeight(layerIndex);
    }

    public void GetNextAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips)
    {
        animators.First(anim => anim).GetNextAnimatorClipInfo(layerIndex, clips);
    }

    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex)
    {
        return animators.First(anim => anim).GetNextAnimatorClipInfo(layerIndex);
    }

    public int GetNextAnimatorClipInfoCount(int layerIndex)
    {
        return animators.First(anim => anim).GetNextAnimatorClipInfoCount(layerIndex);
    }

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
    {
        return animators.First(anim => anim).GetNextAnimatorStateInfo(layerIndex);
    }

    public AnimatorControllerParameter GetParameter(int index)
    {
        return animators.First(anim => anim).GetParameter(index);
    }

    public int GetParameterCount()
    {
        return animators.First(anim => anim).parameterCount;
    }

    public bool HasState(int layerIndex, int stateID)
    {
        return animators.First(anim => anim).HasState(layerIndex, stateID);
    }

    public bool IsInTransition(int layerIndex)
    {
        return animators.First(anim => anim).IsInTransition(layerIndex);
    }

    public bool IsParameterControlledByCurve(string name)
    {
        return animators.First(anim => anim).IsParameterControlledByCurve(name);
    }

    public bool IsParameterControlledByCurve(int id)
    {
        return animators.First(anim => anim).IsParameterControlledByCurve(id);
    }

    public void Play(string stateName)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateName);
        }
    }

    public void Play(string stateName, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateName, layer);
        }
    }

    public void Play(string stateName, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateName, layer, normalizedTime);
        }
    }

    public void Play(int stateNameHash)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateNameHash);
        }
    }

    public void Play(int stateNameHash, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateNameHash, layer);
        }
    }

    public void Play(int stateNameHash, int layer, float normalizedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Play(stateNameHash, layer, normalizedTime);
        }
    }

    public void PlayInFixedTime(string stateName)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateName);
        }
    }

    public void PlayInFixedTime(string stateName, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateName, layer);
        }
    }

    public void PlayInFixedTime(string stateName, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateName, layer, fixedTime);
        }
    }

    public void PlayInFixedTime(int stateNameHash)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateNameHash);
        }
    }

    public void PlayInFixedTime(int stateNameHash, int layer)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateNameHash, layer);
        }
    }

    public void PlayInFixedTime(int stateNameHash, int layer, float fixedTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.PlayInFixedTime(stateNameHash, layer, fixedTime);
        }
    }

    public void ResetTrigger(string name)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.ResetTrigger(name);
        }
    }

    public void ResetTrigger(int id)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.ResetTrigger(id);
        }
    }

    public void SetBool(string name, bool value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetBool(name, value);
        }
    }

    public void SetBool(int id, bool value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetBool(id, value);
        }
    }

    public void SetFloat(string name, float value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetFloat(name, value);
        }
    }

    public void SetFloat(int id, float value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetFloat(id, value);
        }
    }

    public void SetInteger(string name, int value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetInteger(name, value);
        }
    }

    public void SetInteger(int id, int value)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetInteger(id, value);
        }
    }

    public void SetLayerWeight(int layerIndex, float weight)
    {
        //foreach (var animator in animators)
        //{
        //    if (animator)
        //        animator.SetLayerWeight(layerIndex, weight);
        //}
    }

    public void SetTrigger(string name)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetTrigger(name);
        }
    }

    public void SetTrigger(int id)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.SetTrigger(id);
        }
    }

    public void Update(float deltaTime)
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.Update(deltaTime);
        }
    }

    public void WriteDefaultValues()
    {
        foreach (var animator in animators)
        {
            if (animator)
                animator.WriteDefaultValues();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public class AnimatorWrapper : IAnimatorWrapper
{
    private Animator animator;

    public bool IsValid => animator;

    public AnimatorWrapper(Animator animator) => this.animator = animator;

    public void CrossFade(string stateName, float transitionDuration) => animator.CrossFade(stateName, transitionDuration);

    public void CrossFade(string stateName, float transitionDuration, int layer) => animator.CrossFade(stateName, transitionDuration, layer);

    public void CrossFade(string stateName, float transitionDuration, int layer, float normalizedTime) => animator.CrossFade(stateName, transitionDuration, layer, normalizedTime);

    public void CrossFade(int stateNameHash, float transitionDuration) => animator.CrossFade(stateNameHash, transitionDuration);

    public void CrossFade(int stateNameHash, float transitionDuration, int layer) => animator.CrossFade(stateNameHash, transitionDuration, layer);

    public void CrossFade(int stateNameHash, float transitionDuration, int layer, float normalizedTime) => animator.CrossFade(stateNameHash, transitionDuration, layer, normalizedTime);

    public void CrossFadeInFixedTime(string stateName, float transitionDuration) => animator.CrossFadeInFixedTime(stateName, transitionDuration);

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer) => animator.CrossFadeInFixedTime(stateName, transitionDuration, layer);

    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer, float fixedTime) => animator.CrossFadeInFixedTime(stateName, transitionDuration, layer, fixedTime);

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration) => animator.CrossFadeInFixedTime(stateNameHash, transitionDuration);

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer) => animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer);

    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer, float fixedTime) => animator.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer, fixedTime);

    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex) => animator.GetAnimatorTransitionInfo(layerIndex);

    public bool GetBool(string name) => animator.GetBool(name);

    public bool GetBool(int id) => animator.GetBool(id);

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex) => animator.GetCurrentAnimatorClipInfo(layerIndex);

    public void GetCurrentAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips) => animator.GetCurrentAnimatorClipInfo(layerIndex, clips);

    public int GetCurrentAnimatorClipInfoCount(int layerIndex) => animator.GetCurrentAnimatorClipInfoCount(layerIndex);

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => animator.GetCurrentAnimatorStateInfo(layerIndex);

    public float GetFloat(string name) => animator.GetFloat(name);

    public float GetFloat(int id) => animator.GetFloat(id);

    public int GetInteger(string name) => animator.GetInteger(name);

    public int GetInteger(int id) => animator.GetInteger(id);

    public int GetLayerCount() => animator.layerCount;

    public int GetLayerIndex(string layerName) => animator.GetLayerIndex(layerName);

    public string GetLayerName(int layerIndex) => animator.GetLayerName(layerIndex);

    public float GetLayerWeight(int layerIndex) => animator.GetLayerWeight(layerIndex);

    public void GetNextAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips) => animator.GetNextAnimatorClipInfo(layerIndex, clips);

    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex) => animator.GetNextAnimatorClipInfo(layerIndex);

    public int GetNextAnimatorClipInfoCount(int layerIndex) => animator.GetNextAnimatorClipInfoCount(layerIndex);

    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) => animator.GetNextAnimatorStateInfo(layerIndex);

    public AnimatorControllerParameter GetParameter(int index) => animator.GetParameter(index);

    public int GetParameterCount() => animator.parameterCount;

    public bool HasState(int layerIndex, int stateID) => animator.HasState(layerIndex, stateID);

    public bool IsInTransition(int layerIndex) => animator.IsInTransition(layerIndex);

    public bool IsParameterControlledByCurve(string name) => animator.IsParameterControlledByCurve(name);

    public bool IsParameterControlledByCurve(int id) => animator.IsParameterControlledByCurve(id);

    public void Play(string stateName) => animator.Play(stateName);

    public void Play(string stateName, int layer) => animator.Play(stateName, layer);

    public void Play(string stateName, int layer, float normalizedTime) => animator.Play(stateName, layer, normalizedTime);

    public void Play(int stateNameHash) => animator.Play(stateNameHash);

    public void Play(int stateNameHash, int layer) => animator.Play(stateNameHash, layer);

    public void Play(int stateNameHash, int layer, float normalizedTime) => animator.Play(stateNameHash, layer, normalizedTime);

    public void PlayInFixedTime(string stateName) => animator.PlayInFixedTime(stateName);

    public void PlayInFixedTime(string stateName, int layer) => animator.PlayInFixedTime(stateName, layer);

    public void PlayInFixedTime(string stateName, int layer, float fixedTime) => animator.PlayInFixedTime(stateName, layer, fixedTime);

    public void PlayInFixedTime(int stateNameHash) => animator.PlayInFixedTime(stateNameHash);

    public void PlayInFixedTime(int stateNameHash, int layer) => animator.PlayInFixedTime(stateNameHash, layer);

    public void PlayInFixedTime(int stateNameHash, int layer, float fixedTime) => animator.PlayInFixedTime(stateNameHash, layer, fixedTime);

    public void ResetTrigger(string name) => animator.ResetTrigger(name);

    public void ResetTrigger(int id) => animator.ResetTrigger(id);

    public void SetBool(string name, bool value) => animator.SetBool(name, value);

    public void SetBool(int id, bool value) => animator.SetBool(id, value);

    public void SetFloat(string name, float value) => animator.SetFloat(name, value);

    public void SetFloat(int id, float value) => animator.SetFloat(id, value);

    public void SetInteger(string name, int value) => animator.SetInteger(name, value);

    public void SetInteger(int id, int value) => animator.SetInteger(id, value);

    public void SetLayerWeight(int layerIndex, float weight) => animator.SetLayerWeight(layerIndex, weight);

    public void SetTrigger(string name) => animator.SetTrigger(name);

    public void SetTrigger(int id) => animator.SetTrigger(id);
    public void Update(float deltaTime) => animator.Update(deltaTime);
    public void WriteDefaultValues()
    {
        animator.WriteDefaultValues();
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class PlayableWrapper : IAnimatorWrapper
{
    private AnimatorControllerPlayable playable;

    public PlayableWrapper(AnimatorControllerPlayable playable)
    {
        this.playable = playable;
    }

    public bool IsValid => playable.IsValid();

    public float GetFloat(string name) => playable.GetFloat(name);
    public float GetFloat(int id) => playable.GetFloat(id);
    public void SetFloat(string name, float value) => playable.SetFloat(name, value);
    public void SetFloat(int id, float value) => playable.SetFloat(id, value);

    public bool GetBool(string name) => playable.GetBool(name);
    public bool GetBool(int id) => playable.GetBool(id);
    public void SetBool(string name, bool value) => playable.SetBool(name, value);
    public void SetBool(int id, bool value) => playable.SetBool(id, value);

    public int GetInteger(string name) => playable.GetInteger(name);
    public int GetInteger(int id) => playable.GetInteger(id);
    public void SetInteger(string name, int value) => playable.SetInteger(name, value);
    public void SetInteger(int id, int value) => playable.SetInteger(id, value);

    public void SetTrigger(string name) => playable.SetTrigger(name);
    public void SetTrigger(int id) => playable.SetTrigger(id);
    public void ResetTrigger(string name) => playable.ResetTrigger(name);
    public void ResetTrigger(int id) => playable.ResetTrigger(id);

    public bool IsParameterControlledByCurve(string name) => playable.IsParameterControlledByCurve(name);
    public bool IsParameterControlledByCurve(int id) => playable.IsParameterControlledByCurve(id);

    public int GetLayerCount() => playable.GetLayerCount();
    public string GetLayerName(int layerIndex) => playable.GetLayerName(layerIndex);
    public int GetLayerIndex(string layerName) => playable.GetLayerIndex(layerName);
    public float GetLayerWeight(int layerIndex) => playable.GetLayerWeight(layerIndex);
    public void SetLayerWeight(int layerIndex, float weight) => playable.SetLayerWeight(layerIndex, weight);

    public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => playable.GetCurrentAnimatorStateInfo(layerIndex);
    public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) => playable.GetNextAnimatorStateInfo(layerIndex);
    public AnimatorTransitionInfo GetAnimatorTransitionInfo(int layerIndex) => playable.GetAnimatorTransitionInfo(layerIndex);

    public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layerIndex) => playable.GetCurrentAnimatorClipInfo(layerIndex);
    public void GetCurrentAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips) => playable.GetCurrentAnimatorClipInfo(layerIndex, clips);
    public void GetNextAnimatorClipInfo(int layerIndex, List<AnimatorClipInfo> clips) => playable.GetNextAnimatorClipInfo(layerIndex, clips);
    public int GetCurrentAnimatorClipInfoCount(int layerIndex) => playable.GetCurrentAnimatorClipInfoCount(layerIndex);
    public int GetNextAnimatorClipInfoCount(int layerIndex) => playable.GetNextAnimatorClipInfoCount(layerIndex);
    public AnimatorClipInfo[] GetNextAnimatorClipInfo(int layerIndex) => playable.GetNextAnimatorClipInfo(layerIndex);

    public bool IsInTransition(int layerIndex) => playable.IsInTransition(layerIndex);

    public int GetParameterCount() => playable.GetParameterCount();
    public AnimatorControllerParameter GetParameter(int index) => playable.GetParameter(index);

    public void CrossFadeInFixedTime(string stateName, float transitionDuration) => playable.CrossFadeInFixedTime(stateName, transitionDuration);
    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer) => playable.CrossFadeInFixedTime(stateName, transitionDuration, layer);
    public void CrossFadeInFixedTime(string stateName, float transitionDuration, int layer, float fixedTime) => playable.CrossFadeInFixedTime(stateName, transitionDuration, layer, fixedTime);
    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration) => playable.CrossFadeInFixedTime(stateNameHash, transitionDuration);
    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer) => playable.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer);
    public void CrossFadeInFixedTime(int stateNameHash, float transitionDuration, int layer, float fixedTime) => playable.CrossFadeInFixedTime(stateNameHash, transitionDuration, layer, fixedTime);

    public void CrossFade(string stateName, float transitionDuration) => playable.CrossFade(stateName, transitionDuration);
    public void CrossFade(string stateName, float transitionDuration, int layer) => playable.CrossFade(stateName, transitionDuration, layer);
    public void CrossFade(string stateName, float transitionDuration, int layer, float normalizedTime) => playable.CrossFade(stateName, transitionDuration, layer, normalizedTime);
    public void CrossFade(int stateNameHash, float transitionDuration) => playable.CrossFade(stateNameHash, transitionDuration);
    public void CrossFade(int stateNameHash, float transitionDuration, int layer) => playable.CrossFade(stateNameHash, transitionDuration, layer);
    public void CrossFade(int stateNameHash, float transitionDuration, int layer, float normalizedTime) => playable.CrossFade(stateNameHash, transitionDuration, layer, normalizedTime);

    public void PlayInFixedTime(string stateName) => playable.PlayInFixedTime(stateName);
    public void PlayInFixedTime(string stateName, int layer) => playable.PlayInFixedTime(stateName, layer);
    public void PlayInFixedTime(string stateName, int layer, float fixedTime) => playable.PlayInFixedTime(stateName, layer, fixedTime);
    public void PlayInFixedTime(int stateNameHash) => playable.PlayInFixedTime(stateNameHash);
    public void PlayInFixedTime(int stateNameHash, int layer) => playable.PlayInFixedTime(stateNameHash, layer);
    public void PlayInFixedTime(int stateNameHash, int layer, float fixedTime) => playable.PlayInFixedTime(stateNameHash, layer, fixedTime);

    public void Play(string stateName) => playable.Play(stateName);
    public void Play(string stateName, int layer) => playable.Play(stateName, layer);
    public void Play(string stateName, int layer, float normalizedTime) => playable.Play(stateName, layer, normalizedTime);
    public void Play(int stateNameHash) => playable.Play(stateNameHash);
    public void Play(int stateNameHash, int layer) => playable.Play(stateNameHash, layer);
    public void Play(int stateNameHash, int layer, float normalizedTime) => playable.Play(stateNameHash, layer, normalizedTime);

    public bool HasState(int layerIndex, int stateID) => playable.HasState(layerIndex, stateID);
    public void Update(float deltaTime) { }

    public void WriteDefaultValues()
    {
        
    }
}


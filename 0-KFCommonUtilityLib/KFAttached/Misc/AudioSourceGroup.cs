using System;
using UnityEngine;

[Serializable]
public class AudioSourceGroup
{
    [SerializeField]
    public string groupName;
    [SerializeField]
    public AudioClip[] clips = new AudioClip[0];
    [SerializeField]
    public AudioSource source;

    public bool IsValid => source != null && clips != null && clips.Length > 0 && source != null;
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[AddComponentMenu("KFAttachments/Utils/Animation Random Sound")]
public class AnimationRandomSound : MonoBehaviour, ISerializationCallbackReceiver
{
    [SerializeField]
    public List<AudioSourceGroup> audioSourcesEditor;
    [NonSerialized]
    private List<AudioSourceGroup> audioSources;

    [HideInInspector]
    [SerializeField]
    private List<string> list_groupnames;
    [HideInInspector]
    [SerializeField]
    private List<AudioClip> list_clips;
    [HideInInspector]
    [SerializeField]
    private List<AudioSource> list_sources;
    [HideInInspector]
    [SerializeField]
    private List<int> list_clip_indices;
    [HideInInspector]
    [SerializeField]
    private int serializedCount = 0;

    public void OnAfterDeserialize()
    {
        audioSources = new List<AudioSourceGroup>();
        for (int i = 0; i < serializedCount; i++)
        {
            int index = (i == 0 ? 0 : list_clip_indices[i - 1]);
            int count = list_clip_indices[i] - index;
            audioSources.Add(new AudioSourceGroup()
            {
                groupName = list_groupnames[i],
                clips = list_clips.Skip(index).Take(count).ToArray(),
                source = list_sources[i],
            });
        }
    }

    public void OnBeforeSerialize()
    {
        if (audioSourcesEditor != null && audioSourcesEditor.Count > 0)
        {
            serializedCount = 0;
            list_groupnames = new List<string>();
            list_clips = new List<AudioClip>();
            list_clip_indices = new List<int>();
            list_sources = new List<AudioSource>();
            for (int i = 0; i < audioSourcesEditor.Count; i++)
            {
                list_groupnames.Add(audioSourcesEditor[i].groupName);
                list_clips.AddRange(audioSourcesEditor[i].clips);
                list_sources.Add(audioSourcesEditor[i].source);
                list_clip_indices.Add(list_clips.Count);
                serializedCount++;
            }
        }
    }

    public void PlayRandomClip(string group)
    {
        if (audioSources == null)
            return;

        //#if NotEditor
        //        Log.Out($"play random clip {group}, groups: {string.Join("| ", audioSources.Select(g => g.groupName + $"clips: {string.Join(", ", g.clips.Select(c => c.name))}"))}");
        //#endif
        AudioSourceGroup asg = null;
        foreach (var audioSourceGroup in audioSources)
        {
            if (audioSourceGroup.groupName == group)
            {
                asg = audioSourceGroup;
                break;
            }
        }

        if (asg == null)
        {
            return;
        }

        int random = Random.Range(0, asg.clips.Length);
        //asg.source.clip = asg.clips[random];
        asg.source.PlayOneShot(asg.clips[random]);
        //#if NotEditor
        //        Log.Out($"play clip {asg.clips[random].name}");
        //#endif
    }
}

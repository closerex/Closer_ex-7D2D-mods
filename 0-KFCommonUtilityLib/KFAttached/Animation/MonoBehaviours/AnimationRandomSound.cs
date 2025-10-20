using KFCommonUtilityLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("KFAttachments/Utils/Animation Random Sound")]
[DisallowMultipleComponent]
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

#if UNITY_EDITOR
    [Header("Rename")]
    public string originalGroupName;
    public string targetGroupName;
    [Header("Data Transfer")]
    public AudioClipCollection moveToCollection;
    public string commonPrefix;
    public string[] namePrefixes;
    [ContextMenu("Rename Group")]
    private void RenameGroup()
    {
        if (audioSourcesEditor != null && audioSourcesEditor.Count > 0 && !string.IsNullOrEmpty(originalGroupName) && !string.IsNullOrEmpty(targetGroupName) && TryGetComponent<Animator>(out var animator))
        {
            Undo.RecordObject(this, "Rename groups");
            foreach (var group in audioSourcesEditor)
            {
                if (group.groupName == originalGroupName)
                {
                    group.groupName = targetGroupName;
                }
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            Undo.RecordObjects(clips, "change event params");
            foreach (var clip in clips)
            {
                var events = clip.events;
                foreach (var ev in events)
                {
                    bool eventFound = false;
                    if (ev.functionName == nameof(PlayRandomClip) && ev.stringParameter == originalGroupName)
                    {
                        ev.stringParameter = targetGroupName;
                        eventFound = true;
                    }
                    if (eventFound)
                    {
                        AnimationUtility.SetAnimationEvents(clip, events);
                        EditorUtility.SetDirty(clip);
                    }
                }
            }
            AssetDatabase.Refresh();
        }
    }

    [ContextMenu("Move To AudioClipCollection")]
    private void MoveToCollection()
    {
        if (moveToCollection == null || audioSourcesEditor == null || audioSourcesEditor.Count == 0)
            return;
        var audioPlayer = Undo.AddComponent<AnimationAudioPlayer>(gameObject);
        audioPlayer.attachableNodes = audioSourcesEditor.Select(static g => g.source.transform.parent).Distinct().ToArray();
        audioPlayer.audioCollectionHolder = FindFirstObjectByType<AudioCollectionHolder>(FindObjectsInactive.Include);
        List<bool> useLRCorrection = audioSourcesEditor.ConvertAll(g =>
        {
            if (g.groupName.EndsWith('@'))
            {
                g.groupName = g.groupName[..^1];
                return true;
            }
            return false;
        });
        var animator = GetComponent<Animator>();
        if (animator)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            Undo.RecordObjects(clips, "change event params");
            foreach (var clip in clips)
            {
                var events = clip.events;
                bool eventFound = false;
                foreach (var ev in events)
                {
                    if (ev.functionName == nameof(PlayRandomClip))
                    {
                        eventFound = true;
                        ev.functionName = nameof(AnimationAudioPlayer.PlayRandomClip);
                        var index = audioSourcesEditor.FindIndex(g => g.groupName == ev.stringParameter || MatchPrefix(ev.stringParameter, g.groupName));
                        if (index >= 0)
                        {
                            var group = audioSourcesEditor[index];
                            ev.intParameter = Array.IndexOf(audioPlayer.attachableNodes, group.source.transform.parent);
                            ev.stringParameter = !string.IsNullOrEmpty(commonPrefix) ? commonPrefix + group.groupName : group.groupName;
                            if (useLRCorrection[index])
                            {
                                ev.stringParameter = ev.stringParameter[..^1];
                            }
                        }
                    }
                }
                if (eventFound)
                {
                    AnimationUtility.SetAnimationEvents(clip, events);
                    EditorUtility.SetDirty(clip);
                }
            }
            AssetDatabase.Refresh();
        }
        
        Undo.RecordObject(moveToCollection, "add groups to collection");
        for (int i = 0; i < audioSourcesEditor.Count; i++)
        {
            AudioSourceGroup group = audioSourcesEditor[i];
            if (!string.IsNullOrEmpty(commonPrefix))
                group.groupName = commonPrefix + group.groupName;
            if (useLRCorrection[i])
            {
                group.groupName = group.groupName[..^1];
            }
            moveToCollection.AddNew(group);
        }

        foreach (var group in audioSourcesEditor)
        {
            if (group.source)
                Undo.DestroyObjectImmediate(group.source.gameObject);
        }

        Undo.DestroyObjectImmediate(this);
    }

    private bool MatchPrefix(string par, string group)
    {
        if (namePrefixes != null && namePrefixes.Length > 0)
        {
            foreach (var prefix in namePrefixes)
            {
                if (prefix + par == group)
                {
                    return true;
                }
            }
        }
        return false;
    }
#endif

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

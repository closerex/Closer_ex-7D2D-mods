using System.Collections.Generic;
using UnityEngine;
#if NotEditor
using Audio;
#endif

namespace KFCommonUtilityLib
{
    [DisallowMultipleComponent]
    public class AnimationAudioPlayer : MonoBehaviour
    {
#if NotEditor
        private EntityPlayerLocal player;
#elif UNITY_EDITOR
        private Dictionary<string, AudioSourceGroup> dict_groups = new();
        private Dictionary<AudioSource, AudioSource> dict_sources = new();
        private Dictionary<AudioSource, float> dict_volumes = new();
        public AudioCollectionHolder audioCollectionHolder;
#endif

        private void Awake()
        {
#if NotEditor
            player = GetComponent<Animator>()?.GetLocalPlayerInParent();
#elif UNITY_EDITOR
            if (audioCollectionHolder != null && audioCollectionHolder.audioClipCollections != null)
            {
                foreach (var collection in audioCollectionHolder.audioClipCollections)
                {
                    if (collection.audioData != null)
                    {
                        foreach (var audioData in collection.audioData)
                        {
                            if (audioData.clips != null && audioData.clips.Length > 0 && audioData.audioSource != null && !string.IsNullOrEmpty(audioData.soundGroupName))
                            {
                                dict_groups[audioData.soundGroupName] = new AudioSourceGroup()
                                {
                                    groupName = audioData.soundGroupName,
                                    clips = audioData.clips,
                                    source = audioData.audioSource.GetComponent<AudioSource>(),
                                    maxVolume = audioData.maxVolume,
                                };
                            }
                        }
                    }
                }
            }
#endif
        }

        // Legacy event integer parameters are ignored; all sounds use this default node.
        public void PlayRandomClip(AnimationEvent par)
        {
#if NotEditor
            if (!player)
            {
                return;
            }
            var handle = Manager.Play(player, par.stringParameter, 1f, true);
            if (handle != null)
            {
                if (handle.nearSource != null)
                {
                    handle.nearSource.transform.SetParent(transform);
                    handle.nearSource.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
                if (handle.farSource != null)
                {
                    handle.farSource.transform.SetParent(transform);
                    handle.farSource.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
#elif UNITY_EDITOR
            if (dict_groups.TryGetValue(par.stringParameter, out var group) && group.IsValid)
            {
                if (!dict_sources.TryGetValue(group.source, out AudioSource source) || source == null)
                {
                    var sourceObj = GameObject.Instantiate(group.source.gameObject, transform);
                    sourceObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    source = sourceObj.GetComponent<AudioSource>();
                    dict_sources[group.source] = source;
                }
                source.spatialBlend = 0f;
                if (dict_volumes.TryGetValue(source, out float volume))
                {
                    source.volume = volume;
                }
                else
                {
                    dict_volumes[source] = source.volume;
                }
                AudioClip clip = group.clips[UnityEngine.Random.Range(0, group.clips.Length)];
                source.volume *= group.maxVolume;
                source.PlayOneShot(clip);
            }
#endif
        }

    }
}

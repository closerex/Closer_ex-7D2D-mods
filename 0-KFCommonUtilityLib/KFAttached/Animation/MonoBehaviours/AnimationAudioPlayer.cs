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
        public Transform[] attachableNodes;

#if NotEditor
        private EntityPlayerLocal player;
#elif UNITY_EDITOR
        private Dictionary<string, AudioSourceGroup> dict_groups = new();
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
                Transform parent = FindParentNode(par.intParameter);
                if (handle.nearSource != null)
                {
                    handle.nearSource.transform.SetParent(parent);
                    handle.nearSource.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
                if (handle.farSource != null)
                {
                    handle.farSource.transform.SetParent(parent);
                    handle.farSource.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
#elif UNITY_EDITOR
            if (dict_groups.TryGetValue(par.stringParameter, out var group) && group.IsValid)
            {
                AudioSource source = null;
                Transform parent = FindParentNode(par.intParameter);
                source = parent.Find(group.source.gameObject.name + "(Clone)")?.GetComponent<AudioSource>();
                if (source == null)
                {
                    var sourceObj = GameObject.Instantiate(group.source.gameObject, parent);
                    sourceObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    source = sourceObj.GetComponent<AudioSource>();
                }
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

        private Transform FindParentNode(int attachableNodeIndex)
        {
            Transform parent = null;
            if (attachableNodeIndex >= 0)
            {
                if (attachableNodes != null && attachableNodes.Length > 0 && attachableNodes.Length > attachableNodeIndex && attachableNodes[attachableNodeIndex])
                {
                    parent = attachableNodes[attachableNodeIndex];
                }
            }

            if (parent == null)
            {
                parent = this.transform;
            }
            return parent;
        }
    }
}

using UnityEngine;
using UnityEngine.Audio;

namespace KFCommonUtilityLib
{
    [CreateAssetMenu(fileName = "Collection", menuName = "KFLibData/CustomAudioSnapshotHolder", order = 1)]
    public class CustomAudioSnapshotHolder : ScriptableObject
    {
        public AudioMixerSnapshot underwaterSnapshot;
        public AudioMixerSnapshot stunnedSnapshot;
        public AudioMixerSnapshot deafenedSnapshot;
        public AudioMixerSnapshot defaultSnapshot;
    }
}

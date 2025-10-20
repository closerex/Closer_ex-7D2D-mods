using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KFCommonUtilityLib
{
    [Serializable]
    public class NoiseData
    {
        public int noiseID = 0;
        public float volume = 50f;
        public float duration = 1f;
        public float muffledWhenCrouched = 1f;
        public float heatMapStrength = 0;
        public ulong heatMapTime = 100;
    }

    [Serializable]
    public class XmlData
    {
        public int maxVoices;
        public float maxRepeatRate;
        public int maxVoicesPerEntity;
        public float localCrouchVolumeScale;
        public float runningVolumeScale;
        public float crouchNoiseScale;
        public float noiseScale;
        public float lowestPitch;
        public float highestPitch;
        public float distantFadeStart;
        public float distantFadeEnd;
        
        public XmlData()
        {
            this.maxVoices = 1;
            this.maxVoicesPerEntity = 5;
            this.localCrouchVolumeScale = 1f;
            this.crouchNoiseScale = 0.5f;
            this.noiseScale = 1f;
            this.maxRepeatRate = 0.001f;
            this.runningVolumeScale = 1f;
            this.lowestPitch = 1f;
            this.highestPitch = 1f;
            this.distantFadeStart = -1f;
            this.distantFadeEnd = -1f;
        }
    }

    [Serializable]
    public class AudioData
    {
        public string soundGroupName;
        public AudioClip[] clips;
        public float maxVolume = 1f;
        public GameObject audioSource;
        public GameObject networkAudioSource;
        public NoiseData noiseData = new();
        public XmlData xmlData = new();
        public bool excludeFromXml = false;
    }

    [CreateAssetMenu(fileName = "Collection", menuName = "KFLibData/AudioClipCollection", order = 1)]
    public class AudioClipCollection : ScriptableObject
    {
        public string BundlePath;
        public List<AudioData> audioData;

        public void AddNew(AudioSourceGroup group)
        {
            if (audioData == null)
                audioData = new List<AudioData>();
            if (audioData.Any(audioData => audioData.soundGroupName == group.groupName))
                return;

            var newData = new AudioData();
            newData.soundGroupName = group.groupName;
            newData.clips = group.clips;
            audioData.Add(newData);
        }

#if UNITY_EDITOR
        [MenuItem("Assets/Audio Collection to Xml")]
        public static void ToXmlData()
        {
            if (Selection.activeObject is AudioClipCollection collection)
            {
                collection.ConvertXmlData();
            }
        }
#endif

        public void ConvertXmlData()
        {
            if (audioData == null || audioData.Count == 0 || string.IsNullOrEmpty(BundlePath))
                return;

            XmlDocument xmlDoc = new XmlDocument();

            XmlElement root = xmlDoc.CreateElement("append");
            root.Attributes.Append(xmlDoc.CreateAttribute("xpath")).Value = "/Sounds";
            xmlDoc.AppendChild(root);

            foreach (var audio in audioData)
            {
                if (audio.excludeFromXml)
                {
                    continue;
                }
                XmlElement soundDataNode = xmlDoc.CreateElement("SoundDataNode");
                soundDataNode.Attributes.Append(xmlDoc.CreateAttribute("name")).Value = audio.soundGroupName;
                root.AppendChild(soundDataNode);

                if (audio.maxVolume != 1)
                {
                    XmlElement maxVolume = xmlDoc.CreateElement("VolumeModifier");
                    maxVolume.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audio.maxVolume.ToString();
                    soundDataNode.AppendChild(maxVolume);
                }

                if (audio.audioSource)
                {
                    string audioSourcePath = $"#@modfolder:{BundlePath}?{audio.audioSource.name}";
                    XmlElement audioSource = xmlDoc.CreateElement("AudioSource");
                    audioSource.Attributes.Append(xmlDoc.CreateAttribute("name")).Value = audioSourcePath;
                    soundDataNode.AppendChild(audioSource);
                }

                if (audio.networkAudioSource)
                {
                    string audioSourcePath = $"#@modfolder:{BundlePath}?{audio.networkAudioSource.name}";
                    XmlElement audioSource = xmlDoc.CreateElement("NetworkAudioSource");
                    audioSource.Attributes.Append(xmlDoc.CreateAttribute("name")).Value = audioSourcePath;
                    soundDataNode.AppendChild(audioSource);
                }

                XmlElement noiseData = xmlDoc.CreateElement("Noise");
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("ID")).Value = audio.noiseData.noiseID.ToString();
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("noise")).Value = audio.noiseData.volume.ToString();
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("time")).Value = audio.noiseData.duration.ToString();
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("muffled_when_crouched")).Value = audio.noiseData.muffledWhenCrouched.ToString();
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("heat_map_strength")).Value = audio.noiseData.heatMapStrength.ToString();
                noiseData.Attributes.Append(xmlDoc.CreateAttribute("heat_map_time")).Value = audio.noiseData.heatMapTime.ToString();
                soundDataNode.AppendChild(noiseData);

                if (audio.clips != null && audio.clips.Length > 0)
                {
                    foreach (var clip in audio.clips)
                    {
                        if (clip)
                        {
                            XmlElement clipData = xmlDoc.CreateElement("AudioClip");
                            clipData.Attributes.Append(xmlDoc.CreateAttribute("ClipName")).Value = $"#@modfolder:{BundlePath}?{clip.name}";
                            soundDataNode.AppendChild(clipData);
                        }
                    }
                }

                XmlData audioXmlData = audio.xmlData;
                XmlElement xmlData = xmlDoc.CreateElement("LocalCrouchVolumeScale");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.localCrouchVolumeScale.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("RunningVolumeScale");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.runningVolumeScale.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("CrouchNoiseScale");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.crouchNoiseScale.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("NoiseScale");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.noiseScale.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("MaxVoices");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.maxVoices.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("MaxVoicesPerEntity");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.maxVoicesPerEntity.ToString();
                soundDataNode.AppendChild(xmlData);

                xmlData = xmlDoc.CreateElement("MaxRepeatRate");
                xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.maxRepeatRate.ToString();
                soundDataNode.AppendChild(xmlData);

                if (audioXmlData.lowestPitch != 1f)
                {
                    xmlData = xmlDoc.CreateElement("LowestPitch");
                    xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.lowestPitch.ToString();
                    soundDataNode.AppendChild(xmlData);
                }

                if (audioXmlData.highestPitch != 1f)
                {
                    xmlData = xmlDoc.CreateElement("HighestPitch");
                    xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.highestPitch.ToString();
                    soundDataNode.AppendChild(xmlData);
                }

                if (audioXmlData.distantFadeStart >= 0)
                {
                    xmlData = xmlDoc.CreateElement("DistantFadeStart");
                    xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.distantFadeStart.ToString();
                    soundDataNode.AppendChild(xmlData);
                }

                if (audioXmlData.distantFadeEnd >= 0)
                {
                    xmlData = xmlDoc.CreateElement("DistantFadeEnd");
                    xmlData.Attributes.Append(xmlDoc.CreateAttribute("value")).Value = audioXmlData.distantFadeEnd.ToString();
                    soundDataNode.AppendChild(xmlData);
                }

                root.AppendChild(soundDataNode);
            }
            using (StringWriter sw = new StringWriter())
            {
                using (XmlWriter xr = XmlWriter.Create(sw, new()
                {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    IndentChars = "\t"
                }))
                {
                    xmlDoc.Save(xr);
                }
                GUIUtility.systemCopyBuffer = sw.ToString();
            }
        }
    }
}

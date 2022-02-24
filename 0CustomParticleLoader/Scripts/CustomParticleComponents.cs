using System;
using System.Collections.Generic;
using UnityEngine;

public struct ExplosionParams
{
    public ExplosionParams(int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _playerId)
    {
        this._clrIdx = _clrIdx;
        this._worldPos = _worldPos;
        this._blockPos = _blockPos;
        this._rotation = _rotation;
        this._explosionData = _explosionData;
        this._playerId = _playerId;
    }

    public int _clrIdx;
    public Vector3 _worldPos;
    public Vector3i _blockPos;
    public Quaternion _rotation;
    public ExplosionData _explosionData;
    public int _playerId;
}
public class CustomParticleComponents
{
    public CustomParticleComponents(GameObject obj, float duration_particle, string sound_name, float duration_audio, List<Type> CustomScriptTypes = null)
    {
        this.obj = obj;
        this.list_custom = new List<Type>();
        foreach (Type type in CustomScriptTypes)
        {
            if (type == null)
                continue;
            if ((type.IsSubclassOf(typeof(TemporaryObject)) || type == typeof(TemporaryObject)))
                this.TempObjType = type;
            else if ((type.IsSubclassOf(typeof(ExplosionDamageArea)) || type == typeof(ExplosionDamageArea)))
                this.ExplAreaType = type;
            else if ((type.IsSubclassOf(typeof(AudioPlayer)) || type == typeof(AudioPlayer)))
                this.AudioType = AudioPlayerType;
            else
                this.list_custom.Add(type);
        }

        this.duration_particle = duration_particle;
        this.sound_name = sound_name;
        this.duration_audio = duration_audio;
        this.cur_params = new ExplosionParams();
        if (this.sound_name != null && this.AudioPlayerType == null)
            this.AudioType = typeof(AudioPlayer);
    }

    private GameObject obj;
    private Type TempObjType = null;
    private Type ExplAreaType = null;
    private Type AudioType = null;
    private List<Type> list_custom;
    private float duration_particle;
    private float duration_audio;
    private string sound_name;
    private ExplosionParams cur_params;
    private ItemValue cur_itemValue;

    public GameObject Particle { get => obj; }
    public Type TemporaryObjectType { get => TempObjType; }
    public Type ExplosionDamageAreaType { get => ExplAreaType; }
    public Type AudioPlayerType { get => AudioType; }
    public List<Type> List_CustomTypes { get => list_custom; }
    public float ParticleDuration { get => duration_particle;}
    public float AudioDuration{ get => duration_audio; }
    public string SoundName { get => sound_name; }
    public ExplosionParams CurrentExplosionParams { get => cur_params; set => cur_params = value; }
    public ItemValue CurrentItemValue { get => cur_itemValue; set => cur_itemValue = value; }
}


using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public struct ExplosionParams
{
    public ExplosionParams(int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _playerId, uint _explId)
    {
        this._clrIdx = _clrIdx;
        this._worldPos = _worldPos;
        this._blockPos = _blockPos;
        this._rotation = _rotation;
        this._explosionData = _explosionData;
        this._playerId = _playerId;
        this._explId = _explId;
    }

    public ExplosionParams(byte[] _explosionParamsAsArr)
    {
        _clrIdx = 0;
        _worldPos = Vector3.zero;
        _blockPos = Vector3i.zero;
        _rotation = Quaternion.identity;
        _explosionData = default(ExplosionData);
        _playerId = -1;
        _explId = uint.MaxValue;
        using (PooledBinaryReader pooledBinaryReader = MemoryPools.poolBinaryReader.AllocSync(false))
        {
            pooledBinaryReader.SetBaseStream(new MemoryStream(_explosionParamsAsArr));
            read(pooledBinaryReader);
        }
    }

    public void read(PooledBinaryReader _br)
    {
        _clrIdx = (int)_br.ReadUInt16();
        _worldPos = StreamUtils.ReadVector3(_br);
        _blockPos = StreamUtils.ReadVector3i(_br);
        _rotation = StreamUtils.ReadQuaterion(_br);
        _explosionData.Read(_br);
        _playerId = _br.ReadInt32();
        _explId = _br.ReadUInt32();
    }

    public void write(PooledBinaryWriter _bw)
    {
        _bw.Write((ushort)_clrIdx);
        StreamUtils.Write(_bw, _worldPos);
        StreamUtils.Write(_bw, _blockPos);
        StreamUtils.Write(_bw, _rotation);
        _explosionData.Write(_bw);
        _bw.Write(_playerId);
        _bw.Write(_explId);
    }

    public byte[] ToByteArray()
    {
        MemoryStream memoryStream = new MemoryStream();
        using (PooledBinaryWriter pooledBinaryWriter = MemoryPools.poolBinaryWriter.AllocSync(false))
        {
            pooledBinaryWriter.SetBaseStream(memoryStream);
            write(pooledBinaryWriter);
        }
        return memoryStream.ToArray();
    }

    public int _clrIdx;
    public Vector3 _worldPos;
    public Vector3i _blockPos;
    public Quaternion _rotation;
    public ExplosionData _explosionData;
    public int _playerId;
    public uint _explId;
}
public class ExplosionComponent
{
    public ExplosionComponent(GameObject obj, string sound_name, float duration_audio, ExplosionData data, List<Type> CustomScriptTypes)
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

        this.sound_name = sound_name;
        this.duration_audio = duration_audio;
        this.hash_custom_properties = new Dictionary<string, object>();
        this.data = data;
        if (this.sound_name != null && this.AudioPlayerType == null)
            this.AudioType = typeof(AudioPlayer);
    }

    private GameObject obj;
    private Type TempObjType = null;
    private Type ExplAreaType = null;
    private Type AudioType = null;
    private List<Type> list_custom;
    private float duration_audio;
    private string sound_name;
    private ExplosionData data;
    private Dictionary<string, object> hash_custom_properties;

    public bool TryGetCustomProperty(string name, out object value)
    {
        return hash_custom_properties.TryGetValue(name, out value);
    }

    internal void AddCustomProperty(string name, object value)
    {
        if(hash_custom_properties.Remove(name))
            Log.Out("Custom explosion component property already exists, overwriting: " + name);
        hash_custom_properties.Add(name, value);
    }

    public GameObject Particle { get => obj; }
    public Type TemporaryObjectType { get => TempObjType; }
    public Type ExplosionDamageAreaType { get => ExplAreaType; }
    public Type AudioPlayerType { get => AudioType; }
    public List<Type> List_CustomTypes { get => list_custom; }
    public float AudioDuration{ get => duration_audio; }
    public string SoundName { get => sound_name; }
    public ExplosionData BoundExplosionData { get => data; }
    public ItemClass BoundItemClass { get; set; }
    public bool SyncOnConnect { get; set; } = false;
}

public class ExplosionValue
{
    public ExplosionComponent Component { get; set; }
    public ExplosionParams CurrentExplosionParams { get; set; }
    public ItemValue CurrentItemValue { get; set; }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public class NetSyncHelper : MonoBehaviour
{
    public event Action<PooledBinaryWriter> ClientConnected
    {
        add { list_connect_server_handlers.Add(value); }
        remove { list_connect_server_handlers.Remove(value); }
    }
    public event Action<PooledBinaryReader> ConnectedToServer
    {
        add { list_connect_client_handlers.Add(value); }
        remove { list_connect_client_handlers.Remove(value); }
    }
    public event Action<PooledBinaryWriter> ExplosionServerInit
    {
        add { list_init_server_handlers.Add(value); }
        remove { list_init_server_handlers.Remove(value); }
    }
    public event Action<PooledBinaryReader> ExplosionClientInit
    {
        add { list_init_client_handlers.Add(value); }
        remove { list_init_client_handlers.Remove(value); }
    }
    private List<Action<PooledBinaryWriter>> list_connect_server_handlers = new List<Action<PooledBinaryWriter>>();
    private List<Action<PooledBinaryReader>> list_connect_client_handlers = new List<Action<PooledBinaryReader>>();
    private List<Action<PooledBinaryWriter>> list_init_server_handlers = new List<Action<PooledBinaryWriter>>();
    private List<Action<PooledBinaryReader>> list_init_client_handlers = new List<Action<PooledBinaryReader>>();
    public ExplosionParams explParams;
    public ItemValue explValue;
    public uint explId;
    private static Dictionary<uint, NetSyncHelper> hash_helpers = new Dictionary<uint, NetSyncHelper>();

    static NetSyncHelper()
    {
        CustomExplosionManager.CleanUp += hash_helpers.Clear;
    }

    void Awake()
    {
        hash_helpers.Add((explId = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._explId), this);
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && CustomExplosionManager.LastInitializedComponent.Component.SyncOnConnect)
        {
            explParams = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams;
            explValue = CustomExplosionManager.LastInitializedComponent.CurrentItemValue?.Clone();
            CustomExplosionManager.ClientConnected += OnClientConnected;
        }
    }

    public static bool TryGetValue(uint id, out NetSyncHelper helper)
    {
        return hash_helpers.TryGetValue(id, out helper);
    }

    void OnDestroy()
    {
        CustomExplosionManager.ClientConnected -= OnClientConnected;
        hash_helpers.Remove(explId);
    }

    void OnClientConnected(PooledBinaryWriter _bw)
    {
        explParams._worldPos = transform.position + Origin.position;
        explParams._rotation = transform.rotation;
        byte[] array = explParams.ToByteArray();
        _bw.Write((ushort)array.Length);
        _bw.Write(array);
        _bw.Write(explValue != null);
        if (explValue != null)
        {
            explValue.Write(_bw);
        }
        foreach (var handler in list_connect_server_handlers)
            handler(_bw);
    }

    public void OnConnectedToServer(PooledBinaryReader _br)
    {
        foreach (var handler in list_connect_client_handlers)
            handler(_br);
    }

    public void OnExplosionServerInit(PooledBinaryWriter _bw)
    {
        foreach (var handler in list_init_server_handlers)
            handler(_bw);
    }

    public void OnExplosionClientInit(PooledBinaryReader _br)
    {
        foreach (var handler in list_init_client_handlers)
            handler(_br);
    }
}


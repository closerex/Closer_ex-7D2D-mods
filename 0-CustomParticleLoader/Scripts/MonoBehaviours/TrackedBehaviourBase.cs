using UnityEngine;

public class TrackedBehaviourBase : MonoBehaviour
{
    protected uint explId;
    protected bool isServer;
    protected object key = null;
    protected bool syncOnConnect = false;
    protected bool syncOnInit = false;
    protected bool track = false;
    protected bool handleClientInfo = false;
    protected NetSyncHelper helper = null;

    protected virtual void Awake()
    {
        explId = CustomExplosionManager.LastInitializedComponent.CurrentExplosionParams._explId;
        if (track)
        {
            if (key == null)
            {
                Log.Error("TrackedBehaviourBase: key is not initialized before addRef!");
                return;
            }
            addRef();
        }
        bool flag = NetSyncHelper.TryGetValue(explId, out helper);
        isServer = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
        if (flag)
        {
            if (syncOnConnect)
            {
                if (isServer)
                    helper.ClientConnected += OnClientConnected;
                else
                    helper.ConnectedToServer += OnConnectedToServer;
            }
            if (syncOnInit)
            {
                if (isServer)
                    helper.ExplosionServerInit += OnExplosionInitServer;
                else
                    helper.ExplosionClientInit += OnExplosionInitClient;
            }
        }else
            Log.Error("NetSyncHelper not initialized: explId: " + explId);
        if (handleClientInfo)
            CustomExplosionManager.HandleClientInfo += OnHandleClientInfo;
    }

    protected virtual void OnDestroy()
    {
        if (helper != null)
        {
            if (syncOnConnect)
            {
                if (isServer)
                    helper.ClientConnected -= OnClientConnected;
                else
                    helper.ConnectedToServer -= OnConnectedToServer;
            }
            if (syncOnInit)
            {
                if (isServer)
                    helper.ExplosionServerInit -= OnExplosionInitServer;
                else
                    helper.ExplosionClientInit -= OnExplosionInitClient;
            }
        }
        if (handleClientInfo)
            CustomExplosionManager.HandleClientInfo -= OnHandleClientInfo;
        if (track)
            removeRef();
    }
    protected virtual void addRef()
    {
    }
    protected virtual void removeRef()
    {
    }
    protected virtual void OnClientConnected(PooledBinaryWriter _bw)
    {
    }
    protected virtual void OnConnectedToServer(PooledBinaryReader _br)
    {
    }
    protected virtual void OnExplosionInitServer(PooledBinaryWriter _bw)
    {
    }
    protected virtual void OnExplosionInitClient(PooledBinaryReader _br)
    {
    }
    protected virtual void OnHandleClientInfo(ClientInfo info)
    {
    }
}


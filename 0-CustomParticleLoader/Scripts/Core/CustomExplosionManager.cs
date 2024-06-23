using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CustomExplosionManager
{
    private static Dictionary<int, string> hash_paths = new Dictionary<int, string>();
    private static Dictionary<string, ExplosionComponent> hash_components = new Dictionary<string, ExplosionComponent>();
    private static Dictionary<string, GameObject> hash_assets = new Dictionary<string, GameObject>();
    private static HashSet<GameObject> hash_initialized = new HashSet<GameObject>();
    private static List<IExplosionPropertyParser> list_parsers = new List<IExplosionPropertyParser>();
    private static Stack<ExplosionValue> last_init_components = new Stack<ExplosionValue>();

    public static event Action<PooledBinaryWriter> ClientConnected;

    public static event Action<ClientInfo> HandleClientInfo;

    public static event Action CleanUp;

    public static ExplosionValue LastInitializedComponent
    {
        get => last_init_components.Count > 0 ? last_init_components.Peek() : null;
    }

    public static uint NextExplosionIndex { get; set; } = 0;

    internal static void OnCleanUp()
    {
        Log.Out("Custom Explosion Manager cleanup...");
        destroyAllParticles();
        NextExplosionIndex = 0;
        CleanUp?.Invoke();
    }

    internal static void OnClientConnected(ClientInfo client)
    {
        var handler = ClientConnected;
        if (handler != null)
        {
            uint count = (uint)ClientConnected.GetInvocationList().Length;
            MemoryStream memoryStream = new MemoryStream();
            using (PooledBinaryWriter _bw = MemoryPools.poolBinaryWriter.AllocSync(false))
            {
                _bw.SetBaseStream(memoryStream);
                _bw.Write(count);
                handler(_bw);
                if (HandleClientInfo != null)
                    HandleClientInfo(client);
            }
            byte[] data = memoryStream.ToArray();
            client.SendPackage(NetPackageManager.GetPackage<NetPackageExplosionSyncOnConnect>().Setup(data));
        }
    }

    internal static void CreatePropertyParsers()
    {
        var assemblies = ModManager.GetLoadedAssemblies();
        string iname = nameof(IExplosionPropertyParser);

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.GetInterface(iname) != null)
                {
                    var parser = Activator.CreateInstance(type) as IExplosionPropertyParser;
                    list_parsers.Add(parser);
                    Log.Out("Found custom explosion property parser: " + type.Name);
                }
            }
        }
    }

    internal static GameObject InitializeParticle(ExplosionComponent component, Vector3 position, Quaternion rotation)
    {
        GameObject __result = UnityEngine.Object.Instantiate<GameObject>(component.Particle, position, rotation);
        __result.AddComponent<NetSyncHelper>();
        if (component.TemporaryObjectType != null)
            __result.AddComponent(component.TemporaryObjectType);
        if (component.ExplosionDamageAreaType != null)
            __result.AddComponent(component.ExplosionDamageAreaType);
        if (component.AudioPlayerType != null)
        {
            AudioPlayer audio_script = __result.AddComponent(component.AudioPlayerType) as AudioPlayer;
            if (component.SoundName != null)
                audio_script.soundName = component.SoundName;
            if (component.AudioDuration >= 0)
                audio_script.duration = component.AudioDuration;
        }
        if (component.List_CustomTypes.Count > 0)
            foreach (Type customtype in component.List_CustomTypes)
                if (customtype != null)
                    __result.AddComponent(customtype);
        AutoRemove remove_script = __result.AddComponent<AutoRemove>();
        CustomExplosionManager.addInitializedParticle(__result);
        return __result;
    }

    internal static void PushLastInitComponent(ExplosionValue component)
    {
        last_init_components.Push(component);
    }

    internal static void PopLastInitComponent()
    {
        if (last_init_components.Count > 0)
            last_init_components.Pop();
    }

    private static bool LoadParticleEffect(string fullpath, ExplosionData data, out ExplosionComponent component, string sound_name = null, float duration_audio = -1, List<Type> CustomScriptList = null)
    {
        component = null;
        fullpath = fullpath.Trim();
        if (!parsePathString(fullpath, out string path, out string assetname))
            return false;
        //check if asset is loaded
        string path_asset = path + "?" + assetname;
        bool flag = hash_assets.TryGetValue(path_asset, out GameObject obj);
        if (!flag)
        {
            //load asset
            string path_bundle = ModManager.PatchModPathString(path).TrimStart('#');
            Log.Out("Bundle path: " + path_bundle);
            AssetBundleManager.Instance.LoadAssetBundle(path_bundle);
            obj = AssetBundleManager.Instance.Get<GameObject>(path_bundle, assetname);
            if (obj == null)
                Log.Error("Failed to load asset " + assetname);
            else
                hash_assets.Add(path_asset, obj);
        }

        if (obj == null)
        {
            Log.Error("Particle not loaded:" + path_asset);
            return false;
        }

        //pair particle with scripts
        if (hash_components.Remove(fullpath))
            Log.Out("Particle data already exists:" + fullpath + ", now overwriting");
        component = new ExplosionComponent(obj, sound_name, duration_audio, data, CustomScriptList);
        hash_components.Add(fullpath, component);
        return true;
    }

    //this should get a unique index for each particle
    public static int getHashCode(string str)
    {
        int value = (PlatformIndependentHash.StringToUInt16(str));

        while (hash_paths.TryGetValue(value, out string path))
        {
            if (path == str)
                break;
            if (value > Int16.MaxValue || (value >= 0 && value < WorldStaticData.prefabExplosions.Length))
                value = WorldStaticData.prefabExplosions.Length;
            else
                value++;
        }
        return value;
    }

    private static bool parsePathString(string fullpath, out string path, out string assetname)
    {
        //get path to asset
        path = null;
        assetname = null;
        if (fullpath == null)
        {
            Log.Error("Null fullpath parameter:" + fullpath);
            return false;
        }
        int index_path = fullpath.IndexOf('?');
        if (index_path <= 0)
        {
            Log.Error("Particle path does not specify the asset name! fullpath:" + fullpath);
            return false;
        }
        path = fullpath.Substring(0, index_path);
        int index_postfix = fullpath.IndexOf('$');
        if (index_postfix <= 0)
            assetname = fullpath.Substring(index_path + 1);
        else
            assetname = fullpath.Substring(index_path + 1, index_postfix - index_path - 1);
        return true;
    }

    private static void getTypeListFromString(string str, out List<Type> list_types)
    {
        list_types = new List<Type>();
        if (str == null || str == string.Empty)
            return;
        string[] array = str.Split(new char[] { '$' });
        foreach (string typename in array)
        {
            if (typename != null)
            {
                Type type = Type.GetType(typename.Trim());
                if (type != null)
                {
                    if (!list_types.Contains(type))
                        list_types.Add(type);
                    else
                        Log.Out("duplicated script type detected: " + type.AssemblyQualifiedName);
                }
                else
                    Log.Warning("CustomScriptType not found:" + typename);
            }
        }
    }

    public static bool parseParticleData(DynamicProperties _props, out ExplosionComponent component)
    {
        string str_index = null;
        component = null;
        _props.ParseString("Explosion.ParticleIndex", ref str_index);
        if (str_index != null && str_index.StartsWith("#"))
        {
            Log.Out("Original path:" + str_index);
            int hashed_index = getHashCode(str_index);
            _props.Values["Explosion.ParticleIndex"] = hashed_index.ToString();
            Log.Out("Hashed index:" + _props.Values["Explosion.ParticleIndex"]);
            if (!hash_paths.ContainsKey(hashed_index))
                hash_paths.Add(hashed_index, str_index);
            bool overwrite = false;
            _props.ParseBool("Explosion.Overwrite", ref overwrite);
            if (overwrite || !GetCustomParticleComponents(hashed_index, out _))
            {
                string sound_name = null;
                _props.ParseString("Explosion.AudioName", ref sound_name);
                float duration_audio = -1;
                _props.ParseFloat("Explosion.AudioDuration", ref duration_audio);
                bool sync = false;
                _props.ParseBool("Explosion.SyncOnConnect", ref sync);
                bool observe = false;
                _props.ParseBool("Explosion.IsChunkObserver", ref observe);
                getTypeListFromString(_props.Values["Explosion.CustomScriptTypes"], out List<Type> list_customtypes);
                bool flag = LoadParticleEffect(str_index, new ExplosionData(_props), out component, sound_name, duration_audio, list_customtypes);
                if (flag && component != null)
                {
                    component.SyncOnConnect = sync;
                    foreach (var parser in list_parsers)
                        if (list_customtypes.Contains(parser.MatchScriptType()) && parser.ParseProperty(_props, out var property))
                            component.AddCustomProperty(parser.Name(), property);
                }
                return flag;
            }
        }
        return false;
    }

    internal static void addInitializedParticle(GameObject obj)
    {
        hash_initialized.Add(obj);
        //Log.Out("Particle initialized:" + obj.name);
    }

    internal static void removeInitializedParticle(GameObject obj)
    {
        hash_initialized.Remove(obj);
        //Log.Out("Particle removed on destroy:" + obj.name);
    }

    internal static void destroyAllParticles()
    {
        foreach (GameObject obj in hash_initialized)
        {
            Log.Out("Active particle destroyed on disconnect:" + obj.name);
            GameObject.Destroy(obj);
        }
        hash_initialized.Clear();
        /*
        foreach (var pair in hash_assets)
        {
            Log.Out("Loaded particle destroyed on disconnect:" + pair.Value.name);
            GameObject.Destroy(pair.Value);
        }
        hash_assets.Clear();
        hash_components.Clear();
        Log.Out("Loaded particle data cleared on disconnect.");
        */
        last_init_components.Clear();
    }

    public static bool GetCustomParticleComponents(int index, out ExplosionComponent component)
    {
        component = null;
        if (!hash_paths.TryGetValue(index, out string fullpath) || fullpath == null)
            return false;
        return hash_components.TryGetValue(fullpath, out component);
    }
}
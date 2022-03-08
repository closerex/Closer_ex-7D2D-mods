using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEngine;

public class CustomParticleEffectLoader
{
    private static Dictionary<int, string> hash_paths = new Dictionary<int, string>();
    private static Dictionary<string, CustomParticleComponents> hash_effects = new Dictionary<string, CustomParticleComponents>();
    private static Dictionary<string, GameObject> hash_assets = new Dictionary<string, GameObject>();
    private static Dictionary<string, AssetBundle> hash_bundles = new Dictionary<string, AssetBundle>();
    private static HashSet<GameObject> hash_initialized = new HashSet<GameObject>();
    private static CustomParticleComponents last_initialized_component = null;

    public static CustomParticleComponents LastInitializedComponent { get => last_initialized_component; set => last_initialized_component = value; }

    private static bool LoadParticleEffect(string fullpath, float duration_particle = -1, string sound_name = null, float duration_audio = -1, List<Type> CustomScriptList = null)
    {
        CustomParticleComponents component = null;
        if (!parsePathString(fullpath, out string path, out string assetname))
            return false;
        //check if asset is loaded
        string path_asset = path + "?" + assetname;
        bool flag = hash_assets.TryGetValue(path_asset, out GameObject obj);
        if(!flag)
        {
            //load asset
            string path_bundle = ModManager.PatchModPathString(path).TrimStart('#');
            Log.Out("Bundle path: " + path_bundle);
            flag = hash_bundles.TryGetValue(path, out AssetBundle bundle);
            if (!flag)
            {
                bundle = AssetBundle.LoadFromFile(path_bundle);
                if(bundle == null)
                {
                    Log.Error("Failed to load AssetBundle from file:" + path_bundle);
                    return false;
                }
                hash_bundles.Add(path, bundle);
            }
            obj = bundle.LoadAsset<GameObject>(assetname);
            if (obj == null)
                Log.Error("Failed to load asset " + assetname);
            else
                hash_assets.Add(path_asset, obj);
        }

        if(obj == null)
        {
            Log.Error("Particle not loaded:" + path_asset);
            return false;
        }

        //pair particle with scripts
        if (hash_effects.Remove(fullpath))
            Log.Out("Particle data already exists:" + fullpath + ", now overwriting");
        component = new CustomParticleComponents(obj, duration_particle, sound_name, duration_audio, CustomScriptList);
        hash_effects.Add(fullpath, component);
        return true;
    }

    //this should get a unique index for each particle
    private static int getHashCode(string str)
    {
        byte[] encoded = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(str));
        int value = (int)(BitConverter.ToUInt32(encoded, 0) % Int16.MaxValue);

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
        if(fullpath == null)
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
                Type type = Type.GetType(typename);
                if (type != null)
                    list_types.Add(type);
                else
                    Log.Warning("CustomScriptType not found:" + typename);
            }
        }
    }

    public static void parseParticleData(ref DynamicProperties _props)
    {
        string str_index = null;
        _props.ParseString("Explosion.ParticleIndex", ref str_index);
        if (str_index != null && str_index.StartsWith("#"))
        {
            Log.Out("Original path:" + str_index);
            int hashed_index = getHashCode(str_index);
            _props.Values["Explosion.ParticleIndex"] = hashed_index.ToString();
            Log.Out("Hashed index:" + _props.Values["Explosion.ParticleIndex"]);
            if(!hash_paths.ContainsKey(hashed_index))
                hash_paths.Add(hashed_index, str_index);
            bool overwrite = false;
            _props.ParseBool("Explosion.Overwrite", ref overwrite);
            if (overwrite || !GetCustomParticleComponents(hashed_index, out CustomParticleComponents components))
            {
                float duration = -1;
                _props.ParseFloat("Explosion.Duration", ref duration);
                string sound_name = null;
                _props.ParseString("Explosion.AudioName", ref sound_name);
                float duration_audio = -1;
                _props.ParseFloat("Explosion.AudioDuration", ref duration_audio);

                getTypeListFromString(_props.Values["Explosion.CustomScriptTypes"], out List<Type> list_customtypes);
                LoadParticleEffect(str_index, duration, sound_name, duration_audio, list_customtypes);
            }
        }
    }

    public static void addInitializedParticle(GameObject obj)
    {
        hash_initialized.Add(obj);
        //Log.Out("Particle initialized:" + obj.name);
    }

    public static void removeInitializedParticle(GameObject obj)
    {
        hash_initialized.Remove(obj);
        //Log.Out("Particle removed on destroy:" + obj.name);
    }

    public static void destroyAllParticles()
    {
        foreach (GameObject obj in hash_initialized)
        {
            Log.Out("Active particle destroyed on disconnect:" + obj.name);
            GameObject.Destroy(obj);
        }
        hash_initialized.Clear();
        foreach (KeyValuePair<string, AssetBundle> pair in hash_bundles)
        {
            Log.Out("Unloading bundle on disconnect:" + pair.Key);
            pair.Value.Unload(true);
        }
        hash_bundles.Clear();
        hash_assets.Clear();
        hash_effects.Clear();
        Log.Out("Loaded particle data cleared on disconnect.");
        last_initialized_component = null;
    }

    public static bool GetCustomParticleComponents(int index, out CustomParticleComponents component)
    {
        component = null;
        if (!hash_paths.TryGetValue(index, out string fullpath) || fullpath == null)
            return false;
        return hash_effects.TryGetValue(fullpath, out component);
    }

    private CustomParticleEffectLoader() { }
}


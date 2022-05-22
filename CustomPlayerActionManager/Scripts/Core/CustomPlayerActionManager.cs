using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

public class CustomPlayerActionManager
{
    private static Dictionary<string, CustomPlayerActionVersionBase> dict_action_sets = new Dictionary<string, CustomPlayerActionVersionBase>();
    private static readonly string saveName = "/ActionSetSaves.pref";
    private static string saveFile;

    public static void InitCustomControls()
    {
        if (GameManager.IsDedicatedServer)
            return;
        InitFolderPath();
        LoadCustomActionSets();
        LoadCustomControlSaves();
        SaveCustomControls();
    }

    private static void InitFolderPath()
    {
        Assembly assembly = Assembly.GetAssembly(typeof(CustomPlayerActionManager));
        Mod mod = ModManager.GetModForAssembly(assembly);
        saveFile = mod.Path + saveName;
    }

    private static void LoadCustomActionSets()
    {
        var assemblies = ModManager.GetLoadedAssemblies();
        Type baseType = typeof(CustomPlayerActionVersionBase);
        foreach(var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach(var type in types)
            {
                if(type.IsSubclassOf(baseType))
                {
                    var actionSet = Activator.CreateInstance(type) as CustomPlayerActionVersionBase;
                    dict_action_sets.Add(actionSet.Name, actionSet);
                    Log.Out("Found custom player action set: " + actionSet.Name);
                }
            }
        }
        foreach (var pair in dict_action_sets)
            pair.Value.InitActionSetRelations();
    }

    private static void LoadCustomControlSaves()
    {
        if (!File.Exists(saveFile))
            return;
        string content = File.ReadAllText(saveFile);
        string[] perModData = content.Split(';');
        foreach(string data in perModData)
        {
            if (!LoadSaveData(Convert.FromBase64String(data), out string info))
                Log.Warning(info);
        }
    }

    private static bool LoadSaveData(byte[] data, out string info)
    {
        info = string.Empty;
        if(data == null)
        {
            info = "No savedata to read!";
            return false;
        }

        using (MemoryStream stream = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                string name = reader.ReadString();
                int version = reader.ReadInt32();
                if(dict_action_sets.TryGetValue(name, out var actionSet))
                {
                    if (version == (actionSet as CustomPlayerActionVersionBase).Version)
                        actionSet.LoadData(reader.ReadBytes(reader.ReadInt32()));
                    else
                    {
                        info = "Action Set version changed: " + name + ", reset key mapping";
                        return false;
                    }
                }else
                {
                    info = "Action Set not found: " + name;
                    return false;
                }
            }
        }
        return true;
    }

    public static void SaveCustomControls()
    {
        if (dict_action_sets.Count <= 0)
            return;

        string[] data = new string[dict_action_sets.Count];
        int i = 0;
        foreach(var pair in dict_action_sets)
        {
            byte[] result = pair.Value.SaveData();
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value.Version);
                    writer.Write(result.Length);
                    writer.Write(result);
                }
                result = stream.ToArray();
            }
            data[i++] = Convert.ToBase64String(result);
        }
        string saveData = string.Join(";", data);
        File.WriteAllText(saveFile, saveData);
    }

    public static void ResetCustomControls()
    {
        foreach(var pair in dict_action_sets)
            pair.Value.Reset();
        SaveCustomControls();
    }

    public static PlayerActionsBase[] CreateActionArray(PlayerActionsBase[] origin)
    {
        Log.Out("Initializing custom option control panel");
        List<PlayerActionsBase> result = new List<PlayerActionsBase>();
        result.AddRange(origin);
        result.AddRange(dict_action_sets.Values);
        return result.ToArray();
    }

    public static void StoreCurrentCustomBindings(List<string> origin)
    {
        foreach (var pair in dict_action_sets)
            origin.Add(pair.Value.Save());
    }

    public static string CreateDebugInfo(string origin)
    {
        foreach(var pair in dict_action_sets)
            origin += string.Format("{0} ({1}), ", pair.Value.GetType().Name, pair.Value.Enabled);
        return origin;
    }

    public static bool TryGetCustomActionSetByName(string name, out CustomPlayerActionVersionBase value)
    {
        return dict_action_sets.TryGetValue(name, out value);
    }
}
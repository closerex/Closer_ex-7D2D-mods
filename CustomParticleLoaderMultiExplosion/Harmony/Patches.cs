using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch]
public class CustomParticleLoaderMultiExplosionPatches
{
    public static readonly string str_sub_explosion = "Explosion.SubExplosion";
    public static readonly string str_sub_explosion_transform = "Explosion.SubExplosionTransform";
    //public static readonly string str_sub_explosion_force_server = "Explosion.SubExplosionForceServerSide";

    /*
    public static Transform getSubTransform(Transform root, string indice)
    {
        if (indice == null || indice.Length < 1)
            return root;
        Transform trans = root;
        string[] arr_trans = indice.Split(',');
        for(int i = 0; i < arr_trans.Length; ++i)
        {
            if (trans == null)
                break;
            trans = trans.gameObject.transform.GetChild(int.Parse(arr_trans[i]));
        }
        return trans;
    }
    */

    [HarmonyPatch(typeof(CustomParticleEffectLoader), nameof(CustomParticleEffectLoader.parseParticleData))]
    [HarmonyPostfix]
    public static void Postfix_parseParticleData_CustomParticleEffectLoader(ref DynamicProperties _props, bool __result)
    {
        if (!__result)
            return;

        string sub_explosion = null;
        string[] arr_sub_explosions = null;
        List<int> list_sub_explosion_indice = new List<int>();
        _props.ParseString(str_sub_explosion, ref sub_explosion);
        if(sub_explosion != null)
        {
            arr_sub_explosions = sub_explosion.Split(',');
            foreach(string str in arr_sub_explosions)
            {
                if(str.StartsWith("#"))
                {
                    int index = CustomParticleEffectLoader.getHashCode(str.Trim());
                    if (index >= WorldStaticData.prefabExplosions.Length)
                        list_sub_explosion_indice.Add(index);
                }
            }
        }
        var cur_component = CustomParticleEffectLoader.LastInitializedComponent;
        if (cur_component != null && list_sub_explosion_indice.Count > 0)
        {
            cur_component.AddCustomProperty(str_sub_explosion, list_sub_explosion_indice);
            Log.Out("Adding subexplosion data: " + sub_explosion);
        }else
            return;
        
        string sub_transform = null;
        string[] arr_transforms = null;
        _props.ParseString(str_sub_explosion_transform, ref sub_transform);
        arr_transforms = sub_transform.Split(',');
        cur_component.AddCustomProperty(str_sub_explosion_transform, arr_transforms);
        Log.Out("Adding transform: " + string.Join(" ", arr_transforms));
        /*
        bool serverside = false;
        _props.ParseBool(str_sub_explosion_force_server, ref serverside);
        if (serverside)
            cur_component.AddCustomProperty(str_sub_explosion_force_server, serverside);
        */
    }
}

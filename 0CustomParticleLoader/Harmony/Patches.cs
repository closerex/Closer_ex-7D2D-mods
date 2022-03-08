using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("ExplosionClient")]
public class ParticlePatch
{
    private static void Postfix(GameManager __instance, ref GameObject __result, Vector3 _center, Quaternion _rotation, int _index, int _blastPower, float _blastRadius)
    {
        if (__result != null || __instance.World == null)
            return;

        CustomParticleComponents components = CustomParticleEffectLoader.LastInitializedComponent;
        if(components == null && SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient && _index >= WorldStaticData.prefabExplosions.Length)
        {
            bool flag = CustomParticleEffectLoader.GetCustomParticleComponents(_index, out components);
            if (!flag || components == null)
                Log.Warning("Failed to retrieved particle! Index:" + _index.ToString());
        }
        if (components != null)
        {
            __result = UnityEngine.Object.Instantiate<GameObject>(components.Particle, _center - Origin.position, _rotation);
            if (components.TemporaryObjectType != null)
                __result.AddComponent(components.TemporaryObjectType);
            if (components.ExplosionDamageAreaType != null)
                __result.AddComponent(components.ExplosionDamageAreaType);
            if (components.AudioPlayerType != null)
            {
                AudioPlayer audio_script =  __result.AddComponent(components.AudioPlayerType) as AudioPlayer;
                if (components.SoundName != null)
                    audio_script.soundName = components.SoundName;
                if (components.AudioDuration >= 0)
                    audio_script.duration = components.AudioDuration;
            }
            if (components.List_CustomTypes.Count > 0)
                foreach (Type customtype in components.List_CustomTypes)
                    if(customtype != null)
                        __result.AddComponent(customtype);
            AutoRemove remove_script = __result.AddComponent<AutoRemove>();
            if (components.TemporaryObjectType == null && components.ParticleDuration >= 0)
                remove_script.lifetime = components.ParticleDuration;
            CustomParticleEffectLoader.addInitializedParticle(__result);
            ApplyExplosionForce.Explode(_center, (float)_blastPower, _blastRadius);
        }
    }
}

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("Disconnect")]
public class DisconnectPatch
{
    private static void Postfix()
    {
        CustomParticleEffectLoader.destroyAllParticles();
    }
}

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("explode")]
class ExplosionServerPatch
{
    private static bool Prefix(int _clrIdx, Vector3 _worldPos, Vector3i _blockPos, Quaternion _rotation, ExplosionData _explosionData, int _playerId, ItemValue _itemValueExplosive, out bool __state)
    {
        __state = false;
        int index = _explosionData.ParticleIndex;
        //Log.Out("Particle index:" + index.ToString());
        if (index >= WorldStaticData.prefabExplosions.Length)
        {
            //Log.Out("Retrieving particle index:" + index.ToString());
            bool flag = CustomParticleEffectLoader.GetCustomParticleComponents(index, out CustomParticleComponents components);
            if(flag && components != null)
            {
                //Log.Out("Retrieved particle index:" + index.ToString());
                components.CurrentExplosionParams = new ExplosionParams(_clrIdx, _worldPos, _blockPos, _rotation, _explosionData, _playerId);
                //Log.Out("params:" + _clrIdx + _blockPos + _playerId + _rotation + _worldPos + _explosionData.ParticleIndex);
                //Log.Out("params:" + components.CurrentExplosionParams._clrIdx + components.CurrentExplosionParams._blockPos + components.CurrentExplosionParams._playerId + components.CurrentExplosionParams._rotation + components.CurrentExplosionParams._worldPos + components.CurrentExplosionParams._explosionData.ParticleIndex);
                components.CurrentItemValue = _itemValueExplosive;
                CustomParticleEffectLoader.LastInitializedComponent = components;
                __state = true;
            }else
                Log.Warning("Failed to retrieved particle! Index:" + index.ToString());
        }
        return true;
    }

    private static void Postfix(bool __state)
    {
        if (!__state)
            return;
        //CustomParticleEffectLoader.LastInitializedComponent = null;
    }
}

[HarmonyPatch(typeof(ItemClass))]
[HarmonyPatch("Init")]
class BombParsePatch
{
    private static bool Prefix(ItemClass __instance)
    {
        CustomParticleEffectLoader.parseParticleData(ref __instance.Properties);
        return true;
    }
}

[HarmonyPatch(typeof(ItemAction))]
[HarmonyPatch("ReadFrom")]
class ProjectileParsePatch
{
    private static bool Prefix(ref DynamicProperties _props)
    {
        CustomParticleEffectLoader.parseParticleData(ref _props);
        return true;
    }
}

[HarmonyPatch(typeof(Block))]
[HarmonyPatch("Init")]
class BlockParsePatch
{
    private static bool Prefix(Block __instance)
    {
        CustomParticleEffectLoader.parseParticleData(ref __instance.Properties);
        return true;
    }
}

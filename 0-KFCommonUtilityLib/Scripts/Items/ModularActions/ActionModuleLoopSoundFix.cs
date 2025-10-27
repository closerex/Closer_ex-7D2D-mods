using Audio;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using XmlData = Audio.XmlData;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(LoopSoundFixData))]
public class ActionModuleLoopSoundFix
{
    public float loopSegTimeOverride;
    public float acceptableError;

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ItemActionEffects(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_stop = AccessTools.Method(typeof(Manager), nameof(Manager.StopSequence));
        var mtd_play = AccessTools.Method(typeof(Manager), nameof(Manager.PlaySequence));
        var prop_time = AccessTools.PropertyGetter(typeof(AudioSettings), nameof(AudioSettings.dspTime));
        var type_datamodule = typeof(IModuleContainerFor<LoopSoundFixData>);
        var type_data = typeof(LoopSoundFixData);
        var prop_instance = AccessTools.PropertyGetter(type_datamodule, nameof(IModuleContainerFor<LoopSoundFixData>.Instance));
        var mtd_delaystop = AccessTools.Method(type_data, nameof(LoopSoundFixData.StopSequenceDelayed));

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_stop))
            {
                codes[i].operand = mtd_delaystop;
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Castclass, type_datamodule),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance)
                });
                i += 3;
            }
            else if (codes[i].Calls(mtd_play))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Castclass, type_datamodule),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Call, prop_time),
                    CodeInstruction.StoreField(type_data, nameof(LoopSoundFixData.loopStartTime)),
                });
                i += 5;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_ExecuteAction(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_call = AccessTools.Method(typeof(Inventory), nameof(Inventory.CallOnToolbeltChangedInternal));
        var type_datamodule = typeof(IModuleContainerFor<LoopSoundFixData>);
        var type_data = typeof(LoopSoundFixData);
        var prop_instance = AccessTools.PropertyGetter(type_datamodule, nameof(IModuleContainerFor<LoopSoundFixData>.Instance));
        var prop_time = AccessTools.PropertyGetter(typeof(AudioSettings), nameof(AudioSettings.dspTime));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_call))
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Castclass, type_datamodule),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance),
                    new CodeInstruction(OpCodes.Call, prop_time),
                    CodeInstruction.StoreField(type_data, nameof(LoopSoundFixData.lastShotTime))
                });
                break;
            }
        }

        return codes;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ReloadGun))]
    [HarmonyPatch(typeof(ItemActionLauncher), nameof(ItemActionLauncher.ReloadGun))]
    [HarmonyPatch(typeof(ItemActionCatapult), nameof(ItemActionCatapult.ReloadGun))]
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.StopHolding))]
    [MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_DelayStopSequence(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_stop = AccessTools.Method(typeof(Manager), nameof(Manager.StopSequence));
        var type_datamodule = typeof(IModuleContainerFor<LoopSoundFixData>);
        var type_data = typeof(LoopSoundFixData);
        var prop_instance = AccessTools.PropertyGetter(type_datamodule, nameof(IModuleContainerFor<LoopSoundFixData>.Instance));
        var mtd_delaystop = AccessTools.Method(type_data, nameof(LoopSoundFixData.StopSequenceDelayed));

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_stop))
            {
                codes[i].operand = mtd_delaystop;
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Castclass, type_datamodule),
                    new CodeInstruction(OpCodes.Callvirt, prop_instance)
                });
                i += 3;
            }
        }

        return codes;
    }

    [HarmonyPatch(nameof(ItemActionRanged.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(LoopSoundFixData __customData)
    {
        __customData.loopStartTime = -1;
        __customData.lastShotTime = -1;
    }

    [HarmonyPatch(nameof(ItemActionRanged.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(LoopSoundFixData __customData)
    {
        __customData.loopStartTime = -1;
        __customData.lastShotTime = -1;
    }

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(ItemActionRanged __instance)
    {
        loopSegTimeOverride = -1f;
        __instance.Properties.ParseFloat("LoopSegmentLength", ref  loopSegTimeOverride);
        acceptableError = 0.05f;
        __instance.Properties.ParseFloat("AcceptableError", ref acceptableError);
    }

    public class LoopSoundFixData
    {
        public double loopStartTime;
        public double lastShotTime;
        public ItemActionRanged.ItemActionDataRanged rangedData;
        public ActionModuleLoopSoundFix module;

        public LoopSoundFixData(ItemActionRanged.ItemActionDataRanged __instance, ActionModuleLoopSoundFix __customModule)
        {
            rangedData = __instance;
            module = __customModule;
        }

        public static void StopSequenceDelayed(Entity entity, string soundGroupName, LoopSoundFixData data)
        {
            if (data.loopStartTime >= 0f)
            {
                DelayedStopSequence(entity, soundGroupName, data.module.loopSegTimeOverride > 0f ? data.module.loopSegTimeOverride : data.rangedData.Delay, data.module.acceptableError);
            }
            Manager.StopSequence(entity, soundGroupName);
        }

        private static IEnumerator DelayedDestroyPrevSeq(Manager.SequenceStopper sequenceStopper)
        {
            while (Time.time < sequenceStopper.stopTime)
            {
                yield return null;
            }
            for (int i = 0; i < sequenceStopper.sequenceObjs.Count; i++)
            {
                global::UnityEngine.Object.Destroy(sequenceStopper.sequenceObjs[i]);
                //AudioPoolManager.PoolObject(sequenceStopper.sequenceObjs[i]);
            }
        }

        private static void DelayedStopSequence(Entity entity, string soundGroupName, double loopSegTime, double acceptableError)
        {
            if (GameManager.IsDedicatedServer || soundGroupName == null)
            {
                return;
            }
            Manager.ConvertName(ref soundGroupName, entity);
            Dictionary<string, Manager.SequenceGOs> dictionary;
            Manager.SequenceGOs sequenceGOs;
            if (Manager.sequenceOnEntity.TryGetValue(entity.entityId, out dictionary) && dictionary.TryGetValue(soundGroupName, out sequenceGOs))
            {
                AudioSource nearAudioSource = null;
                AudioSource farAudioSource = null;
                double dspTime = AudioSettings.dspTime;
                float delay = 0;
                if (sequenceGOs.nearLoop)
                {
                    nearAudioSource = sequenceGOs.nearLoop.GetComponent<AudioSource>();
                    if (nearAudioSource)
                    {
                        double sampleTime = nearAudioSource.timeSamples / (double)nearAudioSource.clip.frequency;
                        double actualDelay = loopSegTime - sampleTime % loopSegTime;
                        if (actualDelay < loopSegTime * (1 - acceptableError))
                        {
                            nearAudioSource.SetScheduledEndTime(dspTime + actualDelay);
                            delay = Mathf.Max(delay, (float)actualDelay);
                        }
                        else
                        {
                            nearAudioSource.Stop();
                        }
                        Manager.RemovePlayingAudioSource(nearAudioSource);
                    }
                }
                if (sequenceGOs.farLoop)
                {
                    farAudioSource = sequenceGOs.farLoop.GetComponent<AudioSource>();
                    if (farAudioSource)
                    {
                        double sampleTime = farAudioSource.timeSamples / (double)farAudioSource.clip.frequency;
                        double actualDelay = loopSegTime - sampleTime % loopSegTime;
                        if (actualDelay < loopSegTime * (1 - acceptableError))
                        {
                            farAudioSource.SetScheduledEndTime(dspTime + actualDelay);
                            delay = Mathf.Max(delay, (float)actualDelay);
                        }
                        else
                        {
                            farAudioSource.Stop();
                        }
                        Manager.RemovePlayingAudioSource(farAudioSource);
                    }
                }
                dictionary.Remove(soundGroupName);

                if (!entity)
                {
                    return;
                }
                if (sequenceGOs.nearEnd || sequenceGOs.farEnd)
                {
                    AudioSource audioSourceNearEnd = (sequenceGOs.nearEnd ? sequenceGOs.nearEnd.GetComponent<AudioSource>() : null);
                    AudioSource audioSourceFarEnd = (sequenceGOs.farEnd ? sequenceGOs.farEnd.GetComponent<AudioSource>() : null);
                    XmlData xmlData;
                    if (Manager.audioData.TryGetValue(soundGroupName, out xmlData) && !xmlData.playImmediate)
                    {
                        //float offset = (float)(delay > 0 ? loopSegTime - delay : loopSegTime);
                        if (audioSourceNearEnd)
                        {
                            audioSourceNearEnd.volume *= Manager.CalculateOcclusion(entity.position - Origin.position, Manager.currentListenerPosition);
                            audioSourceNearEnd.Play();
                            Manager.AddPlayingAudioSource(audioSourceNearEnd);
                        }
                        if (audioSourceFarEnd)
                        {
                            audioSourceFarEnd.volume *= Manager.CalculateOcclusion(entity.position - Origin.position, Manager.currentListenerPosition);
                            audioSourceFarEnd.Play();
                            Manager.AddPlayingAudioSource(audioSourceFarEnd);
                        }
                    }
                    Dictionary<string, Manager.SequenceStopper> stoppedSequences;
                    Manager.SequenceStopper sequenceStopper;
                    if (Manager.stoppedEntitySequences.TryGetValue(entity.entityId, out stoppedSequences))
                    {
                        if (stoppedSequences.TryGetValue(soundGroupName, out sequenceStopper))
                        {
                            ThreadManager.StartCoroutine(DelayedDestroyPrevSeq(sequenceStopper));
                            stoppedSequences.Remove(soundGroupName);
                        }
                    }
                    else
                    {
                        stoppedSequences = new Dictionary<string, Manager.SequenceStopper>();
                        Manager.stoppedEntitySequences.Add(entity.entityId, stoppedSequences);
                    }
                    if (!stoppedSequences.TryGetValue(soundGroupName, out sequenceStopper))
                    {
                        List<GameObject> list = new List<GameObject>();
                        if (sequenceGOs.nearStart)
                        {
                            list.Add(sequenceGOs.nearStart);
                        }
                        if (sequenceGOs.nearLoop)
                        {
                            list.Add(sequenceGOs.nearLoop);
                        }
                        if (sequenceGOs.nearEnd)
                        {
                            list.Add(sequenceGOs.nearEnd);
                        }
                        if (sequenceGOs.farStart)
                        {
                            list.Add(sequenceGOs.farStart);
                        }
                        if (sequenceGOs.farLoop)
                        {
                            list.Add(sequenceGOs.farLoop);
                        }
                        if (sequenceGOs.farEnd)
                        {
                            list.Add(sequenceGOs.farEnd);
                        }
                        sequenceStopper = new Manager.SequenceStopper(list, (float)(Time.time + Mathf.Max(delay * 2, sequenceGOs.longestClipLength)));
                        stoppedSequences.Add(soundGroupName, sequenceStopper);
                    }
                }
            }
        }
    }
}

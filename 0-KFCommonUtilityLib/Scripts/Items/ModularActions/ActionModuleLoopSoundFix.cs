using Audio;
using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using XmlData = Audio.XmlData;

[TypeTarget(typeof(ItemActionRanged)), TypeDataTarget(typeof(LoopSoundFixData))]
public class ActionModuleLoopSoundFix
{

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
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.LoadField(typeof(LoopSoundFixPatches), nameof(LoopSoundFixPatches.LastScheduledNearSourcePlayTime)),
                    CodeInstruction.StoreField(type_data, nameof(LoopSoundFixData.nearScheduledTime)),
                    CodeInstruction.LoadField(typeof(LoopSoundFixPatches), nameof(LoopSoundFixPatches.LastScheduledFarSourcePlayTime)),
                    CodeInstruction.StoreField(type_data, nameof(LoopSoundFixData.farScheduledTime)),
                    new CodeInstruction(OpCodes.Call, prop_time),
                    CodeInstruction.StoreField(type_data, nameof(LoopSoundFixData.loopStartTime))
                });
                i += 11;
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

    [HarmonyPatch(nameof(ItemActionRanged.onHoldingEntityFired)), MethodTargetPostfix]
    public void Postfix_onHoldingEntityFired(LoopSoundFixData __customData)
    {
        __customData.shotCount++;
    }

    [HarmonyPatch(nameof(ItemActionRanged.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(LoopSoundFixData __customData)
    {
        __customData.Reset();
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemAction __instance, ItemActionData _data, LoopSoundFixData __customData)
    {
        int actionIndex = _data.indexInEntityOfAction;
        string originalValue = "0";
        __instance.Properties.ParseString("LoopSegmentLength", ref originalValue);
        __customData.loopSegTimeOverride = double.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("LoopSegmentLength", originalValue, actionIndex));
        originalValue = "0.05";
        __instance.Properties.ParseString("AcceptableError", ref originalValue);
        __customData.acceptableError = double.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("AcceptableError", originalValue, actionIndex));
        originalValue = "0";
        __instance.Properties.ParseString("OptimalLoopEndShift", ref originalValue);
        __customData.optimalLoopEndShift = Math.Clamp(double.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("OptimalLoopEndShift", originalValue, actionIndex)), 0, 1);
        originalValue = "true";
        __instance.Properties.ParseString("EnableLoopSoundFix", ref originalValue);
        __customData.enabled = bool.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("EnableLoopSoundFix", originalValue, actionIndex));
        __customData.Reset();
    }

    public class LoopSoundFixData
    {
        public double loopSegTimeOverride;
        public double acceptableError;
        public double optimalLoopEndShift;
        public bool enabled;

        public double loopStartTime;
        public double lastShotTime;
        public double nearScheduledTime;
        public double farScheduledTime;
        public int shotCount;
        public ItemActionRanged.ItemActionDataRanged rangedData;
        public ActionModuleLoopSoundFix module;
        private static bool debugLog = false;

        public LoopSoundFixData(ItemActionRanged.ItemActionDataRanged __instance, ActionModuleLoopSoundFix __customModule)
        {
            rangedData = __instance;
            module = __customModule;
        }

        public void Reset()
        {
            loopStartTime = -1;
            lastShotTime = -1;
            nearScheduledTime = -1;
            farScheduledTime = -1;
            shotCount = 0;
        }

        public static void StopSequenceDelayed(Entity entity, string soundGroupName, LoopSoundFixData data)
        {
            if (debugLog)
            {
                Log.Out($"[LoopSoundFix] StopSequenceDelayed called for entity {entity.entityId}, soundGroupName: {soundGroupName}, loopStartTime: {data.loopStartTime}, shotCount: {data.shotCount}");
            }
            if (data.enabled && data.loopStartTime >= 0f && data.shotCount > 0)
            {
                DelayedStopSequence(entity, soundGroupName, data.nearScheduledTime, data.farScheduledTime, data.loopSegTimeOverride > 0f ? data.loopSegTimeOverride : data.rangedData.Delay, data.optimalLoopEndShift, data.shotCount, data.acceptableError);
            }
            else
            {
                Manager.StopSequence(entity, soundGroupName);
            }
            data.Reset();
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

        private static void DelayedStopSequence(Entity entity, string soundGroupName, double nearScheduledStartTime, double farScheduledStartTime, double loopSegTime, double optimalEndShift, int expectedShots, double acceptableError)
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
                double nearEndScheduledTime = -1, farEndScheduledTime = -1;
                float delay = 0;
                if (sequenceGOs.nearLoop)
                {
                    nearAudioSource = sequenceGOs.nearLoop.GetComponent<AudioSource>();
                    DelayStopLoop(sequenceGOs.nearStart ? sequenceGOs.nearStart.GetComponent<AudioSource>() : null, nearAudioSource, nearScheduledStartTime, loopSegTime, optimalEndShift, expectedShots, acceptableError, out nearEndScheduledTime);
                }
                if (sequenceGOs.farLoop)
                {
                    farAudioSource = sequenceGOs.farLoop.GetComponent<AudioSource>();
                    DelayStopLoop(sequenceGOs.farStart ? sequenceGOs.farStart.GetComponent<AudioSource>() : null, farAudioSource, farScheduledStartTime, loopSegTime, optimalEndShift, expectedShots, acceptableError, out farEndScheduledTime);
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
                            if (nearEndScheduledTime > 0)
                            {
                                audioSourceNearEnd.PlayScheduled(nearEndScheduledTime);
                                delay = Mathf.Max(delay, (float)(nearEndScheduledTime - AudioSettings.dspTime));
                            }
                            else
                            {
                                audioSourceNearEnd.Play();
                            }
                            Manager.AddPlayingAudioSource(audioSourceNearEnd);
                        }
                        if (audioSourceFarEnd)
                        {
                            audioSourceFarEnd.volume *= Manager.CalculateOcclusion(entity.position - Origin.position, Manager.currentListenerPosition);
                            if (farEndScheduledTime > 0)
                            {
                                audioSourceFarEnd.PlayScheduled(farEndScheduledTime);
                                delay = Mathf.Max(delay, (float)(farEndScheduledTime - AudioSettings.dspTime));
                            }
                            else
                            {
                                audioSourceFarEnd.Play();
                            }
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
                        float maxStopClipLength = 0f;
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
                            maxStopClipLength = Mathf.Max(maxStopClipLength, sequenceGOs.nearEnd.GetComponent<AudioSource>().clip.length + .5f);
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
                            maxStopClipLength = Mathf.Max(maxStopClipLength, sequenceGOs.farEnd.GetComponent<AudioSource>().clip.length + .5f);
                        }
                        sequenceStopper = new Manager.SequenceStopper(list, (float)(Time.time + delay + maxStopClipLength));
                        stoppedSequences.Add(soundGroupName, sequenceStopper);
                    }
                }
            }
        }

        private static void DelayStopLoop(AudioSource startSource, AudioSource loopSource, double scheduledStartTime, double loopSegTime, double optimalEndShift, int expectedShots, double acceptableError, out double scheduledEndTime)
        {
            scheduledEndTime = -1;
            if (loopSource)
            {
                optimalEndShift = loopSegTime * optimalEndShift;
                double dspTime = scheduledStartTime <= 0 ? AudioSettings.dspTime : scheduledStartTime;
                double sampleTime = loopSource.timeSamples / (double)loopSource.clip.frequency;
                double actualDelay = loopSegTime - sampleTime % loopSegTime;
                int pendingShots = (int)(sampleTime / loopSegTime) + 1;
                double startDelay = 0, burstShotDelay = 0, startClipLength = 0;
                if (startSource)
                {
                    startClipLength = startSource.clip.samples / (double)startSource.clip.frequency / 2;
                    if (startClipLength > 0)
                    {
                        if (scheduledStartTime > 0)
                        {
                            startDelay = sampleTime;
                        }
                        else if (startSource.isPlaying)
                        {
                            startDelay = Mathf.Max(startSource.clip.samples / 2 - startSource.timeSamples, 0) / (double)startSource.clip.frequency;
                        }
                        int delayedShots = Mathf.Min(Mathf.CeilToInt((float)startClipLength / (float)loopSegTime), expectedShots - 1);
                        burstShotDelay = Mathf.Clamp(expectedShots - pendingShots, 0, delayedShots) * loopSegTime;
                    }
                }
                if (startClipLength <= 0)
                {
                    burstShotDelay = Mathf.Clamp01(expectedShots - pendingShots) * loopSegTime;
                }
                if (sampleTime == 0 || startDelay > 0 || burstShotDelay > 0 || actualDelay < loopSegTime * (1 - acceptableError))
                {
                    double totalDelay = startDelay + burstShotDelay + actualDelay;
                    scheduledEndTime = dspTime + totalDelay;
                    loopSource.SetScheduledEndTime(scheduledEndTime);
                    if (totalDelay > optimalEndShift)
                    {
                        scheduledEndTime -= optimalEndShift;
                    }
                    else
                    {
                        scheduledEndTime = -1;
                    }
                    if (debugLog)
                    {
                        Log.Out($"[LoopSoundFix] sampleTime: {sampleTime}, scheduledStartTime: {scheduledStartTime}, actualDelay: {actualDelay}, startDelay: {startDelay}, burstShotDelay: {burstShotDelay}, loopSegTime: {loopSegTime}, expectedShots: {expectedShots}, pendingShots: {pendingShots}");
                    }
                }
                else
                {
                    loopSource.Stop();
                }
                Manager.RemovePlayingAudioSource(loopSource);
            }
        }
    }
}

[HarmonyPatch]
public static class LoopSoundFixPatches
{
    public static double LastScheduledNearSourcePlayTime = -1f;
    public static double LastScheduledFarSourcePlayTime = -1f;
    
    [HarmonyPatch(typeof(Manager), nameof(Manager.PlaySequence))]
    [HarmonyPrefix]
    private static void Prefix_PlaySequence_Manager()
    {
        LastScheduledFarSourcePlayTime = LastScheduledNearSourcePlayTime = -1f;
    }

    [HarmonyPatch(typeof(Manager), nameof(Manager.PlaySequence))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_PlaySequence_Manager(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var mtd_play = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayScheduled));
        var fld_near = AccessTools.Field(typeof(Manager.SequenceGOs), nameof(Manager.SequenceGOs.nearLoop));
        var fld_far = AccessTools.Field(typeof(Manager.SequenceGOs), nameof(Manager.SequenceGOs.farLoop));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_near))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].Calls(mtd_play))
                    {
                        codes.InsertRange(j + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldloc_S, codes[j - 1].operand),
                            CodeInstruction.StoreField(typeof(LoopSoundFixPatches), nameof(LoopSoundFixPatches.LastScheduledNearSourcePlayTime))
                        });
                        i += 2;
                        break;
                    }
                }
            }
            else if (codes[i].StoresField(fld_far))
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (codes[j].Calls(mtd_play))
                    {
                        codes.InsertRange(j + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldloc_S, codes[j - 1].operand),
                            CodeInstruction.StoreField(typeof(LoopSoundFixPatches), nameof(LoopSoundFixPatches.LastScheduledFarSourcePlayTime))
                        });
                        i += 2;
                        break;
                    }
                }
            }
        }

        return codes;
    }
}

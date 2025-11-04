using Audio;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;
using System.Reflection;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class AudioPatches
    {
        #region volume patches
        private static readonly CaseInsensitiveStringDictionary<float> dict_volume_modifiers = new();
        private static bool showDebugInfo = false;

        private static float GetVolumeModifier(string soundGroupName)
        {
            if (!string.IsNullOrEmpty(soundGroupName) && dict_volume_modifiers.TryGetValue(soundGroupName, out float modifier))
            {
                return modifier;
            }
            return 1f;
        }

        [HarmonyPatch(typeof(Manager), nameof(Manager.Reset))]
        [HarmonyPostfix]
        private static void Postfix_Reset_Manager()
        {
            dict_volume_modifiers.Clear();
        }

        [HarmonyPatch(typeof(SoundsFromXml), nameof(SoundsFromXml.Parse))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Parse_SoundsFromXml(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 5)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, 4),
                        new CodeInstruction(OpCodes.Ldloc_1),
                        CodeInstruction.CallClosure<Action<XElement, string>>(static (element, soundGroupName) =>
                        {
                            if (!string.IsNullOrEmpty(soundGroupName) && element.Name.LocalName == "VolumeModifier" && float.TryParse(element.GetAttribute("value"), out float value))
                            {
                                dict_volume_modifiers[soundGroupName] = value;
                            }
                        })
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(Manager), nameof(Manager.Play), new[] { typeof(Entity), typeof(string), typeof(float), typeof(bool) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Play_Entity_Manager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_signalai = AccessTools.Method(typeof(Manager), nameof(Manager.SignalAI));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 11)
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i + 1].ExtractLabels()),
                        new CodeInstruction(OpCodes.Ldloc_S, 8),
                        new CodeInstruction(OpCodes.Ldloc_S, 10),
                        CodeInstruction.CallClosure<Action<string, AudioSource, AudioSource>>(static (soundGroupName, nearAudioSource, farAudioSource) =>
                        {
                            if (!string.IsNullOrEmpty(soundGroupName))
                            {
                                float modifier = GetVolumeModifier(soundGroupName);
                                if (nearAudioSource)
                                {
                                    nearAudioSource.volume *= modifier;
                                    if (showDebugInfo)
                                        Log.Out($"set near audio source volume to {nearAudioSource.volume} group {soundGroupName} modifier {modifier}");
                                }
                                if (farAudioSource)
                                {
                                    farAudioSource.volume *= modifier;
                                    if (showDebugInfo)
                                        Log.Out($"set far audio source volume to {farAudioSource.volume} group {soundGroupName} modifier {modifier}");
                                }
                            }
                        }),
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch]
        private static class PlayPositionPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                if (Constants.cVersionInformation.Major >= 2 && Constants.cVersionInformation.Minor >= 1)
                    yield return AccessTools.Method(typeof(Manager), nameof(Manager.Play), new[] { typeof(Vector3), typeof(string), typeof(int), typeof(bool) });
                else
                    yield return AccessTools.Method(typeof(Manager), nameof(Manager.Play), new[] { typeof(Vector3), typeof(string), typeof(int) });
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                var codes = instructions.ToList();

                var propset_loop = AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.loop));
                var propget_loop = AccessTools.PropertyGetter(typeof(AudioSource), nameof(AudioSource.loop));

                var lbd_modifier = generator.DeclareLocal(typeof(float));

                for (int i = 1; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 5 && codes[i - 1].opcode == OpCodes.Ldnull)
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i + 1].ExtractLabels()),
                            CodeInstruction.Call(typeof(AudioPatches), nameof(GetVolumeModifier)),
                            new CodeInstruction(OpCodes.Stloc_S, lbd_modifier)
                        });
                        i += 3;
                    }
                    else if (codes[i].Calls(propset_loop))
                    {
                        OpCode opcode = default;
                        object par = null;
                        bool insFound = false;
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (codes[j].Calls(propget_loop))
                            {
                                opcode = codes[j - 1].opcode;
                                par = codes[j - 1].operand;
                                insFound = true;
                                break;
                            }
                        }

                        if (insFound)
                        {
                            codes.InsertRange(i + 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_1),
                                new CodeInstruction(opcode, par).WithLabels(codes[i + 1].ExtractLabels()),
                                CodeInstruction.CallClosure<Action<string, AudioSource>>(static (soundGroupName, audioSource) =>
                                {
                                    if (!string.IsNullOrEmpty(soundGroupName))
                                    {
                                        float modifier = GetVolumeModifier(soundGroupName);
                                        if (audioSource)
                                        {
                                            audioSource.volume *= modifier;
                                            if (showDebugInfo)
                                                Log.Out($"set audio source volume to {audioSource.volume} group {soundGroupName} modifier {modifier}");
                                        }
                                    }
                                }),
                            });
                            i += 3;
                        }
                    }
                }

                return codes;
            }
        }

        [HarmonyPatch(typeof(Manager), nameof(Manager.PlaySequence))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_PlaySequence_Manager(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_loadaudio = AccessTools.Method(typeof(Manager), nameof(Manager.LoadAudio));
            var mtd_containskey = AccessTools.Method(typeof(Dictionary<string, Manager.SequenceGOs>), nameof(Dictionary<string, Manager.SequenceGOs>.ContainsKey));

            var lbd_modifier = generator.DeclareLocal(typeof(float));

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].Calls(mtd_loadaudio))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_modifier),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        CodeInstruction.CallClosure<Func<AudioSource, float, string, AudioSource>>(static (audioSource, modifier, soundGroupName) =>
                        {
                            if (audioSource)
                            {
                                audioSource.volume *= modifier;
                                if (showDebugInfo)
                                    Log.Out($"set sequence audio source volume to {audioSource.volume} group {soundGroupName} modifier {modifier}");
                            }
                            return audioSource;
                        })
                    });
                    i += 3;
                }
                else if (codes[i].Calls(mtd_containskey) && codes[i + 1].Branches(out _))
                {
                    codes.InsertRange(i + 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_1).WithLabels(codes[i + 2].ExtractLabels()),
                        CodeInstruction.Call(typeof(AudioPatches), nameof(GetVolumeModifier)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_modifier)
                    });
                    i += 3;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(Manager), nameof(Manager.PlayInsidePlayerHead), new[] { typeof(string), typeof(int) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_PlayInsidePlayerHead_Loop_Manager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var propset_loop = AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.loop));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(propset_loop))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, 4).WithLabels(codes[i + 1].ExtractLabels()),
                        new CodeInstruction(OpCodes.Ldloc_S, 6),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        CodeInstruction.CallClosure<Action<AudioSource, AudioSource, string, string>>(static (audioSourceBegin, audioSourceLoop, soundGroupNameBegin, soundGroupNameLoop) => 
                        {
                            float beginModifier = GetVolumeModifier(soundGroupNameBegin);
                            audioSourceBegin.volume *= beginModifier;
                            float loopModifier = GetVolumeModifier(soundGroupNameLoop);
                            audioSourceLoop.volume *= loopModifier;
                            if (showDebugInfo)
                                Log.Out($"set begin audio source volume to {audioSourceBegin.volume} group {soundGroupNameBegin} modifier {beginModifier}, loop audio source volume to {audioSourceLoop} group {soundGroupNameLoop} modifier {soundGroupNameLoop}");
                        })
                    });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(Manager), nameof(Manager.PlayInsidePlayerHead), new[] { typeof(string), typeof(int), typeof(float), typeof(bool), typeof(bool) })]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_PlayInsidePlayerHead_OneShot_Manager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_loadaudio = AccessTools.Method(typeof(Manager), nameof(Manager.LoadAudio));

            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_loadaudio))
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        CodeInstruction.CallClosure<Func<AudioSource, string, AudioSource>>(static (audioSource, soundGroupName) =>
                        {
                            if (audioSource)
                            {
                                float modifier = GetVolumeModifier(soundGroupName);
                                audioSource.volume *= modifier;
                                if (showDebugInfo)
                                    Log.Out($"set local audio source volume to {audioSource.volume} group {soundGroupName} modifier {modifier}");
                            }
                            return audioSource;
                        })
                    });
                    break;
                }
            }

            return codes;
        }
        #endregion

        #region bug fixes
        [HarmonyPatch]
        public static class AudioSourceImplicitPatches
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return new[]
                {
                    AccessTools.Method(typeof(Audio.Handle), nameof(Audio.Handle.IsPlaying)),
                    AccessTools.Method(typeof(TriggerEffectManager), nameof(TriggerEffectManager.Update)),
                    AccessTools.Method(typeof(TriggerEffectManager), nameof(TriggerEffectManager.SetAudioRumbleSource)),
                    AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(PlayAndCleanup), nameof(PlayAndCleanup.StopBeginWhenDone)))
                };
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                var mtd_inequality = AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality", new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) });
                var mtd_implicit = AccessTools.Method(typeof(UnityEngine.Object), "op_Implicit", new[] { typeof(UnityEngine.Object) });
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(mtd_inequality))
                    {
                        codes[i].operand = mtd_implicit;
                        codes.RemoveAt(i - 1);
                        i--;
                    }
                }
                return codes;
            }
        }
        //[HarmonyPatch(typeof(ItemAction), nameof(ItemAction.HandleItemBreak))]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_HandleItemBreak_ItemAction(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = instructions.ToList();

        //    var mtd_broadcast = AccessTools.Method(typeof(Manager), nameof(Manager.BroadcastPlay), new[] { typeof(Entity), typeof(string), typeof(bool) });

        //    for (int i = 0; i < codes.Count; i++)
        //    {
        //        if (codes[i].Calls(mtd_broadcast))
        //        {
        //            codes.InsertRange(i + 1, new[]
        //            {
        //                new CodeInstruction(OpCodes.Ldarg_1),
        //                CodeInstruction.CallClosure<Action<ItemActionData>>(static (actionData) =>
        //                {
        //                    var rangedData = actionData as ItemActionRanged.ItemActionDataRanged;
        //                    if (rangedData == null)
        //                    {
        //                        return;
        //                    }
        //                    rangedData.invData.gameManager.ItemActionEffectsServer(rangedData.invData.holdingEntity.entityId, rangedData.invData.slotIdx, rangedData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 0);
        //                })
        //        });
        //            break;
        //        }
        //    }

        //    return codes;
        //}

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var prop_perc = AccessTools.PropertyGetter(typeof(ItemValue), nameof(ItemValue.PercentUsesLeft));

            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].Calls(prop_perc) && codes[i + 2].opcode == OpCodes.Bne_Un)
                {
                    codes.InsertRange(i + 3, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_0).WithLabels(codes[i + 3].ExtractLabels()),
                        CodeInstruction.Call(typeof(AudioPatches), nameof(FuckTFP))
                    });
                    break;
                }
            }

            return codes;
        }

        private static void FuckTFP(ItemActionRanged.ItemActionDataRanged rangedData)
        {
            rangedData.invData.gameManager.ItemActionEffectsServer(rangedData.invData.holdingEntity.entityId, rangedData.invData.slotIdx, rangedData.indexInEntityOfAction, 0, Vector3.zero, Vector3.zero, 0);
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ItemActionEffects))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ItemActionEffects_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_stop = AccessTools.Method(typeof(Manager), nameof(Manager.StopSequence));
            var fld_remote = AccessTools.Field(typeof(Entity), nameof(Entity.isEntityRemote));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_stop))
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].LoadsField(fld_remote))
                        {
                            var lbl = generator.DefineLabel();
                            codes[j - 1].WithLabels(lbl);
                            var operand = codes[j - 2].operand;
                            codes[j - 2].opcode = codes[j - 2].opcode == OpCodes.Brfalse_S ? OpCodes.Brtrue_S : OpCodes.Brfalse_S;
                            codes[j - 2].operand = lbl;
                            codes.InsertRange(j - 1, new[]
                            {
                                new CodeInstruction(OpCodes.Ldarg_3),
                                new CodeInstruction(OpCodes.Ldc_I4_0),
                                new CodeInstruction(OpCodes.Beq_S, operand)
                            });
                            i += 3;
                            break;
                        }
                    }
                }
            }

            return codes;
        }
        #endregion

        #region
        [HarmonyPatch(typeof(AudioMixerManager), nameof(AudioMixerManager.Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_Update_AudioMixerManager(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            var mtd_transitionto = AccessTools.Method(typeof(AudioMixerManager), nameof(AudioMixerManager.transitionTo));
            var fld_underwater = AccessTools.Field(typeof(AudioMixerManager), nameof(AudioMixerManager.underwaterSnapshot));
            var fld_stunned = AccessTools.Field(typeof(AudioMixerManager), nameof(AudioMixerManager.stunnedSnapshot));
            var fld_deafened = AccessTools.Field(typeof(AudioMixerManager), nameof(AudioMixerManager.deafenedSnapshot));
            var fld_default = AccessTools.Field(typeof(AudioMixerManager), nameof(AudioMixerManager.defaultSnapshot));
            var fld_transitiontime = AccessTools.Field(typeof(AudioMixerManager.SnapshotController), nameof(AudioMixerManager.SnapshotController.transitionToTime));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_transitionto))
                {
                    if (codes[i - 1].LoadsField(fld_underwater))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, fld_underwater),
                            new CodeInstruction(OpCodes.Ldfld, fld_transitiontime),
                            CodeInstruction.Call(typeof(CustomAudioSnapshotManager), nameof(CustomAudioSnapshotManager.TransitionToUnderwater))
                        });
                        i += 4;
                    }
                    else if (codes[i - 1].LoadsField(fld_stunned))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, fld_stunned),
                            new CodeInstruction(OpCodes.Ldfld, fld_transitiontime),
                            CodeInstruction.Call(typeof(CustomAudioSnapshotManager), nameof(CustomAudioSnapshotManager.TransitionToStunned))
                        });
                        i += 4;
                    }
                    else if (codes[i - 1].LoadsField(fld_deafened))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, fld_deafened),
                            new CodeInstruction(OpCodes.Ldfld, fld_transitiontime),
                            CodeInstruction.Call(typeof(CustomAudioSnapshotManager), nameof(CustomAudioSnapshotManager.TransitionToDeafened))
                        });
                        i += 4;
                    }
                    else if (codes[i - 1].LoadsField(fld_default))
                    {
                        codes.InsertRange(i + 1, new[]
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, fld_default),
                            new CodeInstruction(OpCodes.Ldfld, fld_transitiontime),
                            CodeInstruction.Call(typeof(CustomAudioSnapshotManager), nameof(CustomAudioSnapshotManager.TransitionToDefault))
                        });
                        i += 4;
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(SoundsFromXml), nameof(SoundsFromXml.ParseNode))]
        [HarmonyPostfix]
        private static void Postfix_ParseNode_SoundsFromXml(XElement root)
        {
            foreach (XElement node in root.Elements("CustomSnapshotHolder"))
            {
                var path = node.GetAttribute("path");
                CustomAudioSnapshotManager.RegisterSnapshotHolder(DataLoader.LoadAsset<CustomAudioSnapshotHolder>(path, true));
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Disconnect))]
        [HarmonyPostfix]
        private static void Postfix_Disconnect_GameManager()
        {
            CustomAudioSnapshotManager.CleanupHolders();
        }
        #endregion
    }

    public static class CustomAudioSnapshotManager
    {
        private static readonly List<CustomAudioSnapshotHolder> list_snapshot = new();

        public static void RegisterSnapshotHolder(CustomAudioSnapshotHolder holder)
        {
            if (!list_snapshot.Contains(holder))
            {
                list_snapshot.Add(holder);
            }
        }

        public static void CleanupHolders()
        {
            list_snapshot.Clear();
        }

        public static void TransitionToUnderwater(float transitionToTime)
        {
            if (list_snapshot.Count > 0)
            {
                foreach (var holder in list_snapshot)
                {
                    if (holder && holder.underwaterSnapshot)
                    {
                        holder.underwaterSnapshot.TransitionTo(transitionToTime);
                    }
                }
            }
        }

        public static void TransitionToStunned(float transitionToTime)
        {
            if (list_snapshot.Count > 0)
            {
                foreach (var holder in list_snapshot)
                {
                    if (holder && holder.stunnedSnapshot)
                    {
                        holder.stunnedSnapshot.TransitionTo(transitionToTime);
                    }
                }
            }
        }

        public static void TransitionToDeafened(float transitionToTime)
        {
            if (list_snapshot.Count > 0)
            {
                foreach (var holder in list_snapshot)
                {
                    if (holder && holder.deafenedSnapshot)
                    {
                        holder.deafenedSnapshot.TransitionTo(transitionToTime);
                    }
                }
            }
        }

        public static void TransitionToDefault(float transitionToTime)
        {
            if (list_snapshot.Count > 0)
            {
                foreach (var holder in list_snapshot)
                {
                    if (holder && holder.defaultSnapshot)
                    {
                        holder.defaultSnapshot.TransitionTo(transitionToTime);
                    }
                }
            }
        }
    }
}

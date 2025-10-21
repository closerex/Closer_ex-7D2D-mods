/*
using Audio;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib
{
    [HarmonyPatch]
    public static class AudioPoolingLoadPatches
    {
        [HarmonyPatch(typeof(Manager), nameof(Manager.LoadAudio))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Tranpiler_LoadAudio(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            MethodInfo mtd_instantiate = typeof(Object).GetMethods(BindingFlags.Public | BindingFlags.Static).
                                                        Where(static m => m.IsGenericMethod && m.ContainsGenericParameters &&m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1 && m.GetGenericArguments()[0] == m.GetParameters()[0].ParameterType).
                                                        First(static m => m.Name == nameof(Object.Instantiate)).
                                                        MakeGenericMethod(new[] { typeof(GameObject) });
            MethodInfo mtd_setvel = AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.velocity));
            MethodInfo mtd_setkinematic = AccessTools.PropertySetter(typeof(Rigidbody), nameof(Rigidbody.isKinematic));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(mtd_instantiate))
                {
                    codes[i] = CodeInstruction.Call(typeof(AudioPoolManager), nameof(AudioPoolManager.GetOrCreate));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_3));
                    i++;
                }
                else if (codes[i].Calls(mtd_setvel))
                {
                    codes.InsertRange(i - 1, new[]
                    {
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Callvirt, mtd_setkinematic)
                    });
                    i += 3;
                }
            }

            return codes;
        }

        //[HarmonyPatch(typeof(PlayAndCleanup), nameof(PlayAndCleanup.StopBeginWhenDone), MethodType.Enumerator)]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_StopBeginWhenDone_PlayAndCleanup(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = instructions.ToList();
        //    var mtd_destroy = AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) });
        //    var mtd_pool = AccessTools.Method(typeof(AudioPoolManager), nameof(AudioPoolManager.PoolObject));

        //    for (var i = 0; i < codes.Count; i++)
        //    {
        //        if (codes[i].Calls(mtd_destroy))
        //        {
        //            codes[i].operand = mtd_pool;
        //            Log.Out($"PlayAndCleanup.StopBeginWhenDone patched!");
        //        }
        //    }
        //    return codes;
        //}

        //[HarmonyPatch(typeof(PlayAndCleanup), nameof(PlayAndCleanup.StopWhenDone), MethodType.Enumerator)]
        //[HarmonyTranspiler]
        //private static IEnumerable<CodeInstruction> Transpiler_StopWhenDone_PlayAndCleanup(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = instructions.ToList();
        //    var mtd_destroy = AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) });
        //    var mtd_pool = AccessTools.Method(typeof(AudioPoolManager), nameof(AudioPoolManager.PoolObject));

        //    for (var i = 0; i < codes.Count; i++)
        //    {
        //        if (codes[i].Calls(mtd_destroy))
        //        {
        //            codes[i].operand = mtd_pool;
        //            Log.Out($"PlayAndCleanup.StopWhenDone patched!");
        //        }
        //    }
        //    return codes;
        //}


        [HarmonyPatch(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) })]
        [HarmonyPostfix]
        private static void Postfix_Destroy(Object obj)
        {
            if (obj && obj is GameObject gameObj && gameObj.TryGetComponent<AudioSourceIDRef>(out var idRef))
            {
                Log.Error($"Destroying AudioSource {idRef.id}\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }
    }

    [HarmonyPatch]
    public static class AudioPoolingDestroyPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new[]
            {
                AccessTools.Method(typeof(Manager), nameof(Manager.DestroySoundsForEntity)),
                AccessTools.Method(typeof(Manager), nameof(Manager.FrameUpdate)),
                AccessTools.Method(typeof(Manager), nameof(Manager.Play), new[] { typeof(Entity), typeof(string), typeof(float), typeof(bool) }),
                AccessTools.Method(typeof(Manager), nameof(Manager.Play), new[] { typeof(Vector3), typeof(string), typeof(int), typeof(bool) }),
                AccessTools.Method(typeof(Manager), nameof(Manager.RestartSequence)),
                AccessTools.Method(typeof(Manager), nameof(Manager.Stop), new[]{ typeof(Vector3), typeof(string) }),
                AccessTools.Method(typeof(Manager), nameof(Manager.Stop), new[] { typeof(int), typeof(string) }),
                AccessTools.Method(typeof(Manager), nameof(Manager.StopAllSequencesOnEntity)),
                AccessTools.Method(typeof(Manager), nameof(Manager.StopGroupLoop)),
                AccessTools.Method(typeof(Manager), nameof(Manager.StopLoopInsidePlayerHead)),
                AccessTools.Method(typeof(Manager), nameof(Manager.StopSequence)),
                AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(PlayAndCleanup), nameof(PlayAndCleanup.StopBeginWhenDone))),
                AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(PlayAndCleanup), nameof(PlayAndCleanup.StopWhenDone)))
            };
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) }),
                                               AccessTools.Method(typeof(AudioPoolManager), nameof(AudioPoolManager.PoolObject)));
        }
    }

    public static class AudioPoolManager
    {
        private static readonly Dictionary<string, AudioPool> dict_pools = new();
        public static Transform AudioSourcePoolParent
        {
            get
            {
                if (!parent)
                {
                    parent = new GameObject("AudioSourcePool").transform;
                    parent.gameObject.SetActive(false);
                }
                return parent;
            }
        }
        private static Transform parent;

        public static GameObject GetOrCreate(GameObject templateObject, string audioSourcePath)
        {
            if (!templateObject || !templateObject.TryGetComponent<AudioSource>(out var audioSource))
            {
                return null;
            }
            if (!dict_pools.TryGetValue(audioSourcePath, out var pool))
            {
                pool = new AudioPool(audioSource, audioSourcePath);
                dict_pools.Add(audioSourcePath, pool);
            }

            return pool.GetOrCreate();
        }

        public static void PoolObject(GameObject templateObject)
        {
            if (!templateObject)
            {
                return;
            }

            if (!templateObject.TryGetComponent<AudioSource>(out var audioSource) || !templateObject.TryGetComponent<AudioSourceIDRef>(out var pathRef) || string.IsNullOrEmpty(pathRef.id) || !dict_pools.TryGetValue(pathRef.id, out var pool))
            {
                GameObject.Destroy(templateObject);
                return;
            }

            if (!pathRef.pooled)
            {
                if (pool.Pool(audioSource))
                {
                    pathRef.pooled = true;
                }
            }
        }

        public class AudioPool
        {
            private readonly AudioSource templateAudioSource;
            private readonly Queue<GameObject> queue_pool;
            private readonly string id;
            private readonly int maxCount;

            public AudioPool(AudioSource templateAudioSource, string id, int maxCount = 100)
            {
                this.templateAudioSource = templateAudioSource;
                this.maxCount = maxCount;
                this.id = id;
                queue_pool = new Queue<GameObject>(maxCount);
            }

            public bool Pool(AudioSource itemToPool)
            {
                if (!itemToPool)
                {
                    return false;
                }

                if (queue_pool.Count >= maxCount)
                {
                    UnityEngine.Object.Destroy(itemToPool.gameObject);
                    return false;
                }

                itemToPool.Stop();
                itemToPool.clip = null;
                itemToPool.dopplerLevel = templateAudioSource.dopplerLevel;
                itemToPool.volume = templateAudioSource.volume;
                itemToPool.pitch = templateAudioSource.pitch;
                itemToPool.loop = templateAudioSource.loop;
                GameObject obj = itemToPool.gameObject;
                obj.transform.parent = AudioPoolManager.AudioSourcePoolParent;
                obj.SetActive(false);
                queue_pool.Enqueue(obj);
                return true;
            }

            public GameObject GetOrCreate()
            {
                GameObject res;
                if (queue_pool.Count == 0)
                {
                    res = GameObject.Instantiate(templateAudioSource.gameObject);
                    res.GetOrAddComponent<AudioSourceIDRef>().id = id;
                }
                else
                {
                    res = queue_pool.Dequeue();
                    res.transform.parent = null;
                    res.SetActive(true);
                    res.GetComponent<AudioSourceIDRef>().pooled = false;
                }

                return res;
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class AudioSourceIDRef : MonoBehaviour
    {
        public string id;
        public bool pooled;
    }
}
*/
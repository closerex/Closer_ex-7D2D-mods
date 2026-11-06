#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KFCommonUtilityLib
{
    // Editor preview instances only. Never attached to a persistent weapon or effect.
    [DefaultExecutionOrder(12000)]
    public sealed class KFMedicVfxPreviewPlayer : MonoBehaviour
    {
        [Serializable] public sealed class Route
        {
            public string category;
            public GameObject prefab;
            public Transform socket;
        }
        private sealed class Owned { public string category; public GameObject root; }
        private readonly List<Owned> owned = new List<Owned>();
        private Route[] routes;
        private RigTargets rig;
        private Transform playerRoot;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private string mainState, dartState;
        private int pending; // 1 = main, 2 = dart; a request is consumed only in LateUpdate.
        public bool IsInitialized { get; private set; }
        public bool MoveAndTurn { get; set; }
        public bool FireEnabled { get; private set; } = true;
        public bool SmokeEnabled { get; private set; } = true;
        public bool ExhaustEnabled { get; private set; } = true;
        public bool DartEnabled { get; private set; } = true;
        public int MainShots { get; private set; }
        public int DartShots { get; private set; }
        public string LastFailure { get; private set; }
        public string LastState { get; private set; }
        public GameObject[] LastSpawned { get; private set; } = new GameObject[0];
        public RigTargets Rig => rig;
        public Route[] Routes => routes == null ? new Route[0] : (Route[])routes.Clone();
        public GameObject[] OwnedRoots => owned.Where(o => o.root).Select(o => o.root).ToArray();
        public int LiveInstances => owned.Count(o => o.root);

        public void Initialize(RigTargets target, Transform player, string main, string dart, Route[] bindings)
        {
            if (!target || !target.IsAnimationSet || !target.ItemAnimator || !player)
                throw new InvalidOperationException("Preview rig is not initialized.");
            if (bindings == null || bindings.Length == 0 || bindings.Any(r => r == null || !r.prefab || !r.socket))
                throw new InvalidOperationException("Missing preview route.");
            if (IsInitialized) throw new InvalidOperationException("Preview already initialized.");
            rig = target; playerRoot = player; mainState = main; dartState = dart; routes = bindings;
            initialPosition = player.position; initialRotation = player.rotation;
            IsInitialized = true;
        }

        public bool RequestMainShot() => Request(1);
        public bool RequestDartShot() => Request(2);
        private bool Request(int kind)
        {
            if (!IsInitialized || !isActiveAndEnabled || !rig || !rig.ItemAnimator || pending != 0)
                return Reject("Preview is unavailable or a shot is pending.");
            if (LiveInstances > 48) return Reject("Wait for the existing particle tails to finish.");
            string state = kind == 1 ? mainState : dartState;
            if (string.IsNullOrEmpty(state) || !rig.ItemAnimator.HasState(0, Animator.StringToHash(state)))
                return Reject("Missing preview animation state.");
            rig.ItemAnimator.Play(state, 0, 0);
            rig.ItemAnimator.Update(0);
            pending = kind; LastState = state; LastFailure = null;
            return true;
        }
        private bool Reject(string message) { LastFailure = message; return false; }
        private bool Selected(string category)
        {
            switch (category)
            {
                case "fire": return FireEnabled;
                case "smoke": return SmokeEnabled;
                case "exhaust": return ExhaustEnabled;
                case "dart": return DartEnabled;
                default: return false;
            }
        }
        public void SetCategoryEnabled(string category, bool enabled)
        {
            switch (category)
            {
                case "fire": FireEnabled = enabled; break;
                case "smoke": SmokeEnabled = enabled; break;
                case "exhaust": ExhaustEnabled = enabled; break;
                case "dart": DartEnabled = enabled; break;
                default: throw new ArgumentException("Unknown effect category.", nameof(category));
            }
            if (!enabled)
            {
                foreach (var effect in owned.Where(o => o.category == category).ToArray())
                {
                    if (effect.root) Destroy(effect.root);
                    owned.Remove(effect);
                }
            }
        }
        public void ResetView()
        {
            MoveAndTurn = false;
            if (playerRoot) playerRoot.SetPositionAndRotation(initialPosition, initialRotation);
        }
        private void Update()
        {
            if (IsInitialized && MoveAndTurn && playerRoot)
            {
                playerRoot.position += Vector3.right * (.7f * Time.deltaTime);
                playerRoot.rotation = Quaternion.Euler(0, playerRoot.eulerAngles.y + 35 * Time.deltaTime, 0);
            }
        }
        private void LateUpdate()
        {
            owned.RemoveAll(o => !o.root);
            if (!IsInitialized || pending == 0) return;
            int shot = pending; pending = 0;
            var created = new List<Owned>();
            GameObject staging = null;
            try
            {
                staging = new GameObject("KFMedicVfxPreview.InactiveStaging");
                staging.SetActive(false);
                foreach (var route in routes.Where(r => Selected(r.category) && (shot == 2 ? r.category == "dart" : r.category != "dart")))
                {
                    if (!route.socket || !route.socket.gameObject.activeInHierarchy)
                        throw new InvalidOperationException("Inactive or missing socket: " + route.category);
                    var root = Instantiate(route.prefab, staging.transform, false);
                    created.Add(new Owned { category = route.category, root = root });
                    root.name = "KFMedicVfxPreview." + route.category;
                    root.SetActive(false);
                    root.transform.localPosition = Vector3.zero;
                    root.transform.SetParent(route.socket, false); // Keep the authored rotation and scale.
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 10;
                    foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                    root.SetActive(true);
                    // playOnAwake may have run during activation. No simulation occurs between these calls.
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    foreach (var capture in root.GetComponentsInChildren<KFParticleWorldBurstVelocity>())
                        if (!capture.PrepareForPlay()) throw new InvalidOperationException(capture.LastFailure);
                }
                // Prepare the entire shot before starting any of its routes.
                foreach (var effect in created)
                    if (!effect.root.AddComponent<AnimationParticleOneShot>().Begin(effect.root.transform.parent))
                        throw new InvalidOperationException("Preview requires finite, non-looping effects.");
                owned.AddRange(created);
                LastSpawned = created.Select(o => o.root).ToArray();
                if (shot == 1) MainShots++; else DartShots++;
                LastFailure = null;
            }
            catch (Exception e)
            {
                foreach (var effect in created) if (effect.root) Destroy(effect.root);
                LastSpawned = new GameObject[0];
                LastFailure = e.Message;
                Debug.LogError("KF medical preview: " + e.Message, this);
            }
            finally { if (staging) Destroy(staging); }
        }
        public void CancelOwnedEffects()
        {
            pending = 0; MoveAndTurn = false;
            foreach (var effect in owned) if (effect.root) Destroy(effect.root);
            owned.Clear(); LastSpawned = new GameObject[0];
        }
        private void OnDisable() { CancelOwnedEffects(); }
    }
}
#endif

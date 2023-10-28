using System.Collections.Generic;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public static class AnimationRiggingManager
    {
        public class FpvTransformRef
        {
            public Animator fpvAnimator;
            public RigTargets targets;
            public Transform muzzle;
            public Transform muzzle2;
            public bool isDoubleBarrel;

            public FpvTransformRef(RigTargets targets, bool isDoubleBarrel)
            {
                this.targets = targets;
                this.isDoubleBarrel = isDoubleBarrel;
                fpvAnimator = targets.itemFpv.GetComponentInChildren<Animator>();
                if (isDoubleBarrel)
                {
                    muzzle = targets.itemFpv.transform.FindInChildren("Muzzle_L");
                    muzzle2 = targets.itemFpv.transform.FindInChildren("Muzzle_R");
                }
                else
                    muzzle = targets.itemFpv.transform.FindInChilds("Muzzle");
            }
        }

        public static bool IsHoldingRiggedWeapon => fpvTransformRef != null;
        public static FpvTransformRef FpvTransformReference => fpvTransformRef;

        //private static readonly HashSet<int> hash_rig_items = new HashSet<int>();
        private static FpvTransformRef fpvTransformRef;
        private static readonly HashSet<int> hash_items_take_over_reload_time = new HashSet<int>();
        private static readonly HashSet<string> hash_items_parse_later = new HashSet<string>();

        //patched to item xml parsing
        //public static void AddRigItem(int itemId) => hash_rig_items.Add(itemId);

        public static void Clear()
        {
            //hash_rig_items.Clear();
            fpvTransformRef = null;
            hash_items_parse_later.Clear();
            hash_items_take_over_reload_time.Clear();
        }

        public static void AddReloadTimeTakeOverItem(string name)
        {
            hash_items_parse_later.Add(name);
        }

        public static void ParseItemIDs()
        {
            foreach (var name in hash_items_parse_later)
            {
                hash_items_take_over_reload_time.Add(ItemClass.GetItemClass(name).Id);
                //Log.Out($"parse item id: {name} {ItemClass.GetItemClass(name).Id}");
            }
            hash_items_parse_later.Clear();
        }

        public static bool IsReloadTimeTakeOverItem(int id)
        {
            return hash_items_take_over_reload_time.Contains(id);
        }

        //public static bool IsRigItem(int itemId) => hash_rig_items.Contains(itemId);

        public static void UpdateLocalPlayerAvatar(AvatarLocalPlayerController controller)
        {
            if (fpvTransformRef != null && (controller.Entity as EntityPlayerLocal).bFirstPersonView)
            {
                //workaround for animator bullshit
                if (!fpvTransformRef.targets.itemFpv.gameObject.activeSelf)
                {
                    Log.Out("Rigged weapon not active, enabling it...");
                    fpvTransformRef.targets.SetEnabled(true);
                }
                controller.FPSArms?.Animator?.SetInteger(AvatarController.weaponHoldTypeHash, -1);
            }
        }

        public static void OnClearInventorySlot(Inventory inv, int slot)
        {
            Transform transform = inv.models[slot];
            if (transform != null && transform.TryGetComponent<RigTargets>(out var targets))
                targets.Destroy();
        }

        //patched to EntityPlayerLocal.OnHoldingItemChanged
        public static void OnHoldingItemIndexChanged(EntityPlayerLocal player)
        {
            Inventory inv = player.inventory;
            Transform transform = inv.models[inv.holdingItemIdx];
            fpvTransformRef = null;
            if (transform != null && transform.TryGetComponent(out RigTargets targets))
                fpvTransformRef = new FpvTransformRef(targets, inv.holdingItemData.item.ItemTags.Test_Bit(FastTags.GetBit("dBarrel")));
        }

        public static Transform GetAttachmentReferenceOverrideTransform(Transform transform, string transformPath, Entity entity)
        {
            if (transform == null || entity == null || !(entity is EntityAlive entityAlive) || entityAlive.inventory == null)
                return null;

            //Log.Out("TRYING TO REDIRECT TRANSFORM REFERENCE: " + transform.name + " CHILD: " + transformPath);
            var targets = entityAlive.inventory.GetHoldingItemTransform()?.GetComponent<AttachmentReference>();
            if (targets != null && targets.attachmentReference != null)
            {
                var redirected = GameUtils.FindDeepChild(targets.attachmentReference, transform.name) ?? targets.attachmentReference;
                //Log.Out("REDIRECTING TRANSFORM REFERENCE TO " + redirected.name);
                var find = GameUtils.FindDeepChild(redirected, transformPath);
                if (find != null)
                    //Log.Out("FOUND REDIRECTED CHILD: " +  find.name + " PARENT: " + find.parent.name);
                    return find;
            }

            return GameUtils.FindDeepChild(transform, transformPath);
        }

        public static Transform GetMuzzleOverrideFPV(Transform muzzle, bool isLocalFpv)
        {
            if (!isLocalFpv || fpvTransformRef == null)
                return muzzle;
            return fpvTransformRef.muzzle;
        }

        public static Transform GetMuzzle2OverrideFPV(Transform muzzle2, bool isLocalFpv)
        {
            if (!isLocalFpv || fpvTransformRef == null)
                return muzzle2;
            return fpvTransformRef.muzzle2;
        }

        public static Transform GetTransformOverrideByName(string name, Transform itemModel)
        {
            if(itemModel == null)
                return null;
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null || !itemModel.TryGetComponent<RigTargets>(out var targets))
                return itemModel.FindInAllChilds(name, false);

            return (player.bFirstPersonView ? targets.itemFpv : itemModel).FindInAllChilds(name, false);
        }

        //patched to ItemActionRanged.ItemActionEffect
        public static bool SpawnFpvParticles(bool isLocalFpv, ItemActionData _actionData, string particlesMuzzleFire, string particlesMuzzleFireFpv, string particlesMuzzleSmoke, string particlesMuzzleSmokeFpv)
        {
            if (!isLocalFpv || fpvTransformRef == null)
                return false;
            var itemActionDataRanged = _actionData as ItemActionRanged.ItemActionDataRanged;
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (particlesMuzzleFire != null && fpvTransformRef.muzzle != null)
            {
                Transform fire = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleFireFpv != null ? particlesMuzzleFireFpv : particlesMuzzleFire, Vector3.zero, 1f, Color.clear, null, null, false), player.entityId, true);
                if (fire != null)
                {
                    fire.transform.localPosition = Vector3.zero;
                    //fire.transform.localEulerAngles = Vector3.zero;
                    if (fpvTransformRef.isDoubleBarrel && itemActionDataRanged.invData.itemValue.Meta == 0)
                        fire.transform.SetParent(fpvTransformRef.muzzle2, false);
                    else
                        fire.transform.SetParent(fpvTransformRef.muzzle, false);
                    Utils.SetLayerRecursively(fire.gameObject, 10, null);
                    //fire.transform.localPosition = Vector3.zero;
                    //fire.transform.localEulerAngles = Vector3.zero;
                    //fire.transform.localScale = Vector3.one;
                    foreach (var particle in fire.GetComponentsInChildren<ParticleSystem>())
                    {
                        particle.gameObject.SetActive(true);
                        particle.Clear();
                        particle.Play();
                    }
                    var temp = fire.gameObject.AddMissingComponent<TemporaryObject>();
                    temp.life = 5;
                    temp.Restart();
                    if (fire.TryGetComponent<LODGroup>(out var lod))
                        lod.enabled = false;
                    //Log.Out($"barrel position: {fire.transform.parent.parent.position}/{fire.transform.parent.parent.localPosition}, muzzle position: {fire.transform.parent.position}/{fire.transform.parent.localPosition}, particle position: {fire.transform.position}");
                    //Log.Out($"particles: {string.Join("\n", fire.GetComponentsInChildren<ParticleSystem>().Select(ps => ps.name + " active: " + ps.gameObject.activeInHierarchy + " layer: " + ps.gameObject.layer + " position: " + ps.transform.position))}");
                    if (fire.GetComponentsInChildren<ParticleSystem>().Length != 0)
                        itemActionDataRanged.particlesFire.Add(fire);
                }
            }
            if (particlesMuzzleSmoke != null && fpvTransformRef.muzzle != null)
            {
                float num = GameManager.Instance.World.GetLightBrightness(World.worldToBlockPos(fpvTransformRef.muzzle.transform.position)) / 2f;
                Color clear = Color.clear;
                Transform smoke = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleSmokeFpv != null ? particlesMuzzleSmokeFpv : particlesMuzzleSmoke, Vector3.zero, num, clear, null, null, false), player.entityId, true);
                if (smoke != null)
                {
                    smoke.transform.localPosition = Vector3.zero;
                    //smoke.transform.localEulerAngles = Vector3.zero;
                    smoke.gameObject.layer = 10;
                    smoke.transform.SetParent(fpvTransformRef.muzzle, false);
                    //smoke.transform.localPosition = Vector3.zero;
                    //smoke.transform.localEulerAngles = Vector3.zero;
                    //smoke.transform.localScale = Vector3.one;
                    foreach (var particle in smoke.GetComponentsInChildren<ParticleSystem>())
                    {
                        particle.gameObject.SetActive(true);
                        particle.Clear();
                        particle.Play();
                    }
                    var temp = smoke.gameObject.AddMissingComponent<TemporaryObject>();
                    temp.life = 5;
                    temp.Restart();
                    if (smoke.TryGetComponent<LODGroup>(out var lod))
                        lod.enabled = false;
                    itemActionDataRanged.particlesSmoke.Add(smoke);
                }
            }
            return true;
        }

        public static void SetTrigger(int _pid)
        {
            if (fpvTransformRef != null)
                fpvTransformRef.fpvAnimator?.SetTrigger(_pid);
        }

        public static void ResetTrigger(int _pid)
        {
            if (fpvTransformRef != null)
                fpvTransformRef.fpvAnimator?.ResetTrigger(_pid);
        }

        public static void SetFloat(int _pid, float _value)
        {
            if (fpvTransformRef != null)
                fpvTransformRef.fpvAnimator?.SetFloat(_pid, _value);
        }

        public static void SetBool(int _pid, bool _value)
        {
            if (fpvTransformRef != null)
                fpvTransformRef.fpvAnimator?.SetBool(_pid, _value);
        }

        public static void SetInt(int _pid, int _value)
        {
            if (fpvTransformRef != null)
                fpvTransformRef.fpvAnimator?.SetInteger(_pid, _value);
        }
    }
}

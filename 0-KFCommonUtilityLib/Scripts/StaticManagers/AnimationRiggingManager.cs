using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using static ItemActionRanged;

namespace KFCommonUtilityLib
{
    public static class AnimationRiggingManager
    {
        public static bool IsHoldingRiggedWeapon(EntityPlayer player)
        {
            AnimationTargetsAbs targets = GetActiveRigTargetsFromPlayer(player);
            return targets && !targets.Destroyed;
        }

        //private static bool RigItemChangedThisFrame = false;

        //private static readonly HashSet<int> hash_rig_items = new HashSet<int>();
        private static readonly HashSet<int> hash_items_take_over_reload_time = new HashSet<int>();
        private static readonly HashSet<string> hash_items_parse_later = new HashSet<string>();
        private static readonly HashSet<string> hash_rig_names = new HashSet<string>();
        private static readonly HashSet<int> hash_rig_changed_players = new HashSet<int>();

        //patched to item xml parsing
        //public static void AddRigItem(int itemId) => hash_rig_items.Add(itemId);

        public static void Clear()
        {
            //hash_rig_items.Clear();
            //RigItemChangedThisFrame = false;
            hash_items_parse_later.Clear();
            hash_items_take_over_reload_time.Clear();
            hash_rig_names.Clear();
            hash_rig_changed_players.Clear();
        }

        public static void AddReloadTimeTakeOverItem(string name)
        {
            hash_items_parse_later.Add(name);
        }

        public static void AddRigExcludeName(string name)
        {
            hash_rig_names.Add(name);
        }

        public static void RemoveRigExcludeName(string name)
        {
            hash_rig_names.Remove(name);
        }

        public static bool ShouldExcludeRig(string name)
        {
            return hash_rig_names.Contains(name);
        }

        public static string[] GetExcludeRigs()
        {
            return hash_rig_names.ToArray();
        }

        public static void ParseItemIDs()
        {
            foreach (var name in hash_items_parse_later)
                hash_items_take_over_reload_time.Add(ItemClass.GetItemClass(name).Id);
            hash_items_parse_later.Clear();
        }

        public static bool IsReloadTimeTakeOverItem(int id)
        {
            return hash_items_take_over_reload_time.Contains(id);
        }

        public static AnimationTargetsAbs GetActiveRigTargetsFromPlayer(EntityAlive entity)
        {
            if (entity is not EntityPlayer player || !player?.emodel?.avatarController)
                return null;
            AnimationGraphBuilder graphBuilder;
            if (player.emodel.avatarController is AvatarMultiBodyController multiBodyController)
                graphBuilder = multiBodyController.PrimaryBody?.Animator?.GetComponent<AnimationGraphBuilder>();
            else
                graphBuilder = player.emodel.avatarController.GetAnimator()?.GetComponent<AnimationGraphBuilder>();
            if (graphBuilder && graphBuilder.CurrentTarget)
                return graphBuilder.CurrentTarget;
            return null;
        }

        public static AnimationTargetsAbs GetHoldingRigTargetsFromPlayer(EntityAlive entity)
        {
            if (entity is not EntityPlayer player)
                return null;
            return GetCurRigTargetsFromInventory(player.inventory);
        }

        public static AnimationTargetsAbs GetCurRigTargetsFromInventory(Inventory inventory)
        {
            Transform holdingItemTransform = inventory?.GetHoldingItemTransform();
            if (holdingItemTransform)
                return holdingItemTransform.GetComponent<AnimationTargetsAbs>();
            return null;
        }

        public static AnimationTargetsAbs GetLastRigTargetsFromInventory(Inventory inventory)
        {
            Transform lastHoldingItemTransform = inventory?.lastdrawnHoldingItemTransform;
            if (lastHoldingItemTransform)
                return lastHoldingItemTransform.GetComponent<AnimationTargetsAbs>();
            return null;
        }

        //public static bool IsRigItem(int itemId) => hash_rig_items.Contains(itemId);

        public static void UpdatePlayerAvatar(AvatarController controller)
        {
            if (!controller?.Entity)
                return;
            AnimationTargetsAbs targets = GetActiveRigTargetsFromPlayer(controller.Entity);
            bool RigItemChangedThisFrame = hash_rig_changed_players.Remove(controller.Entity.entityId);
            if (targets && targets.IsAnimationSet)
            {
                targets.UpdatePlayerAvatar(controller, RigItemChangedThisFrame);
            }
            controller.UpdateBool(AvatarController.isCrouchingHash, controller.entity.Crouching, false);
        }

        public static void OnClearInventorySlot(Inventory inv, int slot)
        {
            if (inv == null)
                return;
            Transform transform = inv.models[slot];
            if (transform && transform.TryGetComponent<AnimationTargetsAbs>(out var targets) && !targets.Destroyed)
            {
                //RigItemChangedThisFrame = true;
                targets.Destroy();
                if (slot == inv.holdingItemIdx && inv.entity is EntityPlayer)
                    hash_rig_changed_players.Add(inv.entity.entityId);
            }
        }

        //patched to EntityPlayerLocal.OnHoldingItemChanged and EntityAlive.OnHoldingItemIndexChanged
        public static void OnHoldingItemIndexChanged(EntityPlayer player)
        {
            if (!player || player.inventory == null || player.inventory.m_LastDrawnHoldingItemIndex < 0 || player.inventory.m_LastDrawnHoldingItemIndex >= player.inventory.GetSlotCount())
                return;
            if (player.inventory.m_LastDrawnHoldingItemIndex == player.inventory.holdingItemIdx)
            {
                hash_rig_changed_players.Add(player.entityId);
                return;
            }
            Transform lastHoldingTransform = player.inventory.models[player.inventory.m_LastDrawnHoldingItemIndex];
            if (!lastHoldingTransform)
            {
                hash_rig_changed_players.Add(player.entityId);
                return;
            }
            AnimationTargetsAbs targets = lastHoldingTransform.GetComponent<AnimationTargetsAbs>();
            //if (targets && !targets.Destroyed && targets.IsAnimationSet)
            //{
            //    //RigItemChangedThisFrame = true;
            //    hash_rig_changed_players.Add(player.entityId);
            //    return;
            //}
            targets = GetHoldingRigTargetsFromPlayer(player);
            if (targets && targets.IsAnimationSet)
                //RigItemChangedThisFrame = true;
                hash_rig_changed_players.Add(player.entityId);
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

        public static Transform GetAttachmentReferenceOverrideTransformActive(Transform transform, string transformPath, Entity entity)
        {
            if (transform == null || entity == null || !(entity is EntityAlive entityAlive) || entityAlive.inventory == null)
                return null;

            //Log.Out("TRYING TO REDIRECT TRANSFORM REFERENCE: " + transform.name + " CHILD: " + transformPath);
            var targets = entityAlive.inventory.GetHoldingItemTransform()?.GetComponent<AttachmentReference>();
            if (targets != null && targets.attachmentReference != null)
            {
                var redirected = GameUtils.FindDeepChildActive(targets.attachmentReference, transform.name) ?? targets.attachmentReference;
                //Log.Out("REDIRECTING TRANSFORM REFERENCE TO " + redirected.name);
                var find = GameUtils.FindDeepChildActive(redirected, transformPath);
                if (find != null)
                    //Log.Out("FOUND REDIRECTED CHILD: " +  find.name + " PARENT: " + find.parent.name);
                    return find;
            }

            return GameUtils.FindDeepChildActive(transform, transformPath);
        }

        //public static Transform GetMuzzleOverrideFPV(Transform muzzle, bool isLocalFpv)
        //{
        //    if (!isLocalFpv || fpvTransformRef == null)
        //        return muzzle;
        //    if (fpvTransformRef.IsRanged(out var rangedData))
        //    {
        //        return rangedData.muzzle;
        //    }
        //    return muzzle;
        //}

        //public static Transform GetMuzzle2OverrideFPV(Transform muzzle2, bool isLocalFpv)
        //{
        //    if (!isLocalFpv || fpvTransformRef == null)
        //        return muzzle2;
        //    if (fpvTransformRef.IsRanged(out var rangedData))
        //    {
        //        return rangedData.muzzle2;
        //    }
        //    return muzzle2;
        //}

        public static Transform GetTransformOverrideByName(Transform itemModel, string name, bool onlyActive = true)
        {
            if (itemModel == null)
                return null;
            if (!itemModel.TryGetComponent<AnimationTargetsAbs>(out var targets) || targets.Destroyed)
            {
                if (string.IsNullOrEmpty(name))
                    return itemModel;
                return onlyActive ? GameUtils.FindDeepChildActive(itemModel, name) : GameUtils.FindDeepChild(itemModel, name);
            }

            var attachmentOverride = targets.GetAttachmentPathOverride(name, onlyActive);
            if (attachmentOverride)
                return attachmentOverride;

            Transform targetRoot = targets.ItemCurrentOrDefault;
            if (string.IsNullOrEmpty(name))
                return targetRoot;
            return onlyActive ? GameUtils.FindDeepChildActive(targetRoot, name) : GameUtils.FindDeepChild(targetRoot, name);
        }

        public static Transform GetAddPartTransformOverride(Transform itemModel, string name, bool onlyActive = true)
        {
            return GetTransformOverrideByName(itemModel, name, onlyActive) ?? itemModel;
        }

        //patched to ItemActionRanged.ItemActionEffect
        public static bool SpawnFpvParticles(bool isLocalFpv, ItemActionData _actionData, string particlesMuzzleFire, string particlesMuzzleFireFpv, string particlesMuzzleSmoke, string particlesMuzzleSmokeFpv)
        {
            if (!isLocalFpv || !GetCurRigTargetsFromInventory(_actionData.invData.holdingEntity.inventory))
                return false;
            var itemActionDataRanged = _actionData as ItemActionDataRanged;
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (itemActionDataRanged.muzzle != null)
            {
                Transform parent = itemActionDataRanged.IsDoubleBarrel && itemActionDataRanged.invData.itemValue.Meta == 0 ? itemActionDataRanged.muzzle2 : itemActionDataRanged.muzzle;
                if (!itemActionDataRanged.IsFlashSuppressed && particlesMuzzleFire != null)
                {
                    Transform fire = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleFireFpv != null ? particlesMuzzleFireFpv : particlesMuzzleFire, Vector3.zero, 1f, Color.clear, null, null, false), player.entityId, true);
                    ProcessMuzzleFlashParticle(fire, parent);
                }
                if (particlesMuzzleSmoke != null)
                {
                    float num = GameManager.Instance.World.GetLightBrightness(World.worldToBlockPos(itemActionDataRanged.muzzle.transform.position)) / 2f;
                    Transform smoke = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleSmokeFpv != null ? particlesMuzzleSmokeFpv : particlesMuzzleSmoke, Vector3.zero, num, Color.clear, null, null, false), player.entityId, true);
                    ProcessMuzzleFlashParticle(smoke, parent);
                }
            }

            return true;
        }

        public static void ProcessMuzzleFlashParticle(Transform particlePrefab, Transform parent)
        {
            if (particlePrefab != null)
            {
                particlePrefab.transform.localPosition = Vector3.zero;
                particlePrefab.transform.SetParent(parent, false);
                Utils.SetLayerRecursively(particlePrefab.gameObject, 10, null);
                foreach (var particle in particlePrefab.GetComponentsInChildren<ParticleSystem>())
                {
                    particle.gameObject.SetActive(true);
                    particle.Clear();
                    particle.Play();
                }
                var temp = particlePrefab.gameObject.GetOrAddComponent<TemporaryMuzzleFlash>();
                temp.life = 5;
                if (particlePrefab.TryGetComponent<LODGroup>(out var lod))
                    lod.enabled = false;
            }
        }
    }
}

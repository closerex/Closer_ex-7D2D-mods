using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.StaticManagers
{
    public static class AnimationRiggingManager
    {
        //public class FpvTransformRef
        //{
        //    public Animator fpvAnimator;
        //    public RigTargets targets;
        //    public ItemInventoryData invData;
        //    //public Transform muzzle;
        //    //public Transform muzzle2;
        //    //public bool isDoubleBarrel;

        //    public FpvTransformRef(RigTargets targets, ItemInventoryData invData)
        //    {
        //        this.targets = targets;
        //        this.invData = invData;
        //        //this.isDoubleBarrel = isDoubleBarrel;
        //        fpvAnimator = targets.itemFpv.GetComponentInChildren<Animator>();
        //        //if (isDoubleBarrel)
        //        //{
        //        //    muzzle = targets.itemFpv.transform.FindInChildren("Muzzle_L");
        //        //    muzzle2 = targets.itemFpv.transform.FindInChildren("Muzzle_R");
        //        //}
        //        //else
        //        //    muzzle = targets.itemFpv.transform.FindInChilds("Muzzle");
        //    }

        //    public bool IsRanged(out ItemActionRanged.ItemActionDataRanged rangedData)
        //    {
        //        return (rangedData = invData.actionData[MultiActionManager.GetActionIndexForEntity(GameManager.Instance.World.GetPrimaryPlayer())] as ItemActionRanged.ItemActionDataRanged) != null;
        //    }
        //}

        public static bool IsHoldingRiggedWeapon(EntityPlayer player)
        {
            AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
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

        public static AnimationTargetsAbs GetRigTargetsFromPlayer(EntityPlayer player)
        {
            if (!player)
                return null;
            Transform holdingItemTransform = player.inventory?.GetHoldingItemTransform();
            if (holdingItemTransform)
            {
                return holdingItemTransform.GetComponent<AnimationTargetsAbs>();
            }
            return null;
        }

        public static AnimationTargetsAbs GetRigTargetsFromInventory(Inventory inventory)
        {
            Transform holdingItemTransform = inventory?.GetHoldingItemTransform();
            if (holdingItemTransform)
            {
                return holdingItemTransform.GetComponent<AnimationTargetsAbs>();
            }
            return null;
        }

        //public static bool IsRigItem(int itemId) => hash_rig_items.Contains(itemId);

        public static void UpdatePlayerAvatar(AvatarController controller)
        {
            if (!controller?.Entity)
            {
                return;
            }
            AnimationTargetsAbs targets = GetRigTargetsFromPlayer(controller.Entity as EntityPlayer);
            bool RigItemChangedThisFrame = hash_rig_changed_players.Remove(controller.Entity.entityId);
            if (targets && !targets.Destroyed)
            {
                targets.UpdatePlayerAvatar(controller, RigItemChangedThisFrame);
            }
            controller.UpdateBool(AvatarController.isCrouchingHash, controller.entity.IsCrouching, true);
            //if ((controller.Entity as EntityPlayerLocal).bFirstPersonView && targets && !targets.Destroyed && targets.itemFpv)
            //{
            //    //workaround for animator bullshit
            //    if (!targets.itemFpv.gameObject.activeSelf)
            //    {
            //        Log.Out("Rigged weapon not active, enabling it...");
            //        targets.SetEnabled(true);
            //    }
            //    //vroid workaround
            //    //it seems to use a separate animator for vroid model and does not replace CharacterBody
            //    //controller.UpdateInt(AvatarController.weaponHoldTypeHash, -1, false);
            //    controller.FPSArms.Animator.Play("idle", 0, 0f);
            //    foreach (var hash in resetHashes)
            //    {
            //        AnimationRiggingPatches.VanillaResetTrigger(controller, hash, false);
            //    }
            //    //controller.FPSArms?.Animator?.SetInteger(AvatarController.weaponHoldTypeHash, -1);
            //    //controller.CharacterBody?.Animator?.SetInteger(AvatarController.weaponHoldTypeHash, -1);
            //}
            //if (RigItemChangedThisFrame)
            //{
            //    Log.Out("Rigged weapon changed, resetting animator...");
            //    Transform modelRoot = controller.GetActiveModelRoot();
            //    //if (modelRoot && (!targets || targets.Destroyed) && modelRoot.GetComponentInChildren<Animator>().TryGetComponent<AnimationGraphBuilder>(out var builder))
            //    //{
            //    //    builder.DestroyGraph(!targets || targets.Destroyed);
            //    //}
            //    if (controller is AvatarLocalPlayerController localPlayerController && localPlayerController.isFPV && localPlayerController.FPSArms != null)
            //    {
            //        if (localPlayerController.FPSArms.Animator.TryGetComponent<AnimationGraphBuilder>(out var builder))
            //        {
            //            builder.VanillaWrapper.Play("idle", 0, 0f);
            //        }
            //        else
            //        {
            //            localPlayerController.FPSArms.Animator.Play("idle", 0, 0f);
            //        }
            //    }
            //    controller.UpdateInt(AvatarController.weaponHoldTypeHash, -1, false);
            //}
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
                {
                    hash_rig_changed_players.Add(inv.entity.entityId);
                }
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
            targets = GetRigTargetsFromPlayer(player);
            if (targets && !targets.Destroyed && targets.IsAnimationSet)
            {
                //RigItemChangedThisFrame = true;
                hash_rig_changed_players.Add(player.entityId);
            }
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
            if (!isLocalFpv || !GetRigTargetsFromInventory(_actionData.invData.holdingEntity.inventory))
                return false;
            var itemActionDataRanged = _actionData as ItemActionRanged.ItemActionDataRanged;
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (itemActionDataRanged.muzzle != null)
            {
                if (particlesMuzzleFire != null)
                {
                    Transform fire = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleFireFpv != null ? particlesMuzzleFireFpv : particlesMuzzleFire, Vector3.zero, 1f, Color.clear, null, null, false), player.entityId, true);
                    if (fire != null)
                    {
                        fire.transform.localPosition = Vector3.zero;
                        //fire.transform.localEulerAngles = Vector3.zero;
                        if (itemActionDataRanged.IsDoubleBarrel && itemActionDataRanged.invData.itemValue.Meta == 0)
                            fire.transform.SetParent(itemActionDataRanged.muzzle2, false);
                        else
                            fire.transform.SetParent(itemActionDataRanged.muzzle, false);
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
                        var temp = fire.gameObject.GetOrAddComponent<TemporaryMuzzleFlash>();
                        temp.life = 5;
                        //temp.Restart();
                        if (fire.TryGetComponent<LODGroup>(out var lod))
                            lod.enabled = false;
                        //Log.Out($"barrel position: {fire.transform.parent.parent.position}/{fire.transform.parent.parent.localPosition}, muzzle position: {fire.transform.parent.position}/{fire.transform.parent.localPosition}, particle position: {fire.transform.position}");
                        //Log.Out($"particles: {string.Join("\n", fire.GetComponentsInChildren<ParticleSystem>().Select(ps => ps.name + " active: " + ps.gameObject.activeInHierarchy + " layer: " + ps.gameObject.layer + " position: " + ps.transform.position))}");
                    }
                }
                if (particlesMuzzleSmoke != null && itemActionDataRanged.muzzle != null)
                {
                    float num = GameManager.Instance.World.GetLightBrightness(World.worldToBlockPos(itemActionDataRanged.muzzle.transform.position)) / 2f;
                    Color clear = Color.clear;
                    Transform smoke = GameManager.Instance.SpawnParticleEffectClientForceCreation(new ParticleEffect(particlesMuzzleSmokeFpv != null ? particlesMuzzleSmokeFpv : particlesMuzzleSmoke, Vector3.zero, num, clear, null, null, false), player.entityId, true);
                    if (smoke != null)
                    {
                        smoke.transform.localPosition = Vector3.zero;
                        //smoke.transform.localEulerAngles = Vector3.zero;
                        Utils.SetLayerRecursively(smoke.gameObject, 10, null);
                        smoke.transform.SetParent(itemActionDataRanged.muzzle, false);
                        //smoke.transform.localPosition = Vector3.zero;
                        //smoke.transform.localEulerAngles = Vector3.zero;
                        //smoke.transform.localScale = Vector3.one;
                        foreach (var particle in smoke.GetComponentsInChildren<ParticleSystem>())
                        {
                            particle.gameObject.SetActive(true);
                            particle.Clear();
                            particle.Play();
                        }
                        var temp = smoke.gameObject.GetOrAddComponent<TemporaryMuzzleFlash>();
                        temp.life = 5;
                        //temp.Restart();
                        if (smoke.TryGetComponent<LODGroup>(out var lod))
                            lod.enabled = false;
                    }
                }
            }

            return true;
        }

        //public static void SetTrigger(int _pid, EntityPlayer player)
        //{
        //    AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
        //    if (targets && !targets.Destroyed && targets.ItemAnimator)
        //    {
        //        targets.ItemAnimator.SetTrigger(_pid);
        //        //Log.Out($"setting trigger {_pid}");
        //    }
        //}

        //public static void ResetTrigger(int _pid, EntityPlayer player)
        //{
        //    AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
        //    if (targets && !targets.Destroyed && targets.ItemAnimator)
        //    {
        //        targets.ItemAnimator.ResetTrigger(_pid);
        //        //Log.Out($"resetting trigger {_pid}");
        //    }
        //}

        //public static void SetFloat(int _pid, float _value, EntityPlayer player)
        //{
        //    AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
        //    if (targets&& !targets.Destroyed  && targets.ItemAnimator)
        //    {
        //        targets.ItemAnimator.SetFloat(_pid, _value);
        //        //Log.Out($"setting float {_pid}");
        //    }
        //}

        //public static void SetBool(int _pid, bool _value, EntityPlayer player)
        //{
        //    AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
        //    if (targets && !targets.Destroyed && targets.ItemAnimator)
        //    {
        //        targets.ItemAnimator.SetBool(_pid, _value);
        //        //Log.Out($"setting bool {_pid}");
        //    }
        //}

        //public static void SetInt(int _pid, int _value, EntityPlayer player)
        //{
        //    AnimationTargetsAbs targets = GetRigTargetsFromPlayer(player);
        //    if (targets && !targets.Destroyed && targets.ItemAnimator)
        //    {
        //        targets.ItemAnimator.SetInteger(_pid, _value);
        //        //Log.Out($"setting int {_pid}");
        //    }
        //}
    }
}

using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#else
using KFCommonUtilityLib;
#endif

public class AnimationCustomMeleeAttackState : StateMachineBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
    public float RaycastTime = 0.3f;
    public bool GrazeAsRaycast = false;
    public bool UseGraze = true;
    public float CustomGrazeCastTime = 0.3f;
    public float CustomGrazeCastDuration = 0f;
    public float CustomGrazeCastInterval = -1f;
    public bool GrazeCanHitSameTarget = false;
    public float ImpactDuration = 0.01f;
    [Range(0.01f, 1f)]
    public float ImpactPlaybackSpeed = 1f;
    [Range(0f, 1f)]
    public float attackDurationNormalized = 1f;
    [Range(0f, 360f)]
    public float SwingAngle = 0f;
    [Range(-180f, 180f)]
    public float SwingDegrees = 0f;
    public bool IsAlternative = false;
    public bool InvariableRPM = false;
    public bool CancelLoopOnRelease = false;

    [SerializeField]
    private float ClipLength = 0f;

#if UNITY_EDITOR
    public void OnBeforeSerialize()
    {
        var context = AnimatorController.FindStateMachineBehaviourContext(this);
        if (context != null && context.Length > 0)
        {
            var state = context[0].animatorObject as AnimatorState;
            if (state != null)
            {
                var clip = state.motion as AnimationClip;
                if (clip != null)
                {
                    ClipLength = clip.length;
                }
            }
        }
    }

    public void OnAfterDeserialize()
    {
    }
#endif

#if NotEditor
    private static readonly int AttackSpeedHash = Animator.StringToHash("MeleeAttackSpeed");
    private static readonly int MeleeRunningHash = Animator.StringToHash("IsMeleeRunning");
    private float calculatedRaycastTime;
    private float calculatedGrazeTime;
    private float calculatedGrazeDuration;
    private float calculatedGrazeInterval;
    private float calculatedImpactDuration;
    private float calculatedImpactPlaybackSpeed;
    private bool hasFired;
    private int actionIndex;
    private float originalMeleeAttackSpeed;
    //private bool playingImpact;
    private EntityAlive entity;
    private float attacksPerMinute;
    private float speedMultiplierToKeep = 1f;
    private InventorySlotGurad slotGurad = new InventorySlotGurad();
    private ItemModuleMultiItem.MultiItemInvData multiInvData;
    private bool impactSpeedRestored;
    private bool forceStopCalled;
    private bool stateExited = true, stateGuarded = false;

    private Coroutine impactCo, grazeCo;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!stateExited)
        {
            stateGuarded = true;
        }
        stateExited = false;
        impactSpeedRestored = false;
        forceStopCalled = false;
        hasFired = false;
        if (IsAlternative)
        {
            animator.SetWrappedBool(Animator.StringToHash("UseAltMelee"), false);
        }
        actionIndex = animator.GetWrappedInt(AvatarController.itemActionIndexHash);
        entity = animator.GetComponentInParent<EntityAlive>();
        if (!slotGurad.IsValid(entity) || entity.isEntityRemote)
        {
            return;
        }
        float length = attackDurationNormalized > 0 ? ClipLength * attackDurationNormalized : ClipLength;
        attacksPerMinute = 60f / length;
        FastTags<TagGroup.Global> fastTags = ((actionIndex != 1) ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag);
        multiInvData = (entity.inventory.holdingItemData as IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData>)?.Instance;
        bool isValidAlternative = IsAlternative && multiInvData != null;
        var prevData = SetParams(isValidAlternative);
        ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
        if (itemActionDynamicMeleeData != null)
        {
            itemActionDynamicMeleeData.lastUseTime = Time.time;
        }
        ItemValue holdingItemItemValue = entity.inventory.holdingItemItemValue;
        ItemClass itemClass = holdingItemItemValue.ItemClass;
        if (itemClass != null)
        {
            fastTags |= itemClass.ItemTags;
        }
        originalMeleeAttackSpeed = InvariableRPM ? stateInfo.speed : EffectManager.GetValue(PassiveEffects.AttacksPerMinute, holdingItemItemValue, attacksPerMinute, entity, null, fastTags) / 60f * length;
        animator.SetWrappedFloat(AttackSpeedHash, InvariableRPM ? 1 : originalMeleeAttackSpeed);
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        ItemClass holdingItem = entity.inventory.holdingItem;
        //holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.RaycastTime" : "Action1.RaycastTime", ref RaycastTime);
        //float impactDuration = -1f;
        //holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.ImpactDuration" : "Action1.ImpactDuration", ref impactDuration);
        //if (impactDuration >= 0f)
        //{
        //    ImpactDuration = impactDuration * originalMeleeAttackSpeed;
        //}
        //holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.ImpactPlaybackSpeed" : "Action1.ImpactPlaybackSpeed", ref ImpactPlaybackSpeed);
        if (originalMeleeAttackSpeed != 0f)
        {
            calculatedRaycastTime = RaycastTime / originalMeleeAttackSpeed;
            calculatedGrazeTime = CustomGrazeCastTime / originalMeleeAttackSpeed;
            calculatedGrazeDuration = CustomGrazeCastDuration / originalMeleeAttackSpeed;
            calculatedGrazeInterval = CustomGrazeCastInterval <= 0f ? -1f : CustomGrazeCastInterval / originalMeleeAttackSpeed;
            calculatedImpactDuration = ImpactDuration / originalMeleeAttackSpeed;
            calculatedImpactPlaybackSpeed = InvariableRPM ? ImpactPlaybackSpeed : ImpactPlaybackSpeed * originalMeleeAttackSpeed;
        }
        else
        {
            calculatedRaycastTime = 0.001f;
            calculatedGrazeTime = 0.001f;
            calculatedGrazeDuration = 0.001f;
            calculatedGrazeInterval = -1f;
            calculatedImpactDuration = 0.001f;
            calculatedImpactPlaybackSpeed = 0.001f;
        }
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"original: raycast time {RaycastTime} impact duration {ImpactDuration} impact playback speed {ImpactPlaybackSpeed} clip length {length}/{stateInfo.length}");
            Log.Out($"calculated: raycast time {calculatedRaycastTime} impact duration {calculatedImpactDuration} impact playback speed {calculatedImpactPlaybackSpeed} speed multiplier {originalMeleeAttackSpeed}");
        }
        if (!GrazeAsRaycast)
        { 
            impactCo = ThreadManager.StartCoroutine(impactStart(animator, layerIndex, length));
        }
        if (UseGraze)
        {
            grazeCo = ThreadManager.StartCoroutine(customGrazeStart(length));
        }
        RestoreParams(isValidAlternative, prevData);
        entity.emodel.avatarController.UpdateBool(MeleeRunningHash, true, true);
    }

    private IEnumerator impactStart(Animator animator, int layer, float length)
    {
        yield return new WaitForSeconds(Mathf.Max(calculatedRaycastTime, 0));
        impactCo = null;
        if (!hasFired)
        {
            hasFired = true;
            if (entity != null && !entity.isEntityRemote && actionIndex >= 0)
            {
                bool isValidAlternative = IsAlternative && multiInvData != null && multiInvData.boundInvData != null;
                var prevData = SetParams(isValidAlternative);
                float alternativeDeltaUseTimes = isValidAlternative ? multiInvData.boundInvData.itemStack.itemValue.UseTimes : 0f;
                ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
                if (itemActionDynamicMeleeData != null)
                {
                    if ((entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee).Raycast(itemActionDynamicMeleeData))
                    {
                        if (isValidAlternative)
                        {
                            multiInvData.originalData.itemStack.itemValue.UseTimes += multiInvData.boundInvData.itemStack.itemValue.UseTimes - alternativeDeltaUseTimes;
                        }
                        if (ConsoleCmdReloadLog.LogInfo)
                        {
                            Log.Out("Raycast hit!");
                        }
                        impactCo = ThreadManager.StartCoroutine(impactStop(animator, layer, length));
                    }
                    else
                    {
                        if (ConsoleCmdReloadLog.LogInfo)
                        {
                            Log.Out("Raycast miss!");
                        }
                    }
                }
                RestoreParams(isValidAlternative, prevData);
            }
        }
        yield break;
    }

    private IEnumerator impactStop(Animator animator, int layer, float length)
    {
        if (animator)
        {
            //Log.Out("Impact start!");
            animator.SetWrappedFloat(AttackSpeedHash, calculatedImpactPlaybackSpeed);
        }
        speedMultiplierToKeep = calculatedImpactPlaybackSpeed;
        animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
        yield return new WaitForSeconds(calculatedImpactDuration);
        if (animator)
        {
            //Log.Out("Impact stop!");
            animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
        }
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
        impactSpeedRestored = true;
        impactCo = null;
        yield break;
    }

    private IEnumerator customGrazeStart(float length)
    {
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"Custom graze time: {calculatedGrazeTime} original {CustomGrazeCastTime}");
        }
        yield return new WaitForSeconds(calculatedGrazeTime);
        grazeCo = null;
        if (entity != null && !entity.isEntityRemote && actionIndex >= 0)
        {
            bool isValidAlternative = IsAlternative && multiInvData != null && multiInvData.boundInvData != null;
            var prevData = SetParams(isValidAlternative);
            ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
            if (itemActionDynamicMeleeData != null)
            {
                grazeCo = ThreadManager.StartCoroutine(customGrazeUpdate(itemActionDynamicMeleeData));
            }
            RestoreParams(isValidAlternative, prevData);
        }
    }

    private IEnumerator customGrazeUpdate(ItemActionDynamicMelee.ItemActionDynamicMeleeData data)
    {
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"Custom graze duration: {calculatedGrazeDuration} original {CustomGrazeCastDuration}");
        }
        if (calculatedGrazeDuration <= 0f)
        {
            goto Return;
        }
        float grazeStart = Time.time;
        float normalizedTime = 0f;
        while (normalizedTime <= 1 || attackDurationNormalized <= 0)
        {
            if (!slotGurad.IsValid(data.invData.holdingEntity) || data.invData.holdingEntity.IsDead())
            {
                Log.Out($"Invalid graze!");
                goto Return;
            }
            bool isValidAlternative = IsAlternative && multiInvData != null && multiInvData.boundInvData != null;
            var prevData = SetParams(isValidAlternative);
            var action = entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee;
            float originalSwingAngle = action.SwingAngle;
            float originalSwingDegrees = action.SwingDegrees;
            action.SwingAngle = SwingAngle;
            action.SwingDegrees = SwingDegrees;
            bool grazeResult = GrazeAsRaycast ? action.Raycast(data) : action.GrazeCast(data, normalizedTime % 1);
            if (GrazeCanHitSameTarget)
            {
                data.alreadyHitEnts.Clear();
                data.alreadyHitBlocks.Clear();
            }
            if (ConsoleCmdReloadLog.LogInfo)
            {
                Log.Out($"GrazeCast {grazeResult}!");
            }
            action.SwingAngle = originalSwingAngle;
            action.SwingDegrees = originalSwingDegrees;
            if (attackDurationNormalized <= 0)
            {
                data.lastUseTime = Time.time;
            }
            RestoreParams(isValidAlternative, prevData);
            if (!CheckMeleeRunning(data))
            {
                break;
            }
            yield return calculatedGrazeInterval > 0 ? new WaitForSeconds(calculatedGrazeInterval) : null;
            normalizedTime = (Time.time - grazeStart) / calculatedGrazeDuration;
        }
        Return:
        grazeCo = null;
        yield break;
    }

    private void ForceStopCoroutines()
    {
        if (forceStopCalled)
        {
            return;
        }
        if (impactCo != null)
        {
            ThreadManager.StopCoroutine(impactCo);
            impactCo = null;
        }
        if (grazeCo != null)
        {
            ThreadManager.StopCoroutine(grazeCo);
            grazeCo = null;
        }
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        if (entity != null && !entity.isEntityRemote && actionIndex >= 0)
        {
            bool isValidAlternative = IsAlternative && multiInvData != null && multiInvData.boundInvData != null;
            var prevData = SetParams(isValidAlternative);
            var itemActionDynamicMelee = entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee;
            var itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
            entity.MinEventContext.ItemActionData = itemActionDynamicMeleeData;
            if (itemActionDynamicMelee != null && itemActionDynamicMeleeData != null)
            {
                itemActionDynamicMelee.SetAttackFinished(itemActionDynamicMeleeData);
                if (attackDurationNormalized <= 0)
                {
                    itemActionDynamicMeleeData.lastUseTime = 0;
                }
            }
            RestoreParams(isValidAlternative, prevData);
        }
        forceStopCalled = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!stateGuarded)
        {
            ForceStopCoroutines();
            if (!impactSpeedRestored)
            {
                animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
                impactSpeedRestored = true;
            }
            stateExited = true;
        }
        else
        {
            stateGuarded = false;
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!stateGuarded)
        {
            if (attackDurationNormalized > 0 && stateInfo.normalizedTime >= attackDurationNormalized)
            {
                ForceStopCoroutines();
            }
            animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
        }
    }

    private ItemActionData SetParams(bool isValidAlternative)
    {
        ItemActionData prevData = entity.MinEventContext.ItemActionData;
        if (isValidAlternative)
        {
            multiInvData.SetBoundParams();
        }
        entity.MinEventContext.ItemActionData = entity.inventory.holdingItemData.actionData[actionIndex];
        return prevData;
    }

    private void RestoreParams(bool isValidAlternative, ItemActionData prevData)
    {
        if (isValidAlternative)
        {
            multiInvData.RestoreParams(false);
        }
        entity.MinEventContext.ItemActionData = prevData;
    }

    private bool CheckMeleeRunning(ItemActionDynamicMelee.ItemActionDynamicMeleeData data)
    {
        if (data.invData.itemValue.PercentUsesLeft <= 0f || data.invData.holdingEntity.Stamina < data.StaminaUsage || !data.invData.holdingEntity.inventory.GetIsFinishedSwitchingHeldItem() || (attackDurationNormalized <= 0 && data.HasReleased))
        {
            //Log.Out($"Stopping melee running: {attackDurationNormalized} {data.HasReleased}");
            data.invData.holdingEntity.emodel.avatarController.UpdateBool(MeleeRunningHash, false, true);
            data.HasExecuted = true;
            return false;
        }
        return true;
    }
#endif
}
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public class AnimationCustomMeleeAttackState : StateMachineBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
    public float RaycastTime = 0.3f;
    public float CustomGrazeCastTime = 0.3f;
    public float CustomGrazeCastDuration = 0f;
    public float ImpactDuration = 0.01f;
    [Range(0.01f, 1f)]
    public float ImpactPlaybackSpeed = 1f;
    [Range(0.01f, 1f)]
    public float attackDurationNormalized = 1f;
    [Range(0f, 360f)]
    public float SwingAngle = 0f;
    [Range(-180f, 180f)]
    public float SwingDegrees = 0f;

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
    private readonly int AttackSpeedHash = Animator.StringToHash("MeleeAttackSpeed");
    private float calculatedRaycastTime;
    private float calculatedGrazeTime;
    private float calculatedGrazeDuration;
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

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //if (playingImpact)
        //{
        //    return;
        //}
        hasFired = false;
        actionIndex = animator.GetWrappedInt(AvatarController.itemActionIndexHash);
        entity = animator.GetComponentInParent<EntityAlive>();
        if (!slotGurad.IsValid(entity))
        {
            return;
        }
        //Log.Out("State entered!");
        //AnimatorClipInfo[] array = animator.GetNextAnimatorClipInfo(layerIndex);
        float length = ClipLength * attackDurationNormalized;
        ////if (array.Length == 0)
        ////{
        //    var array = animator.GetCurrentAnimatorClipInfo(layerIndex);
        //    if (array.Length == 0)
        //    {
        //        if (float.IsInfinity(stateInfo.length))
        //        {
        //            Log.Out($"Invalid clips!");
        //            return;
        //        }
        //        length = stateInfo.length;
        //    }
        //    else
        //    {
        //        length = array[0].clip.length;
        //    }
        ////}
        ////else
        ////{
        ////    length = array[0].clip.length;
        ////}
        //length *= attackDurationNormalized;
        attacksPerMinute = 60f / length;
        FastTags<TagGroup.Global> fastTags = ((actionIndex != 1) ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag);
        ItemValue holdingItemItemValue = entity.inventory.holdingItemItemValue;
        ItemClass itemClass = holdingItemItemValue.ItemClass;
        if (itemClass != null)
        {
            fastTags |= itemClass.ItemTags;
        }
        originalMeleeAttackSpeed = EffectManager.GetValue(PassiveEffects.AttacksPerMinute, holdingItemItemValue, attacksPerMinute, entity, null, fastTags) / 60f * length;
        animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        ItemClass holdingItem = entity.inventory.holdingItem;
        holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.RaycastTime" : "Action1.RaycastTime", ref RaycastTime);
        float impactDuration = -1f;
        holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.ImpactDuration" : "Action1.ImpactDuration", ref impactDuration);
        if (impactDuration >= 0f)
        {
            ImpactDuration = impactDuration * originalMeleeAttackSpeed;
        }
        holdingItem.Properties.ParseFloat((actionIndex != 1) ? "Action0.ImpactPlaybackSpeed" : "Action1.ImpactPlaybackSpeed", ref ImpactPlaybackSpeed);
        if (originalMeleeAttackSpeed != 0f)
        {
            calculatedRaycastTime = RaycastTime / originalMeleeAttackSpeed;
            calculatedGrazeTime = CustomGrazeCastTime / originalMeleeAttackSpeed;
            calculatedGrazeDuration = CustomGrazeCastDuration / originalMeleeAttackSpeed;
            calculatedImpactDuration = ImpactDuration / originalMeleeAttackSpeed;
            calculatedImpactPlaybackSpeed = ImpactPlaybackSpeed * originalMeleeAttackSpeed;
        }
        else
        {
            calculatedRaycastTime = 0.001f;
            calculatedGrazeTime = 0.001f;
            calculatedGrazeDuration = 0.001f;
            calculatedImpactDuration = 0.001f;
            calculatedImpactPlaybackSpeed = 0.001f;
        }
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"original: raycast time {RaycastTime} impact duration {ImpactDuration} impact playback speed {ImpactPlaybackSpeed} clip length {length}/{stateInfo.length}");
            Log.Out($"calculated: raycast time {calculatedRaycastTime} impact duration {calculatedImpactDuration} impact playback speed {calculatedImpactPlaybackSpeed} speed multiplier {originalMeleeAttackSpeed}");
        }
        GameManager.Instance.StartCoroutine(impactStart(animator, layerIndex, length));
        GameManager.Instance.StartCoroutine(customGrazeStart(length));
    }

    private IEnumerator impactStart(Animator animator, int layer, float length)
    {
        yield return new WaitForSeconds(Mathf.Max(calculatedRaycastTime, 0));
        if (!hasFired)
        {
            hasFired = true;
            if (entity != null && !entity.isEntityRemote && actionIndex >= 0)
            {
                ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
                if (itemActionDynamicMeleeData != null)
                {
                    if ((entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee).Raycast(itemActionDynamicMeleeData))
                    {
                        GameManager.Instance.StartCoroutine(impactStop(animator, layer, length));
                    }
                }
            }
        }
        yield break;
    }

    private IEnumerator impactStop(Animator animator, int layer, float length)
    {
        //playingImpact = true;
        //animator.Play(0, layer, Mathf.Min(1f, calculatedRaycastTime * attackDurationNormalized / length));
        if (animator)
        {
            //Log.Out("Impact start!");
            animator.SetWrappedFloat(AttackSpeedHash, calculatedImpactPlaybackSpeed);
        }
        speedMultiplierToKeep = calculatedImpactPlaybackSpeed;
        yield return new WaitForSeconds(calculatedImpactDuration);
        if (animator)
        {
            //Log.Out("Impact stop!");
            animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
        }
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        //playingImpact = false;
        yield break;
    }

    private IEnumerator customGrazeStart(float length)
    {
        if (ConsoleCmdReloadLog.LogInfo)
        {
            Log.Out($"Custom graze time: {calculatedGrazeTime} original {CustomGrazeCastTime}");
        }
        yield return new WaitForSeconds(calculatedGrazeTime);
        if (entity != null && !entity.isEntityRemote && actionIndex >= 0)
        {
            ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
            if (itemActionDynamicMeleeData != null)
            {
                GameManager.Instance.StartCoroutine(customGrazeUpdate(itemActionDynamicMeleeData));
            }
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
            yield break;
        }
        float grazeStart = Time.time;
        float normalizedTime = 0f;
        var action = entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee;
        while (normalizedTime <= 1)
        {
            if (!slotGurad.IsValid(data.invData.holdingEntity))
            {
                Log.Out($"Invalid graze!");
                yield break;
            }
            float originalSwingAngle = action.SwingAngle;
            float originalSwingDegrees = action.SwingDegrees;
            action.SwingAngle = SwingAngle;
            action.SwingDegrees = SwingDegrees;
            bool grazeResult = action.GrazeCast(data, normalizedTime);
            if (ConsoleCmdReloadLog.LogInfo)
            {
                Log.Out($"GrazeCast {grazeResult}!");
            }
            action.SwingAngle = originalSwingAngle;
            action.SwingDegrees = originalSwingDegrees;
            yield return null;
            normalizedTime = (Time.time - grazeStart) / calculatedGrazeDuration;
        }
        yield break;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //if (entity != null && !entity.isEntityRemote && actionIndex >= 0 && entity.inventory.holdingItemData.actionData[actionIndex] is ItemActionDynamicMelee.ItemActionDynamicMeleeData)
        //{
        //    animator.SetFloat(AttackSpeedHash, originalMeleeAttackSpeed);
        //}
        animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //float normalizedTime = stateInfo.normalizedTime;
        //if (float.IsInfinity(normalizedTime) || float.IsNaN(normalizedTime))
        //{
        //    animator.Play(animator.GetNextAnimatorStateInfo(layerIndex).shortNameHash, layerIndex);
        //}
        animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
    }
#endif
}
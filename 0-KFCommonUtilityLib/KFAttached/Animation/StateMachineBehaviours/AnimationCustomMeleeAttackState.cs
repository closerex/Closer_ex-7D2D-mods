using System.Collections;
using UnityEngine;
#if !UNITY_EDITOR
using KFCommonUtilityLib;
#endif

public class AnimationCustomMeleeAttackState : StateMachineBehaviour
{
    [Range(0f, 1f)]
    public float GlobalDamageMultiplier = 1f;

    public bool UseWindowDamage = false;

    // Half-width of the full-damage plateau in normalized window time.
    [Range(0f, 0.5f)]
    public float WindowDamageSigma = 0.2f;

    // Retained for old assets and the window-mode plateau center.
    public float RaycastTime = 0.3f;
    public float[] RaycastTimes = System.Array.Empty<float>();
    public bool GrazeAsRaycast = false;
    public bool UseRaycast = true;
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
    public int ForceActionIndex = -1;
    public bool IsAlternative = false;
    public bool InvariableRPM = false;
    public bool CancelLoopOnRelease = false;

    [SerializeField]
    private float ClipLength = 0f;

#if NotEditor
    private static readonly int AttackSpeedHash = Animator.StringToHash("MeleeAttackSpeed");
    private static readonly int MeleeRunningHash = Animator.StringToHash("IsMeleeRunning");
    private float calculatedRaycastTime;
    private float calculatedGrazeTime;
    private float calculatedGrazeDuration;
    private float calculatedGrazeInterval;
    private float calculatedImpactDuration;
    private float calculatedImpactPlaybackSpeed;
    private int actionIndex;
    private float originalMeleeAttackSpeed;
    private EntityAlive entity;
    private float attacksPerMinute;
    private float speedMultiplierToKeep = 1f;
    private InventorySlotGurad slotGurad = new();
    private ItemModuleMultiItem.MultiItemInvData multiInvData;
    private bool impactSpeedRestored;
    private bool forceStopCalled;
    private bool stateExited = true, stateGuarded = false;
    private bool windowDamageThisAttack;

    private Coroutine raycastCo, impactCo, grazeCo;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (raycastCo != null)
        {
            ThreadManager.StopCoroutine(raycastCo);
            raycastCo = null;
        }
        if (!stateExited)
        {
            stateGuarded = true;
        }
        stateExited = false;
        impactSpeedRestored = false;
        forceStopCalled = false;
        if (IsAlternative)
        {
            animator.SetWrappedBool(Animator.StringToHash("UseAltMelee"), false);
        }
        actionIndex = ForceActionIndex >= 0 ? ForceActionIndex : animator.GetWrappedInt(AvatarController.itemActionIndexHash);
        if (entity == null)
        {
            entity = animator.GetLocalPlayerInParent();
        }
        if (!slotGurad.IsValid(entity) || entity.isEntityRemote)
        {
            return;
        }
        float length = attackDurationNormalized > 0 ? ClipLength * attackDurationNormalized : ClipLength;
        attacksPerMinute = 60f / length;
        FastTags<TagGroup.Global> fastTags = ((actionIndex != 1) ? ItemActionAttack.PrimaryTag : ItemActionAttack.SecondaryTag);
        multiInvData = ItemClassModuleProcessor.LoadDataModule<ItemModuleMultiItem.MultiItemInvData>(entity.inventory.holdingItemData);
        bool isValidAlternative = IsAlternative && multiInvData != null;
        var prevData = SetParams(isValidAlternative);
        ItemActionDynamicMelee.ItemActionDynamicMeleeData itemActionDynamicMeleeData = entity.inventory.holdingItemData.actionData[actionIndex] as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
        if (itemActionDynamicMeleeData != null)
        {
            itemActionDynamicMeleeData.lastUseTime = Time.time;
            itemActionDynamicMeleeData.HasFinished = false;
        }
        ItemValue holdingItemItemValue = entity.inventory.holdingItemItemValue;
        ItemClass itemClass = holdingItemItemValue.ItemClass;
        if (itemClass != null)
        {
            fastTags |= itemClass.ItemTags;
        }
        float apm = EffectManager.GetValue(PassiveEffects.AttacksPerMinute, holdingItemItemValue, attacksPerMinute, entity, null, fastTags);
        originalMeleeAttackSpeed = InvariableRPM ? stateInfo.speed : apm / 60f * length;
        animator.SetWrappedFloat(AttackSpeedHash, InvariableRPM ? 1 : originalMeleeAttackSpeed);
        speedMultiplierToKeep = originalMeleeAttackSpeed;
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
            Log.Out($"Melee attack started: action index {actionIndex} alternative {isValidAlternative} original rpm {attacksPerMinute} calculated apm {apm} inv rpm {InvariableRPM}");
            if (itemClass != null)
            {
                Log.Out($"Item class: {itemClass.Name} tags: {itemClass.ItemTags.ToString()}");
            }
            else
            {
                Log.Out("Item class: null");
            }
        }
        windowDamageThisAttack = UseWindowDamage && CanRunWindowDamage(isValidAlternative);
        ClearWindowDamageActive();
        if (!windowDamageThisAttack && UseRaycast && !GrazeAsRaycast)
        { 
            raycastCo = ThreadManager.StartCoroutine(impactStart(animator, layerIndex, length));
        }
        if (windowDamageThisAttack)
        {
            grazeCo = ThreadManager.StartCoroutine(windowDamageStart(animator, layerIndex, length));
        }
        else if (UseGraze || GrazeAsRaycast)
        {
            grazeCo = ThreadManager.StartCoroutine(customGrazeStart(length));
        }
        RestoreParams(isValidAlternative, prevData);
        entity.emodel.avatarController.UpdateBool(MeleeRunningHash, true, true);
    }

    private IEnumerator impactStart(Animator animator, int layer, float length)
    {
        float startTime = Time.time;
        float[] times = RaycastTimes;
        bool hasTimes = times is { Length: > 0 };
        int count = hasTimes ? times.Length : 1;
        for (int i = 0; i < count; i++)
        {
            // Times are attack-start offsets; casts are serial and hit-stop does not delay the schedule.
            float triggerTime = hasTimes
                ? (originalMeleeAttackSpeed != 0f ? times[i] / originalMeleeAttackSpeed : 0.001f)
                : calculatedRaycastTime;
            float remaining = triggerTime - (Time.time - startTime);
            if (i == 0 || remaining > 0f)
            {
                yield return new WaitForSeconds(Mathf.Max(remaining, 0f));
            }
            if (entity == null || entity.isEntityRemote || actionIndex < 0
                || !slotGurad.IsValid(entity) || entity.IsDead()) break;

            bool isValidAlternative = IsAlternative && multiInvData is { boundInvData: not null };
            var prevData = SetParams(isValidAlternative);
            var data = entity.inventory.holdingItemData.actionData[actionIndex]
                as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
            if (data == null || (i > 0 && !CheckMeleeRunning(data)))
            {
                RestoreParams(isValidAlternative, prevData);
                break;
            }
            float alternativeDeltaUseTimes = isValidAlternative ? multiInvData.boundInvData.itemStack.itemValue.UseTimes : 0f;
            var action = (ItemActionDynamicMelee)entity.inventory.holdingItem.Actions[actionIndex];
            float originalSwingAngle = action.SwingAngle;
            float originalSwingDegrees = action.SwingDegrees;
            action.SwingAngle = SwingAngle;
            action.SwingDegrees = SwingDegrees;
            // A new configured cast must not inherit the previous cast's penetration ray.
            data.useExistingRay = false;
            bool hit = CastWithDamageMultiplier(action, data, true);
            action.SwingAngle = originalSwingAngle;
            action.SwingDegrees = originalSwingDegrees;
            if (hit)
            {
                if (isValidAlternative)
                {
                    multiInvData.originalData.itemStack.itemValue.UseTimes += multiInvData.boundInvData.itemStack.itemValue.UseTimes - alternativeDeltaUseTimes;
                }
                if (ConsoleCmdReloadLog.LogInfo)
                {
                    Log.Out("Raycast hit!");
                }
                if (impactCo != null)
                {
                    ThreadManager.StopCoroutine(impactCo);
                }
                impactCo = ThreadManager.StartCoroutine(impactStop(animator, layer, length));
            }
            else if (ConsoleCmdReloadLog.LogInfo)
            {
                Log.Out("Raycast miss!");
            }
            RestoreParams(isValidAlternative, prevData);
        }
        raycastCo = null;
    }

    private IEnumerator impactStop(Animator animator, int layer, float length)
    {
        impactSpeedRestored = false;
        if (animator)
        {
            animator.SetWrappedFloat(AttackSpeedHash, calculatedImpactPlaybackSpeed);
        }
        speedMultiplierToKeep = calculatedImpactPlaybackSpeed;
        animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
        yield return new WaitForSeconds(calculatedImpactDuration);
        if (animator)
        {
            animator.SetWrappedFloat(AttackSpeedHash, originalMeleeAttackSpeed);
        }
        speedMultiplierToKeep = originalMeleeAttackSpeed;
        animator.SetWrappedFloat(AttackSpeedHash, speedMultiplierToKeep);
        impactSpeedRestored = true;
        impactCo = null;
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
            bool isValidAlternative = IsAlternative && multiInvData is { boundInvData: not null };
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
            if (!GrazeAsRaycast || UseRaycast)
            {
                bool isValidAlternative = IsAlternative && multiInvData is { boundInvData: not null };
                var prevData = SetParams(isValidAlternative);
                var action = entity.inventory.holdingItem.Actions[actionIndex] as ItemActionDynamicMelee;
                if (action != null)
                {
                    float originalSwingAngle = action.SwingAngle;
                    float originalSwingDegrees = action.SwingDegrees;
                    action.SwingAngle = SwingAngle;
                    action.SwingDegrees = SwingDegrees;
                    // GrazeCast expects clip time, not normalized window progress.
                    bool grazeResult = CastWithDamageMultiplier(action, data, GrazeAsRaycast,
                        Mathf.Lerp(action.GrazeStart, action.GrazeEnd, normalizedTime % 1f));
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
                }
                else if (ConsoleCmdReloadLog.LogInfo)
                {
                    Log.Out($"GrazeCast invalid data! holdingItem {entity.inventory.holdingItem.Name} isAlt {isValidAlternative} actionIndex {actionIndex}");
                }
                RestoreParams(isValidAlternative, prevData);
            }
            if (attackDurationNormalized <= 0)
            {
                data.lastUseTime = Time.time;
            }

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

    // Loop attacks and raycast-as-graze retain legacy scheduling.
    private bool CanRunWindowDamage(bool isValidAlternative)
    {
        if (calculatedGrazeDuration <= 0f || attackDurationNormalized <= 0f || GrazeAsRaycast) return false;
        if (entity == null || entity.isEntityRemote || actionIndex < 0 || !slotGurad.IsValid(entity)) return false;
        if (entity.inventory?.holdingItem?.Actions == null) return false;
        if (!(entity.inventory.holdingItem.Actions[actionIndex] is ItemActionDynamicMelee)) return false;
        return entity.inventory.holdingItemData.actionData[actionIndex]
            .IsModuleContainerFor(out ActionModuleDynamicGraze.WindowDamageData _);
    }

    private void ClearWindowDamageActive()
    {
        if (entity == null || actionIndex < 0) return;
        if (entity.inventory?.holdingItem?.Actions == null) return;
        if (!(entity.inventory.holdingItem.Actions[actionIndex] is ItemActionDynamicMelee)) return;
        if (entity.inventory.holdingItemData.actionData[actionIndex]
            .IsModuleContainerFor(out ActionModuleDynamicGraze.WindowDamageData data))
        {
            data.Active = false;
        }
    }

    private IEnumerator windowDamageStart(Animator animator, int layerIndex, float length)
    {
        yield return new WaitForSeconds(Mathf.Max(calculatedGrazeTime, 0f));
        if (entity == null || entity.isEntityRemote || actionIndex < 0 || !slotGurad.IsValid(entity)) yield break;

        bool isValidAlternative = IsAlternative && multiInvData is { boundInvData: not null };
        var prevData = SetParams(isValidAlternative);
        var data = entity.inventory.holdingItemData.actionData[actionIndex]
            as ItemActionDynamicMelee.ItemActionDynamicMeleeData;
        RestoreParams(isValidAlternative, prevData);
        if (data == null || !data.IsModuleContainerFor(out ActionModuleDynamicGraze.WindowDamageData windowData))
        {
            grazeCo = null;
            yield break;
        }

        // Freeze the authored peak for this swing.
        float peakOffset = MeleeWindowDamage.PeakOffsetForRaycast(
            calculatedRaycastTime, calculatedGrazeTime, calculatedGrazeDuration);
        windowData.PeakOffset = peakOffset;
        windowData.Sigma = WindowDamageSigma;

        // Keep hit history for the whole swing so direct and grazing hits share it.
        // GrazeCanHitSameTarget is intentionally ignored in window mode.
        data.alreadyHitEnts.Clear();
        data.alreadyHitBlocks.Clear();
        data.alreadyHitProps?.Clear();
        data.lastWeaponHeadPosition = Vector3.zero;

        float grazeStart = Time.time;
        float normalizedTime = 0f;

        float previousPhase = -1f;
        bool directHitDone = false;
        bool raycastHitDone = false;
        bool centerRaycastDone = false;

        // One post-window sample covers plateaus skipped between frames.
        bool finalTick = false;
        while (finalTick || normalizedTime <= 1 || attackDurationNormalized <= 0)
        {
            if (!slotGurad.IsValid(entity) || entity.IsDead()) break;

            prevData = SetParams(isValidAlternative);
            if (entity.inventory.holdingItem.Actions[actionIndex] is ItemActionDynamicMelee action
                && data.invData.itemValue.PercentUsesLeft > 0f
                && !entity.Hand.IsSwitching)
            {
                float originalSwingAngle = action.SwingAngle;
                float originalSwingDegrees = action.SwingDegrees;
                action.SwingAngle = SwingAngle;
                action.SwingDegrees = SwingDegrees;

                bool centerRaycastDue = !centerRaycastDone && normalizedTime >= 0.5f;
                float alternativeDeltaUseTimes = isValidAlternative ? multiInvData.boundInvData.itemStack.itemValue.UseTimes : 0f;

                // Classify every crossed sample, not the whole frame, by its own window phase.
                windowData.Active = true;
                bool hit = ProcessWindowSweep(action, data, previousPhase, normalizedTime, windowData,
                    ref directHitDone, ref raycastHitDone, out bool raycastThisTick);

                // Sweep first; only the one midpoint sight ray may also hit solids.
                if (centerRaycastDue)
                {
                    windowData.Phase = 0.5f;
                    hit |= CastWindowRaycast(action, data, entity.GetMeleeRay(), windowData,
                        true, ref directHitDone, ref raycastHitDone);
                    centerRaycastDone = true;
                }
                if (hit)
                {
                    if (isValidAlternative)
                    {
                        multiInvData.originalData.itemStack.itemValue.UseTimes += multiInvData.boundInvData.itemStack.itemValue.UseTimes - alternativeDeltaUseTimes;
                    }
                    if (impactCo != null)
                    {
                        ThreadManager.StopCoroutine(impactCo);
                    }
                    impactCo = ThreadManager.StartCoroutine(impactStop(animator, layerIndex, length));
                }
                if ((raycastThisTick || centerRaycastDue) && entity.emodel.avatarController)
                {
                    entity.emodel.avatarController.UpdateBool("RayHit", hit);
                }

                previousPhase = Mathf.Clamp01(normalizedTime);
                windowData.Active = false;

                action.SwingAngle = originalSwingAngle;
                action.SwingDegrees = originalSwingDegrees;

            }
            RestoreParams(isValidAlternative, prevData);

            if (attackDurationNormalized <= 0)
            {
                data.lastUseTime = Time.time;
            }
            if (!CheckMeleeRunning(data)) break;

            yield return calculatedGrazeInterval > 0f ? new WaitForSeconds(calculatedGrazeInterval) : null;
            normalizedTime = (Time.time - grazeStart) / calculatedGrazeDuration;

            if (finalTick) break;
            finalTick = attackDurationNormalized > 0f && normalizedTime > 1f;
        }
        grazeCo = null;
    }

    private Ray GetWindowSweepRay(ItemActionDynamicMelee action,
        ItemActionDynamicMelee.ItemActionDynamicMeleeData data, float phase, out Vector3 weaponHead)
    {
        Ray ray = entity.GetMeleeRay();
        Transform cameraTransform = ((EntityPlayerLocal)entity).cameraTransform;
        ray.direction = Quaternion.AngleAxis(Mathf.Lerp(action.SwingDegrees * 0.5f,
            -action.SwingDegrees * 0.5f, phase), cameraTransform.right) * ray.direction;
        if (action.SwingAngle != 0f)
        {
            ray.direction = Quaternion.AngleAxis(action.SwingAngle, cameraTransform.forward) * ray.direction;
        }
        float range = EffectManager.GetValue(PassiveEffects.MaxRange, data.invData.itemValue,
            2f, entity, null, data.ActionTags);
        weaponHead = ray.origin + ray.direction * range;
        return ray;
    }

    private bool ProcessWindowSweep(ItemActionDynamicMelee action,
        ItemActionDynamicMelee.ItemActionDynamicMeleeData data, float previousPhase, float currentPhase,
        ActionModuleDynamicGraze.WindowDamageData windowData,
        ref bool directHitDone, ref bool raycastHitDone, out bool raycastThisTick)
    {
        float from = Mathf.Max(previousPhase, 0f);
        float to = Mathf.Clamp01(currentPhase);
        float peak = MeleeWindowDamage.PeakPhase(windowData.PeakOffset);
        float width = MeleeWindowDamage.DirectWindowWidth(windowData.Sigma, windowData.PeakOffset);
        float plateauStart = peak - width;
        float plateauEnd = peak + width;
        float spacing = action.SphereRadius * 1.25f;
        if (spacing == 0f) spacing = 0.15f;
        float range = EffectManager.GetValue(PassiveEffects.MaxRange, data.invData.itemValue,
            2f, entity, null, data.ActionTags);
        bool hit = false;
        raycastThisTick = false;

        // Emit phase zero once, then process (previousPhase, currentPhase] including each boundary.
        if (previousPhase < 0f)
            hit |= CastWindowSample(action, data, from, windowData,
                ref directHitDone, ref raycastHitDone, ref raycastThisTick);
        while (from < to)
        {
            float end = to;
            if (from < plateauStart && plateauStart < end) end = plateauStart;
            if (from < plateauEnd && plateauEnd < end) end = plateauEnd;
            float arcLength = Mathf.Abs(action.SwingDegrees) * Mathf.Deg2Rad * range * (end - from);
            int steps = Mathf.Max(1, Mathf.CeilToInt(arcLength / spacing));
            for (int i = 1; i <= steps; i++)
            {
                float phase = i == steps ? end : Mathf.Lerp(from, end, (float)i / steps);
                hit |= CastWindowSample(action, data, phase, windowData,
                    ref directHitDone, ref raycastHitDone, ref raycastThisTick);
            }
            from = end;
        }
        return hit;
    }

    private bool CastWindowSample(ItemActionDynamicMelee action,
        ItemActionDynamicMelee.ItemActionDynamicMeleeData data, float phase,
        ActionModuleDynamicGraze.WindowDamageData windowData,
        ref bool directHitDone, ref bool raycastHitDone, ref bool raycastThisTick)
    {
        Ray ray = GetWindowSweepRay(action, data, phase, out Vector3 weaponHead);
        windowData.Phase = phase;
        data.lastWeaponHeadPosition = weaponHead;
        float peak = MeleeWindowDamage.PeakPhase(windowData.PeakOffset);
        float width = MeleeWindowDamage.DirectWindowWidth(windowData.Sigma, windowData.PeakOffset);
        if (phase >= peak - width && phase <= peak + width)
        {
            raycastThisTick = true;
            return CastWindowRaycast(action, data, ray, windowData, false,
                ref directHitDone, ref raycastHitDone);
        }

        // Seed the native endpoint so GrazeCast samples this phase instead of resweeping history.
        CastWithDamageMultiplier(action, data, false, Mathf.Lerp(action.GrazeStart, action.GrazeEnd, phase));
        return false;
    }

    private bool CastWindowRaycast(ItemActionDynamicMelee action,
        ItemActionDynamicMelee.ItemActionDynamicMeleeData data, Ray ray,
        ActionModuleDynamicGraze.WindowDamageData windowData, bool allowBlockHits,
        ref bool directHitDone, ref bool raycastHitDone)
    {
        Ray previousRay = data.ray;
        Vector3 previousRayStart = data.rayStartPos;
        bool previousUseExistingRay = data.useExistingRay;
        ray.origin -= ray.direction * action.SphereRadius;
        data.ray = ray;
        data.rayStartPos = ray.origin;
        data.useExistingRay = true;

        // Only the first direct cast pays natively; later successful casts use graze stamina scaling.
        float staminaUsage = data.StaminaUsage;
        if (directHitDone) data.StaminaUsage = 0f;
        directHitDone = true;
        windowData.AllowBlockHits = allowBlockHits;
        bool hit = CastWithDamageMultiplier(action, data, true);
        windowData.AllowBlockHits = false;
        data.StaminaUsage = staminaUsage;
        if (hit)
        {
            if (raycastHitDone)
            {
                entity.Stats.Stamina.Value -= staminaUsage * EffectManager.GetValue(
                    PassiveEffects.GrazeStaminaMultiplier,
                    data.invData.holdingEntity.inventory.holdingItemItemValue,
                    action.GrazeStaminaPercentage * ItemActionAttack.StaminaUsageMultiplier,
                    data.invData.holdingEntity, null, data.invData.holdingEntity.inventory.holdingItem.ItemTags);
            }
            raycastHitDone = true;
        }

        data.ray = previousRay;
        data.rayStartPos = previousRayStart;
        data.useExistingRay = previousUseExistingRay;
        return hit;
    }

    // Restore the previous scale so nested casts and shared action data stay isolated.
    private bool CastWithDamageMultiplier(ItemActionDynamicMelee action,
        ItemActionDynamicMelee.ItemActionDynamicMeleeData data, bool raycast, float grazeTime = 0f)
    {
        bool hasModule = data.IsModuleContainerFor(out ActionModuleDynamicGraze.WindowDamageData damageData);
        float previousMultiplier = hasModule ? damageData.GlobalDamageMultiplier : 1f;
        if (hasModule)
        {
            damageData.GlobalDamageMultiplier = GlobalDamageMultiplier;
        }
        bool hit = raycast ? action.Raycast(data) : action.GrazeCast(data, grazeTime);
        if (hasModule)
        {
            damageData.GlobalDamageMultiplier = previousMultiplier;
        }
        return hit;
    }

    private void ForceStopCoroutines()
    {
        if (forceStopCalled)
        {
            return;
        }
        if (raycastCo != null)
        {
            ThreadManager.StopCoroutine(raycastCo);
            raycastCo = null;
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
            bool isValidAlternative = IsAlternative && multiInvData is { boundInvData: not null };
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
        if (data.invData.itemValue.PercentUsesLeft <= 0f || data.invData.holdingEntity.Stamina < data.StaminaUsage || data.invData.holdingEntity.Hand.IsSwitching || (attackDurationNormalized <= 0 && data.HasReleased))
        {
            if (ConsoleCmdReloadLog.LogInfo)
                Log.Out($"Stopping melee running: {attackDurationNormalized} {data.HasReleased}");
            data.invData.holdingEntity.emodel.avatarController.UpdateBool(MeleeRunningHash, false, true);
            data.HasExecuted = true;
            return false;
        }
        return true;
    }
#endif
}
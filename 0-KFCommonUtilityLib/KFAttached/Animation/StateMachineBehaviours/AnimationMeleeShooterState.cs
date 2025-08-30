using KFCommonUtilityLib;
using UnityEngine;
#if UNITY_EDITOR
using System;
#elif NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif

public class AnimationMeleeShooterState : StateMachineBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
#if UNITY_EDITOR
    [Serializable]
    public struct State
    {
        public float execTime;
        public bool comsumeAmmo;
    }

    public State[] states;
#endif
    public string exitTransitionName = "MeleeShooterStateExit";
    [HideInInspector]
    public float[] stateData;
    [HideInInspector]
    public bool[] ammoData;

#if NotEditor
    private float currentPerc;
    private int actionIndex;
    private EntityPlayerLocal player;
    private ActionModuleMeleeShooter.MeleeShooterData data;
    private IAnimatorWrapper wrapper;
    private bool requestReset;
#endif

#if UNITY_EDITOR
    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
        if (states != null)
        {
            stateData = new float[states.Length];
            ammoData = new bool[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                stateData[i] = states[i].execTime;
                ammoData[i] = states[i].comsumeAmmo;
            }
        }
    }
#endif

#if NotEditor
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null)
        {
            player = animator.GetComponentInParent<EntityPlayerLocal>();
            if (player == null)
            {
                Log.Warning($"AnimationMeleeShooterState: Could not find EntityPlayerLocal. This state will not function correctly.");
                return;
            }
        }

        if (wrapper == null || !wrapper.IsValid)
        {
            wrapper = animator.GetItemAnimatorWrapper();
        }

        currentPerc = 0;
        var activeRig = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
        if (activeRig)
        {
            var invData = player.inventory?.slots?[activeRig.SlotIndex];
            if (invData != null)
            {
                actionIndex = MultiActionManager.GetActionIndexForEntity(player);
                data = (player.inventory?.GetItemDataInSlot(activeRig.SlotIndex)?.actionData?[actionIndex] as IModuleContainerFor<ActionModuleMeleeShooter.MeleeShooterData>)?.Instance;
                if (data != null)
                {
                    data.animationRequested = true;
                }
            }
        }
        requestReset = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (data == null || stateData == null || ammoData == null || stateData.Length != ammoData.Length)
            return;

        var transInfo = wrapper.GetAnimatorTransitionInfo(layerIndex);
        if (transInfo.IsUserName(exitTransitionName))
        {
            data.ResetRequest();
            requestReset = true;
            return;
        }

        float lastPerc = currentPerc;
        currentPerc = stateInfo.normalizedTime % 1;
        for (int i = 0; i < stateData.Length; i++)
        {
            if (stateData[i] >= lastPerc && stateData[i] < currentPerc)
            {
                data.executionRequested = true;
                ItemActionRanged rangedAction = data.invData.item.Actions[actionIndex] as ItemActionRanged;
                bool wasInfiniteAmmo = rangedAction.InfiniteAmmo;
                rangedAction.InfiniteAmmo = !ammoData[i];
                var rangedData = data.invData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
                rangedData.m_LastShotTime = -1;
                rangedAction.ExecuteAction(rangedData, false);
                rangedAction.ExecuteAction(rangedData, true);
                rangedAction.InfiniteAmmo = wasInfiniteAmmo;
                data.executionRequested = false;
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (data != null && !requestReset)
        {
            data.ResetRequest();
        }
        requestReset = false;
        currentPerc = 0;
    }
#endif
}
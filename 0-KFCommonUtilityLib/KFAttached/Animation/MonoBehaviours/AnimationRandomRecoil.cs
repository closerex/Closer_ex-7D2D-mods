﻿using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;

[AddComponentMenu("KFAttachments/Binding Helpers/Animation Random Recoil")]
public class AnimationRandomRecoil : AnimationProceduralRecoildAbs
{
    [Header("Targets")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform pivot;
    [Header("Rotation")]
    //[SerializeField] private Vector3 minRotation = new Vector3(-5, -2, -2);
    //[SerializeField] private Vector3 maxRotation = new Vector3(0, 2, 2);
    [SerializeField] private Vector3 randomRotationMin = new Vector3(-3, -1, -1);
    [SerializeField] private Vector3 randomRotationMax = new Vector3(-1, 1, 1);
    [Header("Kickback")]
    //[SerializeField] private Vector3 minKickback = new Vector3(0, 0, -0.05f);
    //[SerializeField] private Vector3 maxKickback = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 randomKickbackMin = new Vector3(0, 0, -0.025f);
    [SerializeField] private Vector3 randomKickbackMax = new Vector3(0, 0, -0.01f);
    [Header("Recoil")]
    [SerializeField, Range(0, 0.1f)] private float tweenInDuration = 0.04f;
    [SerializeField, Range(0, 5)] private float tweenOutDuration = 1.5f;
    [Header("Return")]
    [SerializeField, Range(1, 5)] private float elasticAmplitude = 1f;
    [SerializeField, Range(0, 1)] private float elasticPeriod = 0.5f;

    private Vector3 targetRotation = Vector3.zero;
    private Vector3 targetPosition = Vector3.zero;
    private Vector3 currentRotation = Vector3.zero;
    private Vector3 currentPosition = Vector3.zero;
    private Sequence seq;
    //private bool isTweeningIn = true;

    private void Awake()
    {
        if (pivot == null || target == null)
        {
            Destroy(this);
        }
    }

    public override void AddRecoil(Vector3 positionMultiplier, Vector3 rotationMultiplier)
    {
        targetPosition = Vector3.Scale(KFExtensions.Random(randomKickbackMin, randomKickbackMax), positionMultiplier);
        targetRotation = Vector3.Scale(KFExtensions.Random(randomRotationMin, randomRotationMax), rotationMultiplier);
        GameObject targetObj = target.gameObject;
        RecreateSeq();
    }

    private void OnEnable()
    {
        ResetSeq();
    }

    private void OnDisable()
    {
        ResetSeq();
    }

    private void ResetSeq()
    {
        seq?.Rewind(false);
        target.localEulerAngles = Vector3.zero;
        target.localPosition = Vector3.zero;
    }

    private void RecreateSeq()
    {
        currentPosition = Vector3.zero;
        currentRotation = Vector3.zero;
        seq?.Kill(false);
        seq = DOTween.Sequence()
                     //.InsertCallback(0, () => isTweeningIn = true)
                     .Insert(0, DOTween.To(() => currentRotation, (rot) => currentRotation = rot, targetRotation, tweenInDuration).SetEase(Ease.OutCubic))
                     .Insert(0, DOTween.To(() => currentPosition, (pos) => currentPosition = pos, targetPosition, tweenInDuration).SetEase(Ease.OutCubic))
                     //.InsertCallback(tweenInDuration, () => isTweeningIn = false)
                     .Insert(tweenInDuration, DOTween.To(() => currentRotation, (rot) => currentRotation = rot, Vector3.zero, tweenOutDuration).SetEase(Ease.OutElastic, elasticAmplitude, elasticPeriod))
                     .Insert(tweenInDuration, DOTween.To(() => currentPosition, (rot) => currentPosition = rot, Vector3.zero, tweenOutDuration).SetEase(Ease.OutElastic, elasticAmplitude, elasticPeriod))
                     .OnUpdate(UpdateTransform)
                     .SetAutoKill(true)
                     .SetRecyclable();
    }

    private void UpdateTransform()
    {
        target.localEulerAngles = Vector3.zero;
        target.localPosition = Vector3.zero;
        target.RotateAroundPivot(pivot, currentRotation);
        target.localPosition += currentPosition;
        //if (!isTweeningIn)
        //{
        //    targetRotation = currentRotation;
        //    targetPosition = currentPosition;
        //}
    }
}
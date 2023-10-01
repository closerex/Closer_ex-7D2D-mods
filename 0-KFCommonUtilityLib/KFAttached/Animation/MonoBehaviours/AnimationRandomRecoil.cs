using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Unity.Mathematics;
using UnityEngine;

public class AnimationRandomRecoil : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform pivot;
    [SerializeField] private Vector3 minRotation = new Vector3(-5, -2, -2);
    [SerializeField] private Vector3 maxRotation = new Vector3(0, 2, 2);
    [SerializeField] private Vector3 randomRotationMin = new Vector3(-3, -1, -1);
    [SerializeField] private Vector3 randomRotationMax = new Vector3(-1, 1, 1);
    [SerializeField] private Vector3 minKickback = new Vector3(0, 0, -0.05f);
    [SerializeField] private Vector3 maxKickback = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 randomKickbackMin = new Vector3(0, 0, -0.025f);
    [SerializeField] private Vector3 randomKickbackMax = new Vector3(0, 0, -0.01f);
    [SerializeField, Range(0, 0.1f)] private float tweenInDuration = 0.04f;
    [SerializeField, Range(0, 5)] private float tweenOutDuration = 1.5f;
    [SerializeField, Range(1, 5)] private float elasticAmplitude = 1f;
    [SerializeField, Range(0, 1)] private float elasticPeriod = 0.5f;

    private Vector3 targetRotation = Vector3.zero;
    private Vector3 targetPosition = Vector3.zero;
    private Vector3 currentRotation = Vector3.zero;
    private Vector3 currentPosition = Vector3.zero;
    private Sequence seq;
    private bool isTweeningIn = true;

    private void Awake()
    {
        if (pivot == null || target == null)
        {
            Destroy(this);
        }
    }

    public void AddRecoil(Vector3 positionMultiplier, Vector3 rotationMultiplier)
    {
        targetPosition = math.clamp(targetPosition + Vector3.Scale(KFExtensions.Random(randomKickbackMin, randomKickbackMax), positionMultiplier), minKickback, maxKickback);
        targetRotation = math.clamp(targetRotation + Vector3.Scale(KFExtensions.Random(randomRotationMin, randomRotationMax), rotationMultiplier), minRotation, maxRotation);
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
        seq.Rewind(false);
        target.localEulerAngles = Vector3.zero;
        target.localPosition = Vector3.zero;
    }

    private void RecreateSeq()
    {
        seq?.Kill(false);
        seq = DOTween.Sequence()
                     .InsertCallback(0, () => isTweeningIn = true)
                     .Insert(0, DOTween.To(() => currentRotation, (rot) => currentRotation = rot, targetRotation, tweenInDuration).SetEase(Ease.OutCubic))
                     .Insert(0, DOTween.To(() => currentPosition, (pos) => currentPosition = pos, targetPosition, tweenInDuration).SetEase(Ease.OutCubic))
                     .InsertCallback(tweenInDuration, () => isTweeningIn = false)
                     .Insert(tweenInDuration, DOTween.To(() => currentRotation, (rot) => currentRotation = rot, Vector3.zero, tweenOutDuration).SetEase(Ease.OutElastic, elasticAmplitude, elasticPeriod))
                     .Insert(tweenInDuration, DOTween.To(() => currentPosition, (rot) => currentPosition = rot, Vector3.zero, tweenOutDuration).SetEase(Ease.OutElastic, elasticAmplitude, elasticPeriod))
                     .OnUpdate(UpdateTransform)
                     .SetAutoKill(true)
                     .SetRecyclable();
    }

    private void UpdateTransform()
    {
        target.RotateAroundPivot(pivot, currentRotation);
        target.localPosition += currentPosition;
        if(!isTweeningIn)
        {
            targetRotation = currentRotation;
            targetPosition = currentPosition;
        }
    }
}
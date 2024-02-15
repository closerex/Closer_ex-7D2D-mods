using UnityEngine;

[AddComponentMenu("KFAttachments/Binding Helpers/Animation Aim Recoil References")]
public class AnimationAimRecoilReferences : MonoBehaviour
{
    [SerializeField]
    private Transform[] aimRecoilTargets;
    private Vector3[] initialPositions;

    private void Start()
    {
        if (aimRecoilTargets != null)
        {
            initialPositions = new Vector3[aimRecoilTargets.Length];
            for (int i = 0; i < aimRecoilTargets.Length; i++)
            {
                initialPositions[i] = aimRecoilTargets[i].localPosition;
            }
        }
    }

    public void Rollback()
    {
        if (aimRecoilTargets != null)
        {
            for (int i = 0; i < aimRecoilTargets.Length; i++)
            {
                aimRecoilTargets[i].localPosition = initialPositions[i];
            }
        }
    }
}
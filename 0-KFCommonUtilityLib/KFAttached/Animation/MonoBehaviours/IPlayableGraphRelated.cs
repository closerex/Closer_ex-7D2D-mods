using UnityEngine;

public interface IPlayableGraphRelated
{
    MonoBehaviour Init(Transform playerAnimatorTrans, bool isLocalPlayer);
    void Disable(Transform playerAnimatorTrans);
}
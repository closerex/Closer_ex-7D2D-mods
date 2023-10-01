using UnityEngine;

[AddComponentMenu("KFAttachments/Weapon Display Controllers/Charge Up controller")]
public class ChargeUpController : MonoBehaviour
{
    [SerializeField]
    private WeaponLabelControllerChargeUp controller;
    private void OnEnable()
    {
        controller.StartChargeUp();
    }

    private void OnDisable()
    {
        controller.StopChargeUp();
    }
}

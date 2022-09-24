using UnityEngine;

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

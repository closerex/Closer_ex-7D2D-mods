using UnityEngine;

public class WeaponDataController : WeaponLabelControllerBase
{
    [SerializeField]
    private WeaponDataHandlerBase[] handlers;
    public override bool setLabelColor(int index, Color color)
    {
        if (handlers == null || index >= handlers.Length || index < 0 || !handlers[index] || !handlers[index].gameObject.activeSelf)
            return false;

        handlers[index]?.SetColor(color);
        return true;
    }

    public override bool setLabelText(int index, string data)
    {
        if (handlers == null || index >= handlers.Length || index < 0 || !handlers[index] || !handlers[index].gameObject.activeSelf)
            return false;

        handlers[index]?.SetText(data);
        return true;
    }
}
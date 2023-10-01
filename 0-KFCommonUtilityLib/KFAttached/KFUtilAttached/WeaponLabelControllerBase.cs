using UnityEngine;

public abstract class WeaponLabelControllerBase : MonoBehaviour
{
    public abstract bool setLabelText(int index, string data);
    public abstract bool setLabelColor(int index, Color color);
}

using UnityEngine;

public abstract class WeaponColorControllerBase : MonoBehaviour
{
    public abstract bool setMaterialColor(int renderer_index, int material_index, int nameId, Color data);
}

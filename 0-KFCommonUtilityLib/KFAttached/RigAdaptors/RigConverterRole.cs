using UnityEngine;

[AddComponentMenu("KFAttachments/RigAdaptors/Rig Converter Ignore")]
public class RigConverterRole : MonoBehaviour
{
    public enum Role
    {
        Normal,
        Reverse,
        Ignore
    }

    public Role role;
}

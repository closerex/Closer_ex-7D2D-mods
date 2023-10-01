using UnityEngine;

[AddComponentMenu("KFAttachments/Weapon Display Controllers/Weapon Color Controller")]
public class WeaponColorController : WeaponColorControllerBase
{
    [SerializeField]
    protected Renderer[] renderers;

    public override bool setMaterialColor(int renderer_index, int material_index, int nameId, Color data)
    {
        if (renderers == null || renderers.Length <= renderer_index || !renderers[renderer_index].gameObject.activeInHierarchy || renderers[renderer_index].materials.Length <= material_index)
            return false;
        renderers[renderer_index].materials[material_index].SetColor(nameId, data);
        return true;
    }
}

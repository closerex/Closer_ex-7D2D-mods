using UnityEngine;

public class WeaponLabelController : MonoBehaviour
{
    public TextMesh[] labels;
    public Renderer[] renderers;

    public void setLabelText(int index, string data)
    {
        if (labels == null || labels.Length <= index)
            return;
        labels[index].text = data;
    }

    public void setLabelColor(int index, Color color)
    {
        if (labels == null || labels.Length <= index)
            return;
        labels[index].color = color;
    }

    public void setMaterialColor(int renderer_index, int material_index, int nameId, Color data)
    {
        if (renderers == null || renderers.Length <= renderer_index || renderers[renderer_index].materials.Length <= material_index)
            return;
        renderers[renderer_index].materials[material_index].SetColor(nameId, data);
    }
}

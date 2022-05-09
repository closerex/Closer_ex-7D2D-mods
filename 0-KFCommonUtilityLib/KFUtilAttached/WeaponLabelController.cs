using UnityEngine;

public class WeaponLabelController : MonoBehaviour
{
    public TextMesh[] labels;
    public Renderer[] renderers;

    public bool setLabelText(int index, string data)
    {
        if (string.Equals(labels[index].text, data))
            return false;
        labels[index].text = data;
        return true;
    }

    public bool setLabelColor(int index, Color color)
    {
        if (labels[index].color.Equals(color))
            return false;
        labels[index].color = color;
        return true;
    }

    public bool setMaterialColor(int renderer_index, int material_index, int nameId, Color data)
    {
        if (renderers[renderer_index].materials[material_index].GetColor(nameId).Equals(data))
            return false;
        renderers[renderer_index].materials[material_index].SetColor(nameId, data);
        return true;
    }
}

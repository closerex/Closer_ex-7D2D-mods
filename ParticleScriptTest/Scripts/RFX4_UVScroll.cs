using UnityEngine;

public class RFX4_UVScroll : MonoBehaviour {

    public Vector2 UvScrollMultiplier = new Vector2(1.0f, 0.0f);
    public RFX4_TextureShaderProperties TextureName = RFX4_TextureShaderProperties._MainTex;

    Vector2 uvOffset = Vector2.zero;

    private Material mat;

    void Start()
    {
        var currentRenderer = GetComponent<Renderer>();
        if (currentRenderer == null)
        {
            var projector = GetComponent<Projector>();
            if (projector != null)
            {
                if (!projector.material.name.EndsWith("(Instance)"))
                    projector.material = new Material(projector.material) { name = projector.material.name + " (Instance)" };
                mat = projector.material;
            }
        }
        else
            mat = currentRenderer.material;
    }

    void Update()
    {
        uvOffset += (UvScrollMultiplier * Time.deltaTime);
        if (mat!=null)
        {
            mat.SetTextureOffset(TextureName.ToString(), uvOffset);
        }
    }
}

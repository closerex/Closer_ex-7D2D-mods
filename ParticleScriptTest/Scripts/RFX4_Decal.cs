using UnityEngine;
using UnityEngine.Rendering;

#if KRIPTO_FX_LWRP_RENDERING
using UnityEngine.Experimental.Rendering.LightweightPipeline;
#endif

[ExecuteInEditMode]
public class RFX4_Decal : MonoBehaviour
{
    public bool IsScreenSpace = true;

    // Material mat;
    ParticleSystem ps;
    ParticleSystem.MainModule psMain;
    private MaterialPropertyBlock props;
    MeshRenderer rend;

    private void OnEnable()
    {
        //if (Application.isPlaying) mat = GetComponent<Renderer>().material;
        //else mat = GetComponent<Renderer>().sharedMaterial;

        ps = GetComponent<ParticleSystem>();
        if (ps != null) psMain = ps.main;

        if (Camera.main.depthTextureMode != DepthTextureMode.Depth) Camera.main.depthTextureMode = DepthTextureMode.Depth;

        GetComponent<MeshRenderer>().reflectionProbeUsage = ReflectionProbeUsage.Off;

#if KRIPTO_FX_LWRP_RENDERING
        var addCamData = Camera.main.GetComponent<LWRPAdditionalCameraData>();
        if (addCamData != null) IsScreenSpace = addCamData.requiresDepthTexture;
#endif

        if (!IsScreenSpace)
        {
            var sharedMaterial = GetComponent<Renderer>().sharedMaterial;
            sharedMaterial.EnableKeyword("USE_QUAD_DECAL");
            sharedMaterial.SetInt("_ZTest1", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            if (Application.isPlaying)
            {
                var pos = transform.localPosition;
                pos.z += 0.1f;
                transform.localPosition = pos;
                var scale = transform.localScale;
                scale.y = 0.001f;
                transform.localScale = scale;
            }
        }
        else
        {
            var sharedMaterial = GetComponent<Renderer>().sharedMaterial;
            sharedMaterial.DisableKeyword("USE_QUAD_DECAL");
            sharedMaterial.SetInt("_ZTest1", (int)UnityEngine.Rendering.CompareFunction.Greater);
        }
    }

    void LateUpdate()
    {
        Matrix4x4 invTransformMatrix = transform.worldToLocalMatrix;
        // mat.SetMatrix("_InverseTransformMatrix", invTransformMatrix);
        if (props == null) props = new MaterialPropertyBlock();
        if (rend == null) rend = GetComponent<MeshRenderer>();
        rend.GetPropertyBlock(props);
       
        props.SetMatrix("_InverseTransformMatrix", invTransformMatrix);
        rend.SetPropertyBlock(props);
        
        if (ps != null) psMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(this.transform.TransformPoint(Vector3.zero), this.transform.rotation, this.transform.lossyScale);
        Gizmos.color = new Color(1, 1, 1, 1);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}

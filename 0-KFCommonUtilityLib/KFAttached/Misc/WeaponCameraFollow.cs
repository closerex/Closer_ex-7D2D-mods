using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("")]
public class WeaponCameraFollow : MonoBehaviour
{
    public RenderTexture targetTexture;

    private void OnEnable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(true);
#endif
    }

    private void OnDisable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(false);
#endif
        if (!targetTexture || !targetTexture.IsCreated())
        {
            return;
        }
        var cmd = new CommandBuffer();
        cmd.SetRenderTarget(targetTexture);
        cmd.ClearRenderTarget(true, true, Color.black);
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Dispose();
    }
}

using UnityEngine;
using UnityEngine.Rendering;

[AddComponentMenu("")]
public class WeaponCameraFollow : MonoBehaviour
{
    public RenderTexture targetTexture;
#if NotEditor
    public ActionModuleDynamicSensitivity.DynamicSensitivityData dynamicSensitivityData;
#endif

    private void OnEnable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(true);
        if (dynamicSensitivityData != null)
        {
            dynamicSensitivityData.activated = true;
        }
#endif
    }

    private void OnDisable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(false);
        if (dynamicSensitivityData != null)
        {
            dynamicSensitivityData.activated = false;
        }
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

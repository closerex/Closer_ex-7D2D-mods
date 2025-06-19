using UnityEngine;
using UnityEngine.Rendering;
#if NotEditor
using UnityEngine.Rendering.PostProcessing;
#endif

[AddComponentMenu("")]
public class WeaponCameraFollow : MonoBehaviour
{
    public RenderTexture targetTexture;
#if NotEditor
    public ActionModuleDynamicSensitivity.DynamicSensitivityData dynamicSensitivityData;
    public EntityPlayerLocal player;
#endif

    private void OnEnable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(true);
        if (dynamicSensitivityData != null)
        {
            dynamicSensitivityData.activated = true;
        }
        //UpdateAntialiasing();
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

#if NotEditor
    //public void UpdateAntialiasing()
    //{
    //    var pipCamera = GetComponent<Camera>();
    //    var layer = GetComponent<PostProcessLayer>();
    //    var prevFsr = player.renderManager.fsr;
    //    int num = player.renderManager.dlssEnabled ? 0 : GamePrefs.GetInt(EnumGamePrefs.OptionsGfxAA);
    //    float @float = GamePrefs.GetFloat(EnumGamePrefs.OptionsGfxAASharpness);
    //    player.renderManager.FSRInit(layer.superResolution);
    //    player.renderManager.SetAntialiasing(num, @float, layer);
    //    Rect rect = pipCamera.rect;
    //    rect.x = ((layer.antialiasingMode == PostProcessLayer.Antialiasing.SuperResolution) ? 1E-07f : 0f);
    //    pipCamera.rect = rect;
    //    player.renderManager.fsr = prevFsr;
    //}
#endif
}

using KFCommonUtilityLib.KFAttached.Render;
using System;
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
    public MagnifyScope magnifyScope;
#endif

    private void OnEnable()
    {
#if NotEditor
        OcclusionManager.Instance.SetMultipleCameras(true);
        if (dynamicSensitivityData != null)
        {
            dynamicSensitivityData.activated = true;
        }
        UpdateAntialiasing();
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
    public void UpdateAntialiasing()
    {
        if (!player)
        {
            return;
        }

        var layer = GetComponent<PostProcessLayer>();
        if (!layer)
        {
            return;
        }

        var prevLayer = player.renderManager.layer;
        player.renderManager.layer = layer;
        try
        {
            if (layer)
            {
                float sharpness = GamePrefs.GetFloat(EnumGamePrefs.OptionsGfxAASharpness);
                int upscalerMode = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxUpscalerMode);
                int aaQuality = GamePrefs.GetInt(EnumGamePrefs.OptionsGfxAA);
                if (KFCommonUtilityLib.Gears.PiPCameraSettings.SyncAAQuality == KFCommonUtilityLib.Gears.SyncAAQualityMode.Upscaling && (upscalerMode == 5 || upscalerMode == 2))
                {
                    PostProcessLayer postProcessLayer = layer;
                    PostProcessLayer.Antialiasing antialiasingMode = ((upscalerMode != 5) ? PostProcessLayer.Antialiasing.FSR3 : PostProcessLayer.Antialiasing.DLSS);
                    postProcessLayer.antialiasingMode = antialiasingMode;
                    layer.fsr3.sharpness = sharpness;
                    layer.dlss.sharpness = sharpness;
                    player.renderManager.UpscalingSetQuality(GamePrefs.GetInt(EnumGamePrefs.OptionsGfxFSRPreset));
                }
                else if(KFCommonUtilityLib.Gears.PiPCameraSettings.SyncAAQuality == KFCommonUtilityLib.Gears.SyncAAQualityMode.Antialiasing)
                {
                    player.renderManager.SetAntialiasing(aaQuality, sharpness, layer);
                }
                else
                {
                    layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                }

                var camera = GetComponent<Camera>();
                Rect rect = camera.rect;
                rect.x = ((layer.antialiasingMode == PostProcessLayer.Antialiasing.DLSS || layer.antialiasingMode == PostProcessLayer.Antialiasing.FSR3) ? 1E-07f : 0f);
                camera.rect = rect;
            }
        }
        catch (Exception e)
        {
            Log.Exception(e);
        }
        finally
        {
            player.renderManager.layer = prevLayer;
        }

    }
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

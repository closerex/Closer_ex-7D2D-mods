using System;
using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("KFAttachments/Render Utils/Magnify Scope")]
    [RequireComponent(typeof(Renderer))]
    public class MagnifyScope : MonoBehaviour
    {
#if NotEditor
        [SerializeField]
        private Material postEffectMat;
        [Range(0f, 3f)]
        public float BlurRadius = 1f;
        [Range(8, 128)]
        public int Iteration = 32;
        [Range(1, 10)]
        public float RTDownScaling = 2;
        [Range(0, 1)]
        public float animatedEffectScale = 0f;

        private int itemSlot = -1;
#endif
        private RenderTexture targetTexture;
        private Renderer renderTarget;

        private float DownScalingReci;
        private Vector4 mGoldenRot = new Vector4();

        private static readonly int GoldenRot = Shader.PropertyToID("_GoldenRot");
        private static readonly int Params = Shader.PropertyToID("_Params");
        private static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");
#if NotEditor
        private EntityPlayerLocal player;
#else
        public Camera debugCamera;
#endif
        private void Awake()
        {
            renderTarget = GetComponent<Renderer>();
            if (renderTarget == null)
            {
                Destroy(this);
                return;
            }
#if NotEditor
            //player = GameManager.Instance?.World?.GetPrimaryPlayer();
            var entity = GetComponentInParent<EntityPlayerLocal>();
            if (!entity)
            {
                Destroy(this);
                return;
            }
            player = entity;
            itemSlot = player.inventory.holdingItemIdx;
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            float c = Mathf.Cos(2.39996323f);
            float s = Mathf.Sin(2.39996323f);
            mGoldenRot.Set(c, s, -s, c);
            DownScalingReci = 1 / RTDownScaling;
#else
            if(debugCamera == null)
            {
                Destroy(this);
                return;
            }
            if (debugCamera && !debugCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = debugCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
#endif
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //}
            //bokeh.target = this;
            // Precompute rotations
        }

        private void OnEnable()
        {
#if NotEditor
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            //inventory holding item is not set when creating model, this might be an issue for items with base scope that has this script attached
            //workaround taken from alternative action module, which keeps a reference to the ItemValue being set until its custom data is created
            //afterwards it's set to null so we still need to access holding item when this method is triggered by mods
            if (itemSlot != player.inventory.holdingItemIdx)
            {
                Log.Out($"Scope shader script: Expecting holding item idx {itemSlot} but getting {player.inventory.holdingItemIdx}!");
                return;
            }
            var zoomAction = (ItemActionZoom)((ActionModuleAlternative.InventorySetItemTemp?.ItemClass ?? player.inventory.holdingItem).Actions[1]);
            var zoomActionData = (ItemActionZoom.ItemActionDataZoom)player.inventory.holdingItemData.actionData[1];
            string originalRatio = zoomAction.Properties.GetString("ZoomRatio");
            if (string.IsNullOrEmpty(originalRatio))
            {
                originalRatio = "0";
            }
            float targetScale = StringParsers.ParseFloat(player.inventory.holdingItemItemValue.GetPropertyOverride("ZoomRatio", originalRatio));
            if (targetScale > 0)
            {
                float maxZoom = zoomActionData.MaxZoomIn;
                float refScale = 1 / (Mathf.Tan(Mathf.Deg2Rad * 27.5f) * player.playerCamera.aspect);
                float maxScale = 1 / (Mathf.Tan(Mathf.Deg2Rad * maxZoom / 2) * player.playerCamera.aspect);
                float shaderScale = targetScale / (maxScale / refScale);
                renderTarget.material.SetFloat("_Zoom", shaderScale);
                Log.Out($"Ref scale {refScale} Max scale {maxScale} Shader scale {shaderScale} Target scale {targetScale}");
                Log.Out($"Max zoom {maxZoom} aspect {player.playerCamera.aspect}");
            }

#else
            if(debugCamera == null)
            {
                Destroy(this);
                return;
            }
            if (!debugCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = debugCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
#endif
            reference.AddTarget(this);
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //    bokeh.target = this;
            //}
            //bokeh.enabled = true;
        }

        private void OnDisable()
        {
#if NotEditor
            if (!player)
                return;

            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
#else
            if(debugCamera == null)
            {
                Destroy(this);
                return;
            }
            if (!debugCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = debugCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
#endif
            reference.RemoveTarget(this);
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //    bokeh.target = this;
            //}
            //bokeh.enabled = false;
        }

        internal void RenderImageCallback(RenderTexture source, RenderTexture destination)
        {
            if (targetTexture == null)
            {
                targetTexture = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    filterMode = FilterMode.Bilinear,
                    antiAliasing = source.antiAliasing
                };
            }
            else if (targetTexture.width != source.width || targetTexture.height != source.height)
            {
                targetTexture.Release();
                targetTexture = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    filterMode = FilterMode.Bilinear,
                    antiAliasing = source.antiAliasing
                };
            }
            renderTarget.material.mainTexture = targetTexture;
            Graphics.Blit(source, targetTexture);
            //if (postEffectMat != null)
            //{
            //    RenderTextureDescriptor desc = source.descriptor;
            //    postEffectMat.SetVector(GoldenRot, mGoldenRot);
            //    postEffectMat.SetVector(Params, new Vector4(Iteration, BlurRadius * animatedEffectScale, 1f / desc.width, 1f / desc.height));
            //    desc.width = (int)(desc.width / RTDownScaling);
            //    desc.height = (int)(desc.height / RTDownScaling);
            //    desc.enableRandomWrite = true;
            //    RenderTexture postEffectTexture = RenderTexture.GetTemporary(desc);
            //    Graphics.Blit(source, postEffectTexture, new Vector2(RTDownScaling, RTDownScaling), Vector2.zero);
            //    Graphics.Blit(postEffectTexture, postEffectTexture, postEffectMat);
            //    Graphics.Blit(postEffectTexture, destination, new Vector2(DownScalingReci, DownScalingReci), Vector2.zero);
            //    RenderTexture.ReleaseTemporary(postEffectTexture);
            //}
        }

        internal void BokehBlurCallback(RenderTexture source, RenderTexture destination)
        {
            //if (postEffectMat != null)
            //{
            //    RenderTextureDescriptor desc = source.descriptor;
            //    desc.width = (int)(desc.width / RTDownScaling);
            //    desc.height = (int)(desc.height / RTDownScaling);
            //    desc.enableRandomWrite = true;
            //    postEffectMat.SetVector(GoldenRot, mGoldenRot);
            //    postEffectMat.SetVector(Params, new Vector4(Iteration, BlurRadius, 1f / desc.width, 1f / desc.height));
            //    RenderTexture postEffectTexture = RenderTexture.GetTemporary(desc);
            //    Graphics.Blit(source, postEffectTexture, new Vector2(RTDownScaling, RTDownScaling), Vector2.zero);
            //    Graphics.Blit(postEffectTexture, postEffectTexture, postEffectMat);
            //    Graphics.Blit(postEffectTexture, destination, new Vector2(DownScalingReci, DownScalingReci), Vector2.zero);
            //    RenderTexture.ReleaseTemporary(postEffectTexture);
            //}
        }
    }
}

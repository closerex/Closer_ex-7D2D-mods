using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("KFAttachments/Render Utils/Magnify Scope")]
    [RequireComponent(typeof(Renderer))]
    public class MagnifyScope : MonoBehaviour
    {
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

        private static readonly int GoldenRot = Shader.PropertyToID("_GoldenRot");
        private static readonly int Params = Shader.PropertyToID("_Params");
        private static readonly int BufferRT1 = Shader.PropertyToID("_BufferRT1");
#if NotEditor
        private EntityPlayerLocal player;
        private RenderTexture targetTexture;
        private Renderer renderTarget;

        private float DownScalingReci;
        private Vector4 mGoldenRot = new Vector4();

        private void Awake()
        {
            renderTarget = GetComponent<Renderer>();
            if (renderTarget == null)
            {
                Destroy(this);
                return;
            }
            player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
            {
                Destroy(this);
                return;
            }
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //}
            //bokeh.target = this;
            // Precompute rotations
            float c = Mathf.Cos(2.39996323f);
            float s = Mathf.Sin(2.39996323f);
            mGoldenRot.Set(c, s, -s, c);
            DownScalingReci = 1 / RTDownScaling;
        }

        private void OnEnable()
        {
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            reference.target = this;
            reference.enabled = true;
            //if (!player.playerCamera.TryGetComponent<BokehBlurTargetRef>(out var bokeh))
            //{
            //    bokeh = player.playerCamera.gameObject.AddComponent<BokehBlurTargetRef>();
            //    bokeh.target = this;
            //}
            //bokeh.enabled = true;
        }

        private void OnDisable()
        {
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            reference.target = null;
            reference.enabled = false;
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
#endif
    }
}

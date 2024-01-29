using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("KFAttachments/Render Utils/Magnify Scope")]
    [RequireComponent(typeof(Renderer))]
    public class MagnifyScope : MonoBehaviour
    {
        [SerializeField]
        private string renderTextureName;
#if NotEditor
        private EntityPlayerLocal player;
        private RenderTexture targetTexture;
        private Renderer renderTarget;
        private void Awake()
        {
            renderTarget = GetComponent<Renderer>();
            if(renderTarget == null)
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
        }

        private void OnEnable()
        {
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            reference.target = this;
        }

        private void OnDisable()
        {
            if (!player.playerCamera.TryGetComponent<MagnifyScopeTargetRef>(out var reference))
            {
                reference = player.playerCamera.gameObject.AddComponent<MagnifyScopeTargetRef>();
            }
            reference.target = null;
        }

        internal void RenderImageCallback(RenderTexture source, RenderTexture destination)
        {
            if (targetTexture == null)
            {
                targetTexture = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }
            else if (targetTexture.width != source.width || targetTexture.height != source.height)
            {
                targetTexture.Release();
                targetTexture = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }
            renderTarget.material.SetTexture(renderTextureName, targetTexture);
            Graphics.Blit(source, targetTexture);
        }
#endif
    }
}

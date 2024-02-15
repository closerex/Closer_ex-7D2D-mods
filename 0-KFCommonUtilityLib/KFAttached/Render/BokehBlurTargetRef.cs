using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("")]
    public class BokehBlurTargetRef : MonoBehaviour
    {
#if NotEditor
        internal MagnifyScope target;

        [ImageEffectUsesCommandBuffer]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (target != null)
            {
                target.BokehBlurCallback(source, destination);
            }
        }
#endif
    }
}

using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("")]
    internal class MagnifyScopeTargetRef : MonoBehaviour
    {
#if NotEditor
        internal MagnifyScope target;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (target)
            {
                target.RenderImageCallback(source, destination);
            }
        }
#endif
    }
}

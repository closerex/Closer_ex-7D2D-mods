using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.Render
{
    [AddComponentMenu("")]
    public class BokehBlurTargetRef : MonoBehaviour
    {
#if NotEditor
        internal MagnifyScope target;
#endif
    }
}

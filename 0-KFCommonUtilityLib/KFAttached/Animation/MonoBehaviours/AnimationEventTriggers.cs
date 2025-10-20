using UnityEngine;

namespace KFCommonUtilityLib
{
    public class AnimationEventTriggers : MonoBehaviour
    {
#if NotEditor
        private EntityPlayerLocal player;
#endif
        public void FireEvent(string tags)
        {
#if NotEditor
            if (player == null)
            {
                player = this.GetLocalPlayerInParent();
                if (player == null)
                {
                    return;
                }
            }

            AnimationStateTriggers.FireEvent(player, FastTags<TagGroup.Global>.Parse(tags), CustomEnums.onAnimationEventTrigger);
#endif
        }
    }
}

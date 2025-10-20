using UnityEngine;

namespace KFCommonUtilityLib
{
    public class AnimationStateTriggers : StateMachineBehaviour
    {
        public bool fireEnterTrigger = false;
        public bool fireUpdateTrigger = false;
        public bool fireExitTrigger = false;
        public string tagName;
#if NotEditor
        private EntityPlayerLocal player;
        private FastTags<TagGroup.Global> tag;
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (tag.IsEmpty)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return;
                }
                tag = FastTags<TagGroup.Global>.Parse(tagName);
            }

            if (!player)
            {
                player = animator.GetLocalPlayerInParent();
            }

            if (fireEnterTrigger)
            {
                FireEvent(player, tag, CustomEnums.onAnimatorStateEntered);
            }
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (fireUpdateTrigger)
            {
                FireEvent(player, tag, CustomEnums.onAnimatorStateUpdate);
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (fireExitTrigger)
            {
                FireEvent(player, tag, CustomEnums.onAnimatorStateExit);
            }
        }

        public static void FireEvent(EntityPlayerLocal player, FastTags<TagGroup.Global> tag, MinEventTypes eventType)
        {
            if (!player || tag.IsEmpty)
            {
                return;
            }
            var prevTags = player.MinEventContext.Tags;
            player.MinEventContext.Tags = tag;
            player.FireEvent(eventType);
            player.MinEventContext.Tags = prevTags;
        }
#endif
    }
}

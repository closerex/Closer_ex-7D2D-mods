using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.StaticManagers
{
    public unsafe struct FakeAttackActions
    {
        private unsafe fixed bool actions[ItemClass.cMaxActionNames];

        public bool this[int i] => actions[i];

        public unsafe static bool Create(ItemClass item, out FakeAttackActions actions)
        {
            actions = default;
            if(item == null)
            {
                return false;
            }
            bool created = false;
            for (int i = 0; i < ItemClass.cMaxActionNames; i++)
            {
                if (item.Actions[i] != null && item.Actions[i].Properties.Values.TryGetString("ForceFakeAttack", out string str) && bool.TryParse(str, out actions.actions[i]))
                {
                    created = true;
                }
            }
            return created;
        }
    }

    public static class FakeAttackManager
    {
        private readonly static Dictionary<int, FakeAttackActions> dict_fake_attack = new Dictionary<int, FakeAttackActions>();

        public static void PreloadCleanup()
        {
            dict_fake_attack.Clear();
        }

        public static void ParseFakeAttackItem(ItemClass item)
        {
            if (FakeAttackActions.Create(item, out FakeAttackActions actions))
            {
                dict_fake_attack.Add(item.Id, actions);
            }
        }

        public static bool ShouldFakeAttack(int itemId, int actionIndex)
        {
            return dict_fake_attack.TryGetValue(itemId, out FakeAttackActions actions) && actions[actionIndex];
        }
    }
}

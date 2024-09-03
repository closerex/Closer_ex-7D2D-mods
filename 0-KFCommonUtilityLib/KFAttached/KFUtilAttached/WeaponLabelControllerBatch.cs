using UnityEngine;

namespace KFCommonUtilityLib.KFAttached.KFUtilAttached
{
    public class WeaponLabelControllerBatch : WeaponLabelControllerBase
    {
        [SerializeField]
        private WeaponLabelControllerBase[] controllers;

        public override bool setLabelColor(int index, Color color)
        {
            bool flag = false;
            foreach (var controller in controllers)
            {
                if (controller && controller.isActiveAndEnabled)
                {
                    flag |= controller.setLabelColor(index, color);
                }
            }
            return flag;
        }

        public override bool setLabelText(int index, string data)
        {
            bool flag = false;
            foreach (var controller in controllers)
            {
                if (controller && controller.isActiveAndEnabled)
                {
                    flag |= controller.setLabelText(index, data);
                }
            }
            return flag;
        }
    }
}

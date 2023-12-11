using GUI_2;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    public static class MultiActionManager
    {

        public static void UpdateExecutingActionIndex(int index, ItemInventoryData invData, PlayerActionsLocal playerActions)
        {
            if(playerActions == null || !(invData.holdingEntity is EntityPlayerLocal player))
            {
                return;
            }

            player.MinEventContext.ItemActionData = invData.actionData[index];
            player.MinEventContext.Tags = MultiActionUtils.GetItemTagsWithActionIndex(invData.actionData[index]);
        }

        public static void SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
        {
            _xuiRadialWindow.ResetRadialEntries();
            string[] magazineItemNames = _epl.inventory.GetHoldingGun().MagazineItemNames;
            int preSelectedIndex = -1;
            for (int i = 0; i < magazineItemNames.Length; i++)
            {
                ItemClass itemClass = ItemClass.GetItemClass(magazineItemNames[i], false);
                if (itemClass != null && (!_epl.isHeadUnderwater || itemClass.UsableUnderwater))
                {
                    int itemCount = _xuiRadialWindow.xui.PlayerInventory.GetItemCount(itemClass.Id);
                    bool flag = (int)_epl.inventory.holdingItemItemValue.SelectedAmmoTypeIndex == i;
                    _xuiRadialWindow.CreateRadialEntry(i, itemClass.GetIconName(), (itemCount > 0) ? "ItemIconAtlas" : "ItemIconAtlasGreyscale", itemCount.ToString(), itemClass.GetLocalizedItemName(), flag);
                    if (flag)
                    {
                        preSelectedIndex = i;
                    }
                }
            }
            _xuiRadialWindow.SetCommonData(UIUtils.ButtonIcon.FaceButtonEast, new XUiC_Radial.CommandHandlerDelegate(handleRadialCommand), new RadialContextMultiAction(_epl.inventory.holdingItemData), preSelectedIndex, false, new XUiC_Radial.RadialStillValidDelegate(radialValidTest));
        }

        private static bool radialValidTest(XUiC_Radial _sender, XUiC_Radial.RadialContextAbs _context)
        {
            RadialContextMultiAction radialContextItem = _context as RadialContextMultiAction;
            if (radialContextItem == null)
            {
                return false;
            }
            EntityPlayerLocal entityPlayer = _sender.xui.playerUI.entityPlayer;
            return radialContextItem.invData == entityPlayer.inventory.holdingItemData;
        }

        private static void handleRadialCommand(XUiC_Radial _sender, int _commandIndex, XUiC_Radial.RadialContextAbs _context)
        {
            RadialContextMultiAction radialContextItem = _context as RadialContextMultiAction;
            if (radialContextItem == null)
            {
                return;
            }
            EntityPlayerLocal entityPlayer = _sender.xui.playerUI.entityPlayer;
            if (radialContextItem.invData == entityPlayer.inventory.holdingItemData)
            {
                radialContextItem.RangedItemAction.SwapSelectedAmmo(entityPlayer, _commandIndex);
            }
        }

        public class RadialContextMultiAction : XUiC_Radial.RadialContextAbs
        {
            public ItemInventoryData invData;

            public RadialContextMultiAction(ItemInventoryData invData)
            {
                this.invData = invData;
            }
        }
    }
}

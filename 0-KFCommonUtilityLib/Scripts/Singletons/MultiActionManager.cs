using GUI_2;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    //concept: maintain an entityID-AltActionIndice mapping on both server and client
    //and get the correct action before calling ItemAction.*
    //always set MinEventParams.itemActionData
    //set meta and ammoindex on switching mode, keep current mode in metadata
    //should take care of accuracy updating
    //should support shared meta
    //alt actions should be considered primary, redirect index == 0 to custom method
    //redirect ItemClass.Actions[0] to custom method
    //patch GameManager.updateSendClientPlayerPositionToServer to sync data, so that mode change always happens after holding item change

    public struct MultiActionIndice
    {
        public const int MAX_ACTION_COUNT = 3;
        public unsafe fixed int indice[MAX_ACTION_COUNT];
        public unsafe fixed int metaIndice[MAX_ACTION_COUNT];

        //this should only be called in createModifierData
        public unsafe MultiActionIndice(ItemAction[] actions)
        {
            indice[0] = 0;
            metaIndice[0] = 0;
            int last = 1;
            for (int i = 3; i < actions.Length && last < MAX_ACTION_COUNT; i++)
            {
                if (actions[i] != null)
                {
                    indice[last] = i;
                    if (actions[i].Properties.Values.TryGetString("ShareMetaWith", out string str) && int.TryParse(str, out int shareWith))
                    {
                        metaIndice[last] = shareWith;
                    }
                    else
                    {
                        metaIndice[last] = i;
                    }
                    last++;
                }
            }
        }
    }

    //MultiActionMapping instance should be changed on ItemAction.StartHolding, so we only need to send curIndex.
    public class MultiActionMapping
    {
        public readonly MultiActionIndice mapping;
        private int curIndex;
        public ItemValue itemValue;

        /// <summary>
        /// when set CurIndex from local input, also set manager to dirty to update the index on other clients
        /// </summary>
        public int CurIndex
        {
            get => curIndex;
            set
            {
                curIndex = value;
                itemValue.SetMetadata("MultiActionIndex", curIndex, TypedMetadataValue.TypeTag.Integer);
                //also set meta and ammo index
            }
        }

        public MultiActionMapping(MultiActionIndice mapping, ItemValue itemValue)
        {
            this.mapping = mapping;
            this.itemValue = itemValue;
            if (itemValue.HasMetadata("MultiActionIndex", TypedMetadataValue.TypeTag.Integer))
            {
                curIndex = (int)itemValue.GetMetadata("MultiActionIndex");
            }
            else
            {
                itemValue.SetMetadata("MultiActionIndex", 0, TypedMetadataValue.TypeTag.Integer);
                curIndex = 0;
            }
        }
    }

    public static class MultiActionManager
    {
        //clear on game load
        private static readonly Dictionary<int, MultiActionMapping> dict_mappings = new Dictionary<int, MultiActionMapping>();

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

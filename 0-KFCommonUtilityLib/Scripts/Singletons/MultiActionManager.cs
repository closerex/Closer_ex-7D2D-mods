﻿using GUI_2;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.Singletons
{
    //concept: maintain an entityID-AltActionIndice mapping on both server and client
    //and get the correct action before calling ItemAction.*
    //always set MinEventParams.itemActionData
    //done: set meta and ammoindex on switching mode, keep current mode in metadata
    //should take care of accuracy updating
    //partially done: should support shared meta
    //alt actions should be considered primary, redirect index == 0 to custom method
    //redirect ItemClass.Actions[0] to custom method
    //patch GameManager.updateSendClientPlayerPositionToServer to sync data, so that mode change always happens after holding item change

    public struct MultiActionIndice
    {
        public const int MAX_ACTION_COUNT = 3;
        public unsafe fixed int indices[MAX_ACTION_COUNT];
        public unsafe fixed int metaIndice[MAX_ACTION_COUNT];
        public readonly int modeCount;

        //this should only be called in createModifierData
        public unsafe MultiActionIndice(ItemAction[] actions)
        {
            indices[0] = 0;
            metaIndice[0] = 0;
            int last = 1;
            for (int i = 3; i < actions.Length && last < MAX_ACTION_COUNT; i++)
            {
                if (actions[i] != null)
                {
                    indices[last] = i;
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
            modeCount = last;
            for (; last < MAX_ACTION_COUNT; last++)
            {
                indices[last] = -1;
                metaIndice[last] = -1;
            }
        }
    }

    //MultiActionMapping instance should be changed on ItemAction.StartHolding, so we only need to send curIndex.
    public class MultiActionMapping
    {
        public const string STR_MULTI_ACTION_INDEX = "MultiActionIndex";
        public readonly MultiActionIndice indices;
        private int curIndex;
        private int lastDisplayMode = -1;
        public EntityAlive entity;
        public string toggleSound;

        /// <summary>
        /// when set CurIndex from local input, also set manager to dirty to update the index on other clients
        /// </summary>
        public int CurMode
        {
            get => curIndex;
            set
            {
                unsafe
                {
                    if (curIndex == value)
                        return;
                    //mostly for CurIndex++, cycle through available indices
                    if (value >= MultiActionIndice.MAX_ACTION_COUNT || indices.indices[value] == -1)
                        value = 0;
                    //save previous meta and ammo index to metadata
                    int curMetaIndex = CurMetaIndex;
                    ItemValue itemValue = entity.inventory.holdingItemItemValue;
                    itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[curMetaIndex], itemValue.Meta, TypedMetadataValue.TypeTag.Integer);
                    itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[curMetaIndex], (int)itemValue.SelectedAmmoTypeIndex, TypedMetadataValue.TypeTag.Integer);

                    //load current meta and ammo index from metadata
                    curIndex = value;
                    curMetaIndex = CurMetaIndex;
                    itemValue.SetMetadata(STR_MULTI_ACTION_INDEX, value, TypedMetadataValue.TypeTag.Integer);
                    object res = itemValue.GetMetadata(MultiActionUtils.ActionMetaNames[curMetaIndex]);
                    if (res is false || res is null)
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[curMetaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                        itemValue.Meta = 0;
                    }
                    else
                    {
                        itemValue.Meta = (int)res;
                    }
                    res = itemValue.GetMetadata(MultiActionUtils.ActionSelectedAmmoNames[curMetaIndex]);
                    if (res is false || res is null)
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[curMetaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                        itemValue.SelectedAmmoTypeIndex = 0;
                    }
                    else
                    {
                        itemValue.SelectedAmmoTypeIndex = (byte)(int)res;
                    }
                }
            }
        }

        //for ItemClass.Actions access
        public int CurActionIndex
        {
            get
            {
                unsafe
                {
                    return indices.indices[curIndex];
                }
            }
        }

        //for meta saving on mode switch only?
        public int CurMetaIndex
        {
            get
            {
                unsafe
                {
                    return indices.metaIndice[curIndex];
                }
            }
        }

        public int ModeCount => indices.modeCount;

        //while mapping object is changed on StartHolding, it's initialized on createModifierData
        //we set the curIndex field instead of the property, according to following situations:
        //1. it's a newly created ItemValue, meta and ammo index belongs to action0, no saving is needed;
        //2. it's an existing ItemValue, meta and ammo index is set to its action index, still saving is unnecessary.
        public MultiActionMapping(MultiActionIndice indices, EntityAlive entity, string toggleSound)
        {
            this.indices = indices;
            this.entity = entity;
            ItemValue itemValue = entity.inventory.holdingItemItemValue;
            object res = itemValue.GetMetadata(STR_MULTI_ACTION_INDEX);
            if (res is false || res is null)
            {
                itemValue.SetMetadata(STR_MULTI_ACTION_INDEX, 0, TypedMetadataValue.TypeTag.Integer);
                curIndex = 0;
            }
            else
            {
                curIndex = (int)res;
            }

            unsafe
            {
                for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
                {
                    int metaIndex = indices.metaIndice[i];
                    if (metaIndex < 0)
                        break;
                    if (!itemValue.HasMetadata(MultiActionUtils.ActionMetaNames[metaIndex]))
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[metaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                    }

                    if (!itemValue.HasMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex]))
                    {
                        itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                    }
                }
            }
            this.toggleSound = toggleSound;
        }

        public int GetModeForAction(int actionIndex)
        {
            int mode = -1;
            for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
            {
                unsafe
                {
                    if (indices.indices[i] == actionIndex)
                    {
                        mode = i;
                        break;
                    }
                }
            }
            return mode;
        }

        public unsafe int SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
        {
            _xuiRadialWindow.ResetRadialEntries();
            int preSelectedIndex = -1;
            string[] magazineItemNames = ((ItemActionAttack)_epl.inventory.holdingItem.Actions[CurActionIndex]).MagazineItemNames;
            for (int i = 0; i < magazineItemNames.Length; i++)
            {
                ItemClass ammoClass = ItemClass.GetItemClass(magazineItemNames[i], false);
                if (ammoClass != null && (!_epl.isHeadUnderwater || ammoClass.UsableUnderwater))
                {
                    int ammoCount = _xuiRadialWindow.xui.PlayerInventory.GetItemCount(ammoClass.Id);
                    bool isCurrentUsing = _epl.inventory.holdingItemItemValue.SelectedAmmoTypeIndex == i;
                    _xuiRadialWindow.CreateRadialEntry(i, ammoClass.GetIconName(), (ammoCount > 0) ? "ItemIconAtlas" : "ItemIconAtlasGreyscale", ammoCount.ToString(), ammoClass.GetLocalizedItemName(), isCurrentUsing);
                    if (isCurrentUsing)
                    {
                        preSelectedIndex = i;
                    }
                }
            }

            return preSelectedIndex;
        }

        public bool CheckDisplayMode()
        {
            if (lastDisplayMode == CurMode)
            {
                return false;
            }
            else
            {
                lastDisplayMode = CurMode;
                return true;
            }
        }
    }

    public static class MultiActionManager
    {
        //clear on game load
        private static readonly Dictionary<int, MultiActionMapping> dict_mappings = new Dictionary<int, MultiActionMapping>();
        //should set to true when:
        //mode switch input received;
        //start holding new multi action weapon.?
        //if true, send local curIndex to other clients in updateSendClientPlayerPositionToServer.
        public static bool LocalModeChanged { get; set; }

        public static void ToggleLocalActionIndex(EntityPlayerLocal player)
        {
            if (player == null || !dict_mappings.TryGetValue(player.entityId, out MultiActionMapping mapping))
                return;

            if (mapping.ModeCount <= 1 || player.inventory.IsHoldingItemActionRunning())
                return;
            mapping.CurMode++;
            player.PlayOneShot(mapping.toggleSound);
            LocalModeChanged = true;
        }

        public static void SetMappingForEntity(int entityID, MultiActionMapping mapping)
        {
            dict_mappings[entityID] = mapping;
            //Log.Out($"current item index mapping: {((mapping == null || mapping.itemValue == null) ? "null" : mapping.itemValue.ItemClass.Name)}");
        }

        public static bool SetModeForEntity(int entityID, int mode)
        {
            if (dict_mappings.TryGetValue(entityID, out MultiActionMapping mapping) && mapping != null)
            {
                int prevMode = mapping.CurMode;
                mapping.CurMode = mode;
                return prevMode != mapping.CurMode;
            }
            return false;
        }

        public static int GetModeForEntity(int entityID)
        {
            if (!dict_mappings.TryGetValue(entityID, out MultiActionMapping mapping) || mapping == null)
                return 0;
            return mapping.CurMode;
        }

        public static int GetActionIndexForEntity(EntityAlive entity)
        {
            if(entity == null || !dict_mappings.TryGetValue(entity.entityId, out var mapping) || mapping == null)
                return 0;
            return mapping.CurActionIndex;
        }

        public static int GetActionIndexForEntityID(int entityID)
        {
            if (!dict_mappings.TryGetValue(entityID, out var mapping) || mapping == null)
                return 0;
            return mapping.CurActionIndex;
        }

        public static int GetMetaIndexForEntity(int entityID)
        {
            if (!dict_mappings.TryGetValue(entityID, out var mapping) || mapping == null)
                return 0;
            return mapping.CurMetaIndex;
        }

        public static int GetMetaIndexForActionIndex(int entityID, int actionIndex)
        {
            if (!dict_mappings.TryGetValue(entityID, out var mapping) || mapping == null)
            {
                return actionIndex;
            }

            int mode = mapping.GetModeForAction(actionIndex);
            if (mode > 0)
            {
                unsafe
                {
                    return mapping.indices.metaIndice[mode];
                }
            }
            return actionIndex;
        }

        public static MultiActionMapping GetMappingForEntity(int entityID)
        {
            dict_mappings.TryGetValue(entityID, out var mapping);
            return mapping;
        }

        private static float inputCD = 0;
        internal static void UpdateLocalInput(EntityPlayerLocal player, PlayerActionsLocal localActions, bool isUIOpen, float _dt)
        {
            if (inputCD > 0)
            {
                inputCD = Math.Max(0, inputCD - _dt);
            }
            if (isUIOpen || inputCD > 0 || player.emodel.IsRagdollActive || player.IsDead() || player.AttachedToEntity != null)
            {
                return;
            }

            if (PlayerActionToggleMode.Instance.Enabled && PlayerActionToggleMode.Instance.Toggle.WasPressed)
            {
                var mapping = GetMappingForEntity(player.entityId);

                if (mapping == null)
                {
                    return;
                }

                if (player.inventory.IsHoldingItemActionRunning())
                {
                    return;
                }

                if (localActions.Reload.WasPressed || localActions.PermanentActions.Reload.WasPressed)
                {
                    inputCD = 0.1f;
                    return;
                }

                player.inventory.Execute(mapping.CurActionIndex, true, localActions);
                localActions.Primary.ClearInputState();
                ToggleLocalActionIndex(player);
            }
        }
    }
}
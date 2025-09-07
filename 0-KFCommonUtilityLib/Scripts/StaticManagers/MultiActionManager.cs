using KFCommonUtilityLib.Scripts.ConsoleCmd;
using KFCommonUtilityLib.Scripts.Utilities;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib
{
    //concept: maintain an entityID-AltActionIndice mapping on both server and client
    //and get the correct action before calling ItemAction.*
    //always set MinEventParams.itemActionData
    //done: set meta and ammoindex on switching mode, keep current mode in metadata
    //should take care of accuracy updating
    //partially done: should support shared meta
    //alt actions should be considered primary, redirect index == 0 to custom method
    //redirect ItemClass.Actions[0] to custom method
    //however, player input handling is redirected to action0 so that alternative module can dispatch it to correct action.
    //patch GameManager.updateSendClientPlayerPositionToServer to sync data, so that mode change always happens after holding item change

    public struct MultiActionIndice
    {
        public const int MAX_ACTION_COUNT = 3;
        public unsafe fixed sbyte indices[MAX_ACTION_COUNT];
        public unsafe fixed sbyte metaIndice[MAX_ACTION_COUNT];
        public readonly byte modeCount;

        public unsafe MultiActionIndice(ItemClass item)
        {
            ItemAction[] actions = item.Actions;
            indices[0] = 0;
            metaIndice[0] = 0;
            byte last = 1;
            for (sbyte i = 3; i < actions.Length && last < MAX_ACTION_COUNT; i++)
            {
                if (actions[i] != null)
                {
                    indices[last] = i;
                    if (actions[i].Properties.Values.TryGetValue("ShareMetaWith", out string str) && sbyte.TryParse(str, out sbyte shareWith))
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

        public unsafe int GetActionIndexForMode(int mode)
        {
            return indices[mode];
        }

        public unsafe int GetMetaIndexForMode(int mode)
        {
            return metaIndice[mode];
        }

        public unsafe int GetMetaIndexForActionIndex(int actionIndex)
        {
            return metaIndice[GetModeForAction(actionIndex)];
        }

        public int GetModeForAction(int actionIndex)
        {
            int mode = -1;
            for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
            {
                unsafe
                {
                    if (indices[i] == actionIndex)
                    {
                        mode = i;
                        break;
                    }
                }
            }
            return mode;
        }
    }

    //MultiActionMapping instance should be changed on ItemAction.StartHolding, so we only need to send curIndex.
    public class MultiActionMapping
    {
        public const string STR_MULTI_ACTION_INDEX = "MultiActionIndex";
        public readonly MultiActionIndice indices;
        private int slotIndex;
        private int curIndex;
        private int lastDisplayMode = -1;
        private readonly bool[] unlocked;
        private ActionModuleAlternative.AlternativeData altData;
        public EntityAlive entity;
        public string toggleSound;

        public ItemValue ItemValue
        {
            get
            {
                var res = entity.inventory.GetItem(slotIndex).itemValue;
                if (res.IsEmpty())
                {
                    return null;
                }
                return res;
            }
        }

        public int SlotIndex => slotIndex;

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
                    if (value < 0)
                        value = 0;
                    else
                    {
                        while (value < MultiActionIndice.MAX_ACTION_COUNT)
                        {
                            if (unlocked[value])
                                break;
                            value++;
                        }
                        //mostly for CurIndex++, cycle through available indices
                        if (value >= MultiActionIndice.MAX_ACTION_COUNT || indices.indices[value] == -1)
                            value = 0;
                    }
                    if (curIndex == value)
                        return;

                    SaveMeta();

                    //load current meta and ammo index from metadata
                    curIndex = value;
                    ReadMeta();
                    entity.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, CurActionIndex, false);
                    //altData.OverrideMuzzleTransform(curIndex);
                }
            }
        }

        //for ItemClass.Actions access
        public int CurActionIndex => indices.GetActionIndexForMode(curIndex);

        //for meta saving on mode switch only?
        public int CurMetaIndex => indices.GetMetaIndexForMode(curIndex);

        public int ModeCount => indices.modeCount;

        //mapping object is created on StartHolding
        //we set the curIndex field instead of the property, according to following situations:
        //1. it's a newly created ItemValue, meta and ammo index belongs to action0, no saving is needed;
        //2. it's an existing ItemValue, meta and ammo index is set to its action index, still saving is unnecessary.
        internal MultiActionMapping(ActionModuleAlternative.AlternativeData altData, MultiActionIndice indices, EntityAlive entity, ItemValue itemValueTemp, string toggleSound, int slotIndex, bool[] unlocked)
        {
            this.altData = altData;
            this.indices = indices;
            this.entity = entity;
            this.slotIndex = slotIndex;
            this.unlocked = unlocked;
            object res = itemValueTemp.GetMetadata(STR_MULTI_ACTION_INDEX);
            if (res is false || res is null)
            {
                itemValueTemp.SetMetadata(STR_MULTI_ACTION_INDEX, 0, TypedMetadataValue.TypeTag.Integer);
                curIndex = 0;
            }
            else
            {
                curIndex = (int)res;
                ReadMeta();
            }

            unsafe
            {
                for (int i = 0; i < MultiActionIndice.MAX_ACTION_COUNT; i++)
                {
                    int metaIndex = indices.metaIndice[i];
                    if (metaIndex < 0)
                        break;
                    if (!itemValueTemp.HasMetadata(MultiActionUtils.ActionMetaNames[metaIndex]))
                    {
                        itemValueTemp.SetMetadata(MultiActionUtils.ActionMetaNames[metaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                    }
#if DEBUG
                    else
                    {
                        Log.Out($"{MultiActionUtils.ActionMetaNames[metaIndex]}: {itemValueTemp.GetMetadata(MultiActionUtils.ActionMetaNames[metaIndex]).ToString()}");
                    }
#endif
                    if (!itemValueTemp.HasMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex]))
                    {
                        itemValueTemp.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex], 0, TypedMetadataValue.TypeTag.Integer);
                    }
#if DEBUG
                    else
                    {
                        Log.Out($"{MultiActionUtils.ActionSelectedAmmoNames[metaIndex]}: {itemValueTemp.GetMetadata(MultiActionUtils.ActionSelectedAmmoNames[metaIndex]).ToString()}");
                    }
#endif
                }
            }
            this.toggleSound = toggleSound;
            entity.emodel?.avatarController?.UpdateInt(MultiActionUtils.ExecutingActionIndexHash, CurActionIndex, false);
#if DEBUG
            Log.Out($"MultiAction mode {curIndex}, meta {itemValueTemp.Meta}, ammo index {itemValueTemp.SelectedAmmoTypeIndex}\n {StackTraceUtility.ExtractStackTrace()}");
#endif
        }

        public void SaveMeta(ItemValue _itemValue = null)
        {
            //save previous meta and ammo index to metadata
            int curMetaIndex = CurMetaIndex;
            ItemValue itemValue = _itemValue ?? ItemValue;
            if (itemValue == null)
                return;
            ItemAction[] actions = itemValue.ItemClass.Actions;
            if (CurActionIndex < 0 || CurActionIndex >= actions.Length)
                return;
            ItemActionAttack itemActionAttack = actions[CurActionIndex] as ItemActionAttack;
            if (itemActionAttack == null)
                return;
            if (ConsoleCmdReloadLog.LogInfo)
            {
                Log.Out($"Saving meta for item {itemValue.ItemClass.Name}");
            }
            itemValue.SetMetadata(MultiActionUtils.ActionMetaNames[curMetaIndex], itemValue.Meta, TypedMetadataValue.TypeTag.Integer);
            itemValue.SetMetadata(MultiActionUtils.ActionSelectedAmmoNames[curMetaIndex], (int)itemValue.SelectedAmmoTypeIndex, TypedMetadataValue.TypeTag.Integer);
            if (itemValue.SelectedAmmoTypeIndex > itemActionAttack.MagazineItemNames.Length)
            {
                Log.Error($"SAVING META ERROR: AMMO INDEX LARGER THAN AMMO ITEM COUNT!\n{StackTraceUtility.ExtractStackTrace()}");
            }
            if (ConsoleCmdReloadLog.LogInfo)
            {
                ConsoleCmdMultiActionItemValueDebug.LogMeta(itemValue);
                Log.Out($"Save Meta stacktrace:\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }

        public void ReadMeta(ItemValue _itemValue = null)
        {
            int curMetaIndex = CurMetaIndex;
            ItemValue itemValue = _itemValue ?? ItemValue;
            if (itemValue == null)
                return;
            itemValue.SetMetadata(STR_MULTI_ACTION_INDEX, curIndex, TypedMetadataValue.TypeTag.Integer);
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
            if (ConsoleCmdReloadLog.LogInfo)
            {
                ConsoleCmdMultiActionItemValueDebug.LogMeta(itemValue);
                Log.Out($"Read Meta stacktrace:\n{StackTraceUtility.ExtractStackTrace()}");
            }
        }

        public int SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
        {
            _xuiRadialWindow.ResetRadialEntries();
            int preSelectedIndex = -1;
            string[] magazineItemNames = ((ItemActionAttack)_epl.inventory.holdingItem.Actions[CurActionIndex]).MagazineItemNames;
            bool[] disableStates = CommonUtilityPatch.GetUnusableItemEntries(magazineItemNames, _epl, CurActionIndex);
            for (int i = 0; i < magazineItemNames.Length; i++)
            {
                ItemClass ammoClass = ItemClass.GetItemClass(magazineItemNames[i], false);
                if (ammoClass != null && (!_epl.isHeadUnderwater || ammoClass.UsableUnderwater) && !disableStates[i])
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

        public bool IsActionUnlocked(int actionIndex)
        {
            if (actionIndex >= MultiActionIndice.MAX_ACTION_COUNT || actionIndex < 0)
                return false;
            return unlocked[actionIndex];
        }
    }

    public static class MultiActionManager
    {
        //clear on game load
        private static readonly Dictionary<int, MultiActionMapping> dict_mappings = new Dictionary<int, MultiActionMapping>();
        private static readonly Dictionary<int, MultiActionIndice> dict_indice = new Dictionary<int, MultiActionIndice>();
        private static readonly Dictionary<int, FastTags<TagGroup.Global>[]> dict_item_action_exclude_tags = new Dictionary<int, FastTags<TagGroup.Global>[]>();
        private static readonly Dictionary<int, int[][]> dict_item_action_exclude_mod_property = new Dictionary<int, int[][]>();
        private static readonly Dictionary<int, int[][]> dict_item_action_exclude_mod_passive = new Dictionary<int, int[][]>();
        private static readonly Dictionary<int, int[][]> dict_item_action_exclude_mod_trigger = new Dictionary<int, int[][]>();

        //should set to true when:
        //mode switch input received;
        //start holding new multi action weapon.?
        //if true, send local curIndex to other clients in updateSendClientPlayerPositionToServer.
        public static bool LocalModeChanged { get; set; }

        public static void PostloadCleanup(ref ModEvents.SGameStartDoneData _)
        {
            dict_mappings.Clear();
            dict_indice.Clear();
        }

        public static void PreloadCleanup()
        {
            dict_item_action_exclude_tags.Clear();
            dict_item_action_exclude_mod_property.Clear();
            dict_item_action_exclude_mod_passive.Clear();
            dict_item_action_exclude_mod_trigger.Clear();
        }

        public static void ParseItemActionExcludeTagsAndModifiers(ItemClass item)
        {
            if (item == null)
                return;
            FastTags<TagGroup.Global>[] tags = null;
            int[][] properties = null, passives = null, triggers = null;
            for (int i = 0; i < item.Actions.Length; i++)
            {
                if (item.Actions[i] != null)
                {
                    if (item.Actions[i].Properties.Values.TryGetValue("ExcludeTags", out string str))
                    {
                        if (tags == null)
                        {
                            tags = new FastTags<TagGroup.Global>[ItemClass.cMaxActionNames];
                            dict_item_action_exclude_tags.Add(item.Id, tags);
                        }
                        tags[i] = FastTags<TagGroup.Global>.Parse(str);
                    }
                    if (item.Actions[i].Properties.Values.TryGetValue("ExcludeMods", out str))
                    {
                        if (properties == null)
                        {
                            properties = new int[ItemClass.cMaxActionNames][];
                            dict_item_action_exclude_mod_property.Add(item.Id, properties);
                        }
                        properties[i] = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .Select(s => ItemClass.GetItemClass(s, false))
                                     .Where(_item => _item != null)
                                     .Select(_item => _item.Id)
                                     .ToArray();
                        //Log.Out($"EXCLUDE PROPERTIES FROM ITEM {item.Name} ITEMID {item.Id} ACTION {i} : {string.Join(" ", properties[i])}");
                    }
                    if (item.Actions[i].Properties.Values.TryGetValue("ExcludePassives", out str))
                    {
                        if (passives == null)
                        {
                            passives = new int[ItemClass.cMaxActionNames][];
                            dict_item_action_exclude_mod_passive.Add(item.Id, passives);
                        }
                        passives[i] = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .Select(s => ItemClass.GetItemClass(s, false))
                                     .Where(_item => _item != null)
                                     .Select(_item => _item.Id)
                                     .ToArray();
                        //Log.Out($"EXCLUDE PASSIVES FROM ITEM {item.Name} ITEMID {item.Id} ACTION {i} : {string.Join(" ", passives[i])}");
                    }
                    if (item.Actions[i].Properties.Values.TryGetValue("ExcludeTriggers", out str))
                    {
                        if (triggers == null)
                        {
                            triggers = new int[ItemClass.cMaxActionNames][];
                            dict_item_action_exclude_mod_trigger.Add(item.Id, triggers);
                        }
                        triggers[i] = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Where(s => !string.IsNullOrEmpty(s))
                                     .Select(s => ItemClass.GetItemClass(s, false))
                                     .Where(_item => _item != null)
                                     .Select(_item => _item.Id)
                                     .ToArray();
                        //Log.Out($"EXCLUDE TRIGGERS FROM ITEM {item.Name} ITEMID {item.Id} ACTION {i} : {string.Join(" ", triggers[i])}");
                    }
                }
            }
        }

        public static void ModifyItemTags(ItemValue itemValue, ItemActionData actionData, ref FastTags<TagGroup.Global> tags)
        {
            if (itemValue == null || actionData == null || !dict_item_action_exclude_tags.TryGetValue(itemValue.type, out var arr_tags))
            {
                return;
            }

            tags = tags.Remove(arr_tags[actionData.indexInEntityOfAction]);
        }

        public static bool ShouldExcludeProperty(int itemId, int modId, int actionIndex)
        {
            return dict_item_action_exclude_mod_property.TryGetValue(itemId, out var arr_exclude) && arr_exclude[actionIndex] != null && Array.IndexOf(arr_exclude[actionIndex], modId) >= 0;
        }

        public static bool ShouldExcludePassive(int itemId, int modId, int actionIndex)
        {
            return dict_item_action_exclude_mod_passive.TryGetValue(itemId, out var arr_exclude) && arr_exclude[actionIndex] != null && Array.IndexOf(arr_exclude[actionIndex], modId) >= 0;
        }

        public static bool ShouldExcludeTrigger(int itemId, int modId, int actionIndex)
        {
            return dict_item_action_exclude_mod_trigger.TryGetValue(itemId, out var arr_exclude) && arr_exclude[actionIndex] != null && Array.IndexOf(arr_exclude[actionIndex], modId) >= 0;
        }

        public static void UpdateLocalMetaSave(int playerID)
        {
            if (dict_mappings.TryGetValue(playerID, out MultiActionMapping mapping))
            {
                mapping?.SaveMeta();
            }
        }

        public static MultiActionIndice GetActionIndiceForItemID(int itemID)
        {
            if (dict_indice.TryGetValue(itemID, out MultiActionIndice indice))
                return indice;
            ItemClass item = ItemClass.GetForId(itemID);
            if (item == null)
                return indice;
            indice = new MultiActionIndice(item);
            dict_indice[itemID] = indice;
            return indice;
        }

        public static void ToggleLocalActionIndex(EntityPlayerLocal player)
        {
            if (player == null || !dict_mappings.TryGetValue(player.entityId, out MultiActionMapping mapping))
                return;

            if (mapping.ModeCount <= 1 || player.inventory.IsHoldingItemActionRunning())
                return;
            int prevMode = mapping.CurMode;
            mapping.CurMode++;
            if (prevMode != mapping.CurMode)
            {
                FireToggleModeEvent(player, mapping);
                player.inventory.CallOnToolbeltChangedInternal();
            }
        }

        public static void FireToggleModeEvent(EntityPlayerLocal player, MultiActionMapping mapping)
        {
            player.PlayOneShot(mapping.toggleSound);
            LocalModeChanged = true;
            player.MinEventContext.ItemActionData = player.inventory.holdingItemData.actionData[mapping.CurActionIndex];
            player.FireEvent(CustomEnums.onSelfItemSwitchMode);
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
            if (entity == null || !dict_mappings.TryGetValue(entity.entityId, out var mapping) || mapping == null)
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

            int mode = mapping.indices.GetModeForAction(actionIndex);
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

        internal static float inputCD = 0;
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

            if (PlayerActionKFLib.Instance.ToggleActionMode && PlayerActionKFLib.Instance.ToggleActionMode.WasPressed)
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

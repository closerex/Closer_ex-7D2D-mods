using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdMultiActionItemValueDebug : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;

        public override int DefaultPermissionLevel => base.DefaultPermissionLevel;

        public override bool AllowedInMainMenu => false;

        private static FieldInfo fld_metadata = AccessTools.Field(typeof(ItemValue), "Metadata");
        private static FieldInfo fld_metatag = AccessTools.Field(typeof(TypedMetadataValue), "typeTag");

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
            if (player)
            {
                ItemValue itemValue = player.inventory.holdingItemItemValue;
                if (itemValue != null && itemValue.ItemClass != null)
                {
                    var metadata = fld_metadata.GetValue(itemValue) as Dictionary<string, TypedMetadataValue>;
                    if (metadata != null)
                    {
                        Log.Out("Logging metadata...");
                        foreach (var pair in metadata)
                        {
                            if (pair.Value != null)
                            {
                                Log.Out($"key: {pair.Key}, type: {fld_metatag.GetValue(pair.Value).ToString()}, value: {pair.Value.GetValue()}");
                            }
                        }
                    }
                    else
                    {
                        Log.Out("Metadata is null!");
                    }
                    Log.Out($"meta: {itemValue.Meta}, ammo index: {itemValue.SelectedAmmoTypeIndex}");
                }
            }
        }

        public override string[] getCommands()
        {
            return new string[] { "maivd" };
        }

        public override string getDescription()
        {
            return "Debug ItemValue metadata and stuff.";
        }
    }
}

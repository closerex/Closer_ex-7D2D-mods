using System;
using System.Collections.Generic;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdPrintLocalCache : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;

        public override bool AllowedInMainMenu => false;

        public override int DefaultPermissionLevel => 1000;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count != 1)
                return;
            int index = int.Parse(_params[0]);
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player != null && player.inventory.holdingItemData.actionData[index] is IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData> module)
            {
                ActionModuleLocalPassiveCache.LocalPassiveCacheData instance = module.Instance;
                foreach (int hash in instance)
                {
                    Log.Out($"cache {instance.GetCachedName(hash)} value {instance.GetCachedValue(hash)}");

                }
            }
        }

        public override string[] getCommands()
        {
            return new[] { "plc" };
        }

        public override string getDescription()
        {
            return "Show local cache for current holding item.";
        }
    }
}

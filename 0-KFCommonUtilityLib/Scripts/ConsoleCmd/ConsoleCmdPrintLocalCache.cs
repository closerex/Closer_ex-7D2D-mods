using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Collections.Generic;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdPrintLocalCache : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;

        public override bool AllowedInMainMenu => false;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (_params.Count != 1)
                return;
            int index = int.Parse(_params[0]);
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player != null && player.inventory.holdingItemData.actionData[index] is IModuleContainerFor<ActionModuleLocalPassiveCache.LocalPassiveCacheData> module)
            {
                ActionModuleLocalPassiveCache.LocalPassiveCacheData instance = module.Instance;
                for (int i = 0; i < instance.cache.Length; i++)
                {
                    Log.Out($"cache {instance._cacheModule.nameHashes[i]} value {instance.cache[i]}");
                }
            }
        }

        protected override string[] getCommands()
        {
            return new[] { "pc" };
        }

        protected override string getDescription()
        {
            throw new NotImplementedException();
        }
    }
}

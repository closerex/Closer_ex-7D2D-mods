using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using UniLinq;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdTestModule : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;

        public override int DefaultPermissionLevel => 1000;

        public override bool AllowedInMainMenu => true;

        private static readonly MethodInfo patchMethodGeneric = AccessTools.Method(typeof(ModuleManagers),
            nameof(ModuleManagers.PatchType),
            new[] { typeof(Type), typeof(Type), typeof(TypeBuilder), typeof(Type[]), typeof(Type).MakeByRefType() });

        public override string[] getCommands()
        {
            return new[] { "testmd" };
        }

        public override string getDescription()
        {
            return "Testing specified module.";
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (ModuleManagers.Inited)
            {
                Log.Error("Can't execute now!");
                return;
            }
            
            int count = _params.Count;
            if (count < 2)
            {
                Log.Error("Requires at least the target module type and base type!");
                return;
            }

            Type moduleType = AccessTools.TypeByName(_params[0]);
            if (moduleType == null)
            {
                Log.Error("Module type not found!");
                return;
            }
            
            Type procType = AccessTools.TypeByName(_params[1]);
            if (procType == null)
            {
                Log.Error("Processor type not found!");
                return;
            }

            var attr = procType.GetCustomAttribute<TypeTargetAttribute>();
            if (attr == null || attr.BaseType == null)
            {
                Log.Error("Invalid processor!");
                return;
            }
            
            Type baseType = attr.BaseType;
            Type[] targetTypes = null;
            if (count == 2)
            {
                targetTypes = AccessTools.AllTypes().Where(baseType.IsAssignableFrom).ToArray();
            }
            else
            {
                targetTypes = new Type[count - 2];
                for (int i = 2; i < count; i++)
                {
                    targetTypes[i - 2] = AccessTools.TypeByName(_params[i]);
                    if (targetTypes[i - 2] == null)
                    {
                        Log.Error($"Target type {targetTypes[i - 2]} not found!");
                        return;
                    }
                }
            }

            Type[] moduleTypeArr = { moduleType };
            var patchMethod = patchMethodGeneric.MakeGenericMethod(procType);
            try
            {
                ModuleManagers.InitNew("TestModule");
                foreach (var targetType in targetTypes)
                {
                    try
                    {
                        patchMethod.Invoke(null, new object[] { targetType, baseType, null, moduleTypeArr, null });
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e);
                    }
                }
            }
            finally
            {
                ModuleManagers.FinishAndLoad(false);
            }

            Log.Out("Finished!");
        }
    }
}
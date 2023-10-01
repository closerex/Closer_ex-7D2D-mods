using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ConsoleCmdReloadLog : ConsoleCmdAbstract
{
    public static bool LogInfo { get; private set; } = false;

    public override bool IsExecuteOnClient => true;

    public override bool AllowedInMainMenu => false;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        LogInfo = !LogInfo;
        Log.Out($"Log Reload Info: {LogInfo}");
    }

    protected override string[] getCommands()
    {
        return new string[] { "reloadlog", "rl" };
    }

    protected override string getDescription()
    {
        return "Print reload animation length and multiplier.";
    }
}
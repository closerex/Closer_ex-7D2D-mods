using System.Collections.Generic;

public class ConsoleCmdReloadLog : ConsoleCmdAbstract
{
    public static bool LogInfo { get; private set; } = false;

    public override bool IsExecuteOnClient => true;

    public override bool AllowedInMainMenu => false;

    public override int DefaultPermissionLevel => 1000;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        LogInfo = !LogInfo;
        Log.Out($"Log Reload Info: {LogInfo}");
    }

    public override string[] getCommands()
    {
        return new string[] { "reloadlog", "rlog" };
    }

    public override string getDescription()
    {
        return "Print reload animation length and multiplier.";
    }
}
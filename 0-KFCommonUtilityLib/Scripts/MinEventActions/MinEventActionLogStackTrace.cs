using UnityEngine;

public class MinEventActionLogStackTrace : MinEventActionLogMessage
{
    public override void Execute(MinEventParams _params)
    {
        base.Execute(_params);
        Log.Out($"Stack Trace:\n{StackTraceUtility.ExtractStackTrace()}");
    }
}

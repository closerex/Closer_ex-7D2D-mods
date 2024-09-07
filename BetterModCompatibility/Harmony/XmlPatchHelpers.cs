using UnityEngine;

internal static class XmlPatchHelpers
{
    public static void LogInsteadOfThrow(string errInfo)
    {
        Log.Warning($"{errInfo}\n{StackTraceUtility.ExtractStackTrace()}");
    }
}
using UnityEngine;

public class MinEventActionLogParams : MinEventActionLogMessage
{
    public override void Execute(MinEventParams _params)
    {
        base.Execute(_params);
        Log.Out($"Params: Area: {_params.Area}" +
                $"\nBiome: {_params.Biome?.ToString() ?? "Null"}" +
                $"\nBlockValue:{_params.BlockValue.ToString()}" +
                $"\nBuffName: {_params.Buff?.BuffName ?? "Null"}" +
                $"\nChallenge: {_params.Challenge?.ChallengeClass?.Name ?? "Null"}" +
                $"\nInstigator: {_params.Instigator?.GetDebugName() ?? "Null"}" +
                $"\nIsLocal: {_params.IsLocal}" +
                $"\nTags: {_params.Tags.ToString()}");
    }
}

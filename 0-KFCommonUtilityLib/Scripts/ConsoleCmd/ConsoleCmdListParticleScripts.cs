using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConsoleCmdListParticleScripts : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;

    public override bool AllowedInMainMenu => false;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if(_params.Count > 1)
        {
            Log.Error("Invalid param count: expecting 0 or 1!");
            return;
        }

        if (_params.Count == 0)
        {
            HashSet<string> typeNames = new HashSet<string>();
            foreach (var pe in ParticleEffect.loadedTs.Values)
            {
                foreach (var script in pe.GetComponentsInChildren<MonoBehaviour>())
                {
                    typeNames.Add(script.GetType().AssemblyQualifiedName);
                }
            }
            string print = string.Join("\n", typeNames.ToList().OrderBy(s => s));
            Log.Out($"Listing all scripts...\n{print}\n");
        }
        else
        {
            int id = ParticleEffect.ToId(_params[0]);
            var pe = ParticleEffect.GetDynamicTransform(id);
            if (pe)
            {
                string print = "";
                foreach (var script in pe.GetComponentsInChildren<MonoBehaviour>())
                {
                    print += $"{(script.transform.parent != null ? pe.GetChildPath(script.transform) : script.transform.name)} - {script.GetType().AssemblyQualifiedName}\n";
                }
                Log.Out($"{_params[0]} has following scripts attached:\n{print}");
            }
        }
    }

    public override string[] getCommands()
    {
        return new[] { "listpes" };
    }

    public override string getDescription()
    {
        return "list monobehaviour on all the particle effects that are currently loaded, or the specified one only.";
    }
}
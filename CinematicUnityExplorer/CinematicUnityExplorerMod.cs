using CueMcp.RuntimeAdapter;
using UnityExplorer;

namespace CinematicUnityExplorer
{
    public class CinematicUnityExplorerMod : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            ExplorerStandalone.CreateInstance();
            RuntimeAdapterBootstrap.Start();
        }
    }
}
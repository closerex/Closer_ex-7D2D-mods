namespace FPVLegs
{
    public class FPVLegsInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (!inited)
            {
                inited = true;
                Log.Out("Loading Patch: " + GetType());
                var harmony = new HarmonyLib.Harmony(GetType().ToString());
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            }
        }
    }
}

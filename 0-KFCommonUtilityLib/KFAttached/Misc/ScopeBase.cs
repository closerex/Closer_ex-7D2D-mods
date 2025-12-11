using UnityEngine;

public class ScopeBase : MonoBehaviour
{
    public AimReference defaultReference;
#if NotEditor
    public ActionModuleProceduralAiming.ProceduralAimingData aimingModule;
#endif
}
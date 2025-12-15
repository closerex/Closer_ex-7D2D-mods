using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib
{
    public static class VersionPatchManager
    {
        static VersionPatchManager()
        {
            #region XUiC_HUDStatBar.LocalPlayer
            DynamicMethod dynmtd = new DynamicMethod("GetLocalPlayer", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(EntityPlayerLocal), new[] { typeof(XUiC_HUDStatBar) }, typeof(XUiC_HUDStatBar), true);
            ILGenerator generator = dynmtd.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
            {
                generator.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(XUiC_HUDStatBar), "LocalPlayer"));
            }
            else
            {
                generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(XUiC_HUDStatBar), "localPlayer"));
            }
            generator.Emit(OpCodes.Ret);
            XUiC_HUDStatBar_LocalPlayer = (Func<XUiC_HUDStatBar, EntityPlayerLocal>)dynmtd.CreateDelegate(typeof(Func<XUiC_HUDStatBar, EntityPlayerLocal>));

            #endregion

            #region XUiC_HUDStatBar.Vehicle
            dynmtd = new DynamicMethod("GetLocalPlayer", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(EntityVehicle), new[] { typeof(XUiC_HUDStatBar) }, typeof(XUiC_HUDStatBar), true);
            generator = dynmtd.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            if (Constants.cVersionInformation.LTE(VersionInformation.EGameReleaseType.V, 2, 4))
            {
                generator.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(XUiC_HUDStatBar), "Vehicle"));
            }
            else
            {
                generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(XUiC_HUDStatBar), "vehicle"));
            }
            generator.Emit(OpCodes.Ret);
            XUiC_HUDStatBar_Vehicle = (Func<XUiC_HUDStatBar, EntityVehicle>)dynmtd.CreateDelegate(typeof(Func<XUiC_HUDStatBar, EntityVehicle>));
            #endregion

            #region ItemActionRanged.ReloadSuccess
            dynmtd = new DynamicMethod("OnReloadSuccess", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, null, new[] { typeof(ItemActionRanged), typeof(ItemActionRanged.ItemActionDataRanged) }, typeof(ItemActionRanged), true);
            generator = dynmtd.GetILGenerator();
            if (Constants.cVersionInformation.GTE(VersionInformation.EGameReleaseType.V, 2, 5))
            {
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Call, AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ReloadSuccess)));
            }
            generator.Emit(OpCodes.Ret);
            ItemActionRanged_ReloadSuccess = (Action<ItemActionRanged, ItemActionRanged.ItemActionDataRanged>)dynmtd.CreateDelegate(typeof(Action<ItemActionRanged, ItemActionRanged.ItemActionDataRanged>));
            #endregion

            #region ItemAction.ExecutionRequirements
            dynmtd = new DynamicMethod("CopyRequirements", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, null, new[] { typeof(ItemAction), typeof(ItemAction) }, typeof(ItemAction), true);
            generator = dynmtd.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, AccessTools.Field(typeof(ItemAction), nameof(ItemAction.ExecutionRequirements)));
            generator.Emit(OpCodes.Stfld, AccessTools.Field(typeof(ItemAction), nameof(ItemAction.ExecutionRequirements)));
            generator.Emit(OpCodes.Ret);
            ItemAction_CopyRequirements = (Action<ItemAction, ItemAction>)dynmtd.CreateDelegate(typeof(Action<ItemAction, ItemAction>));
            #endregion
        }

        public readonly static Func<XUiC_HUDStatBar, EntityPlayerLocal> XUiC_HUDStatBar_LocalPlayer;
        public static EntityPlayerLocal GetLocalPlayer(this XUiC_HUDStatBar self)
        {
            return XUiC_HUDStatBar_LocalPlayer(self);
        }

        public readonly static Func<XUiC_HUDStatBar, EntityVehicle> XUiC_HUDStatBar_Vehicle;
        public static EntityVehicle GetVehicle(this XUiC_HUDStatBar self)
        {
            return XUiC_HUDStatBar_Vehicle(self);
        }

        public readonly static Action<ItemActionRanged, ItemActionRanged.ItemActionDataRanged> ItemActionRanged_ReloadSuccess;
        public static void OnReloadSuccess(this ItemActionRanged self, ItemActionRanged.ItemActionDataRanged data)
        {
            ItemActionRanged_ReloadSuccess(self, data);
        }

        public readonly static Action<ItemAction, ItemAction> ItemAction_CopyRequirements;
        public static void CopyRequirements(ItemAction from, ItemAction to)
        {
            ItemAction_CopyRequirements(from, to);
        }

        #region Version Compare Helpers
        public static bool LT(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) < 0;
        }

        public static bool LTE(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) <= 0;
        }

        public static bool Equals(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) == 0;
        }

        public static bool NotEquals(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) != 0;
        }

        public static bool GT(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) > 0;
        }

        public static bool GTE(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build = -1)
        {
            return self.CompareVersion(releaseType, major, minor, build) >= 0;
        }

        public static int CompareVersion(this VersionInformation self, VersionInformation.EGameReleaseType releaseType, int major, int minor, int build)
        {
            int res = self.ReleaseType - releaseType;
            if (res != 0)
            {
                return res;
            }

            res = self.Major - major;
            if (res != 0)
            {
                return res;
            }

            res = self.Minor - minor;
            if (res != 0)
            {
                return res;
            }

            if (build >= 0)
            {
                return self.Build - build;
            }
            return 0;
        }
        #endregion
    }
}

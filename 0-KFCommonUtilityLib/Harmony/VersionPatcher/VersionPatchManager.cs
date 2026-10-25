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

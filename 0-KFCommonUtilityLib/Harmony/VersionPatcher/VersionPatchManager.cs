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
        private static readonly Func<MinEventActionModifyCVar, bool> readCVarRef;
        private static readonly Func<MinEventActionModifyCVar, string> readRefCVarName;
        private static readonly Func<MinEventActionModifyCVar, float> readValue;
        private static readonly Action<MinEventActionModifyCVar, float> writeValue;

        static VersionPatchManager()
        {
            // V3.4.0 uses Minor=40; only these three members changed representation.
            bool useProperties = Constants.cVersionInformation.GTE(VersionInformation.EGameReleaseType.V, 3, 40);
            readCVarRef = CreateCVarGetter<bool>("cvarRef", useProperties);
            readRefCVarName = CreateCVarGetter<string>("refCvarName", useProperties);
            readValue = CreateCVarGetter<float>("value", useProperties);

            var owner = typeof(MinEventActionModifyCVar);
            var method = new DynamicMethod("WriteModifyCVarValue", typeof(void), new[] { owner, typeof(float) }, owner, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            if (useProperties)
            {
                var setter = owner.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetSetMethod(true);
                if (setter == null || setter.ReturnType != typeof(void) || setter.GetParameters().Length != 1 || setter.GetParameters()[0].ParameterType != typeof(float))
                    throw new MissingMethodException(owner.FullName, "set_value(System.Single)");
                il.Emit(OpCodes.Call, setter);
            }
            else
            {
                var field = owner.GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null || field.FieldType != typeof(float))
                    throw new MissingFieldException(owner.FullName, "value");
                il.Emit(OpCodes.Stfld, field);
            }
            il.Emit(OpCodes.Ret);
            writeValue = (Action<MinEventActionModifyCVar, float>)method.CreateDelegate(typeof(Action<MinEventActionModifyCVar, float>));
        }

        public static void Init() { }

        private static Func<MinEventActionModifyCVar, T> CreateCVarGetter<T>(string name, bool useProperties)
        {
            var owner = typeof(MinEventActionModifyCVar);
            var method = new DynamicMethod("ReadModifyCVar_" + name, typeof(T), new[] { owner }, owner, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (useProperties)
            {
                var getter = owner.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetGetMethod(true);
                if (getter == null || getter.ReturnType != typeof(T) || getter.GetParameters().Length != 0)
                    throw new MissingMethodException(owner.FullName, "get_" + name + "()");
                il.Emit(OpCodes.Call, getter);
            }
            else
            {
                var field = owner.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null || field.FieldType != typeof(T))
                    throw new MissingFieldException(owner.FullName, name);
                il.Emit(OpCodes.Ldfld, field);
            }
            il.Emit(OpCodes.Ret);
            return (Func<MinEventActionModifyCVar, T>)method.CreateDelegate(typeof(Func<MinEventActionModifyCVar, T>));
        }

        public static bool ReadCVarRef(MinEventActionModifyCVar action) => readCVarRef(action);
        public static string ReadRefCVarName(MinEventActionModifyCVar action) => readRefCVarName(action);
        public static float ReadValue(MinEventActionModifyCVar action) => readValue(action);
        public static void WriteValue(MinEventActionModifyCVar action, float value) => writeValue(action, value);
        
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

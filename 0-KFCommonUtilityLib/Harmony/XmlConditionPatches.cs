using System;
using System.IO;
using HarmonyLib;
using NCalc;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public static class XmlConditionPatches
    {
        [HarmonyPatch(typeof(XmlPatchConditionEvaluator), nameof(XmlPatchConditionEvaluator.NCalcEvaluateFunction))]
        [HarmonyPostfix]
        private static void Postfix_XmlPatchConditionEvaluator_NCalcEvaluationFunction(string _name, FunctionArgs _args)
        {
            if (_name.EqualsCaseInsensitive("path_exists"))
            {
                path_exists(_args);
                return;
            }
        }

        private static void path_exists(FunctionArgs _args)
        {
            if (_args.Parameters.Length != 1)
            {
                throw new ArgumentException(
                    $"Calling function path_exists with invalid number of arguments ({_args.Parameters.Length}, expected 1)");
            }

            if (_args.Parameters[0].Evaluate() is not string path)
            {
                throw new ArgumentException("Calling function path_exists: Expected string argument");
            }
            
            path = ModManager.PatchModPathString(path);
            _args.Result = !string.IsNullOrEmpty(path) && (Directory.Exists(path) || File.Exists(path));
        }
    }
}
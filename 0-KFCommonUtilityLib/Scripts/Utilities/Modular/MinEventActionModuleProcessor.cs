using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using KFCommonUtilityLib.Attributes;

namespace KFCommonUtilityLib
{
    public interface ICustomXElementParser
    {
        bool ParseModuleAttribute(MinEventActionBase parent, XAttribute attr);
        bool ParseModuleElements(MinEventActionBase parent, XElement elem);
    }
    
    [TypeTarget(typeof(MinEventActionBase))]
    public struct MinEventActionModuleProcessor : IModuleProcessor
    {
        public void InitModules(ModuleManipulator manipulator)
        {
            
        }

        public bool DefineConstructorArgs(ModuleManipulator manipulator, ConstructorBuilder ctorbd, out ParameterBuilder[] pbs)
        {
            pbs = null;
            return false;
        }

        public void BuildConstructor(ModuleManipulator manipulator, ILGenerator generator)
        {
            
        }

        public Type GetModuleTypeByName(string name)
        {
            return ReflectionHelpers.GetTypeWithPrefix("MEAModule", name);
        }

        public bool MatchSpecialArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par,
            MethodPatchInfo mtdpinf_derived, MethodOverrideInfo mtdoinf_target)
        {
            return false;
        }

        public bool MatchConstructorArgs(ModuleManipulator manipulator, ILGenerator generator, ParameterInfo par,
            ParameterBuilder[] paramInfo, Type[] paramTypes, ConstructorInfo ctorinf_target, int moduleIndex)
        {
            return false;
        }

        public void ApplyPatchModifiers(PatchInfo patchInfo, string owner)
        {
            
        }

        public static void ParseModuleAttribute(MinEventActionBase minEvent, List<XElement> moduleElements)
        {
            if (minEvent is IModuleCollection collection)
            {
                var modules = collection.Modules;
                if (modules != null && modules.Length == moduleElements.Count)
                {
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var module = modules[i];
                        if (module is ICustomXElementParser parser)
                        {
                            foreach (var attr in moduleElements[i].Attributes())
                            {
                                _ = parser.ParseModuleAttribute(minEvent, attr);
                            }

                            foreach (var elem in moduleElements[i].Elements())
                            {
                                _ = parser.ParseModuleElements(minEvent, elem);
                            }
                        }
                    }
                }
                else
                {
                    Log.Error($"Module length does not match!");
                }
            }
        }
    }
}
using HarmonyLib;
using HarmonyLib.Public.Patching;
using KFCommonUtilityLib.Harmony;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdDumpHarmonyPatches : ConsoleCmdAbstract
    {
        private const TypeAttributes STATIC_ATTR = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit;
        private const TypeAttributes NESTED_STATIC_ATTR = TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit;


        public override bool IsExecuteOnClient => true;

        public override int DefaultPermissionLevel => 1000;

        public override bool AllowedInMainMenu => true;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            var allPatchedMethods = PatchManager.GetPatchedMethods();
            if (allPatchedMethods.Any())
            {
                string assname = "HarmonyDump" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Mod self = ModManager.GetMod("CommonUtilityLib");
                if (self == null)
                {
                    Log.Warning("Failed to get mod!");
                    self = ModManager.GetModForAssembly(typeof(ModuleManagers).Assembly);
                }
                DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(self.Path, "AssemblyOutput"));
                AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(new System.Reflection.AssemblyName(assname), AssemblyBuilderAccess.Save, dirInfo.FullName);
                ModuleBuilder mb = ab.DefineDynamicModule(assname, assname + ".dll", false);
                DictionaryList<Type, TypeBuilder> allTBs = new();
                foreach (var mtd in allPatchedMethods)
                {
                    var pinfo = mtd.ToPatchInfoDontAdd().Copy();
                    var decType = mtd.DeclaringType;
                    //create container type if not created yet
                    if (!allTBs.dict.TryGetValue(decType, out var tb))
                    {
                        if (decType.IsNested)
                        {
                            ModuleManagers.LogOut($"Cehcking nested type {decType.FullDescription()}");
                            Stack<Type> parentTypes = new();
                            var rootType = decType.DeclaringType;

                            if (!allTBs.dict.TryGetValue(rootType, out var parentTB))
                            {
                                while (rootType.IsNested)
                                {
                                    ModuleManagers.LogOut($"Marked nest parent {rootType.FullDescription()} for Creation");
                                    parentTypes.Push(rootType);
                                    rootType = rootType.DeclaringType;
                                    if (allTBs.dict.TryGetValue(rootType, out parentTB))
                                    {
                                        break;
                                    }
                                }
                            }

                            //found the undefined root type, create it
                            if (parentTB == null)
                            {
                                ModuleManagers.LogOut($"No parent TypeBuilder found, creating root type from {rootType.FullDescription()}");
                                parentTB = mb.DefineType($"{rootType.FullName}_{rootType.Assembly.GetName().Name}", STATIC_ATTR);
                                allTBs.Add(rootType, parentTB);
                            }

                            var directParentTB = parentTB;
                            while(parentTypes.TryPop(out var nestedParentType))
                            {
                                ModuleManagers.LogOut($"Creating nested parent type from {nestedParentType.FullDescription()}");
                                directParentTB = directParentTB.DefineNestedType(nestedParentType.Name, NESTED_STATIC_ATTR);
                                allTBs.Add(nestedParentType, directParentTB);
                            }

                            ModuleManagers.LogOut($"Creating nested type from {decType.FullDescription()}");
                            tb = directParentTB.DefineNestedType(decType.Name, NESTED_STATIC_ATTR);
                            allTBs.Add(decType, tb);
                        }
                        else
                        {
                            ModuleManagers.LogOut($"Creating type from {decType.FullDescription()}");
                            tb = mb.DefineType($"{decType.FullName}_{decType.Assembly.GetName().Name}", STATIC_ATTR);
                            allTBs.Add(decType, tb);
                        }
                    }

                    pinfo.AddILManipulators("harmony.dump.cmd", new HarmonyMethod(AccessTools.Method(typeof(CallClosureFixPatches), nameof(CallClosureFixPatches.DelegateManipulator))));
                    var patcher = mtd.GetMethodPatcher();
                    var dmd = patcher.CopyOriginal();
                    mtd.CopyParamInfoTo(dmd);
                    var context = new ILContext(dmd.Definition);
                    CallClosureFixPatches.ApplyFix(tb);
                    HarmonyManipulator.Manipulate(mtd, pinfo, context);
                    CallClosureFixPatches.RemoveFix();
                    dmd.GenerateMethodBuilder(tb);
                }

                foreach (var tb in allTBs.list)
                {
                    tb.CreateType();
                }
                ab.Save(assname + ".dll");
            }
            else
            {
                Log.Out($"No patched methods found.");
            }
        }

        public override string[] getCommands()
        {
            return new string[] { "hmdump" };
        }

        public override string getDescription()
        {
            return "Dump harmony patch result into a dll.";
        }
    }
}

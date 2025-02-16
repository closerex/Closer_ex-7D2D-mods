using KFCommonUtilityLib.Scripts.Attributes;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Permissions;
using UniLinq;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public static class ModuleManagers
    {
        public static AssemblyDefinition WorkingAssembly { get; private set; } = null;
        public static event Action OnAssemblyCreated;
        public static event Action OnAssemblyLoaded;
        public static bool Inited { get; private set; }
        private static readonly List<Assembly> list_created = new List<Assembly>();
        private static DefaultAssemblyResolver resolver;
        private static ModuleAttributes moduleAttributes;
        private static ModuleCharacteristics moduleCharacteristics;

        internal static void ClearOutputFolder()
        {
            Mod self = ModManager.GetMod("CommonUtilityLib");
            string path = Path.Combine(self.Path, "AssemblyOutput");
            if (Directory.Exists(path))
                Array.ForEach(Directory.GetFiles(path), File.Delete);
            else
                Directory.CreateDirectory(path);
        }

        internal static void InitNew()
        {
            if (Inited)
            {
                return;
            }
            WorkingAssembly?.Dispose();
            if (resolver == null)
            {
                resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.Combine(Application.dataPath, "Managed"));

                foreach (var mod in ModManager.GetLoadedMods())
                {
                    resolver.AddSearchDirectory(mod.Path);
                }

                AssemblyDefinition assdef_main = AssemblyDefinition.ReadAssembly($"{Application.dataPath}/Managed/Assembly-CSharp.dll", new ReaderParameters() { AssemblyResolver = resolver });
                moduleAttributes = assdef_main.MainModule.Attributes;
                moduleCharacteristics = assdef_main.MainModule.Characteristics;
                Log.Out("Reading Attributes from assembly: " + assdef_main.FullName);
            }
            string assname = "RuntimeAssembled" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            WorkingAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(assname,
                                                                                           new Version(0, 0, 0, 0)),
                                                                                           assname + ".dll",
                                                                                           new ModuleParameters()
                                                                                           {
                                                                                               Kind = ModuleKind.Dll,
                                                                                               AssemblyResolver = resolver,
                                                                                               Architecture = TargetArchitecture.I386,
                                                                                               Runtime = TargetRuntime.Net_4_0,
                                                                                           });
            WorkingAssembly.MainModule.Attributes = moduleAttributes;
            WorkingAssembly.MainModule.Characteristics = moduleCharacteristics;
            //write security attributes so that calling non-public patch methods from this assembly is allowed
            Mono.Cecil.SecurityAttribute sattr_permission = new Mono.Cecil.SecurityAttribute(WorkingAssembly.MainModule.ImportReference(typeof(SecurityPermissionAttribute)));
            Mono.Cecil.CustomAttributeNamedArgument caarg_SkipVerification = new Mono.Cecil.CustomAttributeNamedArgument(nameof(SecurityPermissionAttribute.SkipVerification), new CustomAttributeArgument(WorkingAssembly.MainModule.TypeSystem.Boolean, true));
            sattr_permission.Properties.Add(caarg_SkipVerification);
            SecurityDeclaration sdec = new SecurityDeclaration(Mono.Cecil.SecurityAction.RequestMinimum);
            sdec.SecurityAttributes.Add(sattr_permission);
            WorkingAssembly.SecurityDeclarations.Add(sdec);
            OnAssemblyCreated?.Invoke();
            Inited = true;
            Log.Out("======Init New======");
        }

        public static bool PatchType(Type targetType, Type baseType, string moduleNames, IModuleProcessor processor, out string typename)
        {
            string[] modules = moduleNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Type[] moduleTypes = modules.Select(s => processor.GetModuleTypeByName(s.Trim()))
                                        .Where(t => t.GetCustomAttribute<TypeTargetAttribute>().BaseType.IsAssignableFrom(targetType)).ToArray();
            typename = ModuleUtils.CreateTypeName(targetType, moduleTypes);
            //Log.Out(typename);
            if (!ModuleManagers.TryFindType(typename, out _) && !ModuleManagers.TryFindInCur(typename, out _))
                _ = new ModuleManipulator(ModuleManagers.WorkingAssembly, processor, targetType, baseType, moduleTypes);
            return true;
        }

        internal static void FinishAndLoad()
        {
            if (!Inited)
            {
                return;
            }
            //output assembly
            Mod self = ModManager.GetMod("CommonUtilityLib");
            if (self == null)
            {
                Log.Warning("Failed to get mod!");
                self = ModManager.GetModForAssembly(typeof(ItemActionModuleManager).Assembly);
            }
            if (self != null && WorkingAssembly != null && WorkingAssembly.MainModule.Types.Count > 1)
            {
                Log.Out("Assembly is valid!");
                Log.Out("======Finish and Load======");
                using (MemoryStream ms = new MemoryStream())
                {
                    try
                    {
                        WorkingAssembly.Write(ms);
                    }
                    catch (Exception)
                    {
                        new ConsoleCmdShutdown().Execute(new List<string>(), new CommandSenderInfo());
                    }
                    DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(self.Path, "AssemblyOutput"));
                    string filename = Path.Combine(dirInfo.FullName, WorkingAssembly.Name.Name + ".dll");
                    Log.Out("Output Assembly: " + filename);
                    using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        ms.WriteTo(fs);
                    }
                    Assembly newAssembly = Assembly.LoadFile(filename);
                    list_created.Add(newAssembly);
                }
                Inited = false;
                OnAssemblyLoaded?.Invoke();
                Cleanup();
            }
        }

        //cleanup
        internal static void Cleanup()
        {
            Inited = false;
            WorkingAssembly?.Dispose();
            WorkingAssembly = null;
        }

        /// <summary>
        /// Check if type is already generated in previous assemblies.
        /// </summary>
        /// <param name="name">Full type name.</param>
        /// <param name="type">The retrieved type, null if not found.</param>
        /// <returns>true if found.</returns>
        public static bool TryFindType(string name, out Type type)
        {
            type = null;
            foreach (var assembly in list_created)
            {
                type = assembly.GetType(name, false);
                if (type != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if type is already generated in current working assembly definition.
        /// </summary>
        /// <param name="name">Full type name.</param>
        /// <param name="typedef">The retrieved type definition, null if not found.</param>
        /// <returns>true if found.</returns>
        public static bool TryFindInCur(string name, out TypeDefinition typedef)
        {
            typedef = WorkingAssembly?.MainModule.GetType(name);
            return typedef != null;
        }
    }
}

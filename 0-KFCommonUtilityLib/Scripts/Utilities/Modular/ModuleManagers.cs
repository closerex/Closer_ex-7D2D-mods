using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using UniLinq;

namespace KFCommonUtilityLib
{
    public static class ModuleManagers
    {
        private static class ModuleExtensions<T>
        {
            public readonly static List<Type> extensions = new();
        }
        public static AssemblyBuilder WorkingAssembly { get; private set; } = null;
        public static ModuleBuilder WorkingModule { get; private set; } = null;
        public static event Action OnAssemblyCreated;
        public static event Action OnAssemblyFinished;
        public static bool Inited { get; private set; }
        private static bool extensionScanned;
        private static readonly HashSet<string> list_registered_path = new();
        private static readonly List<Assembly> list_created = new();
        private static readonly HashSet<Assembly> set_checked = new();
        //private static DefaultAssemblyResolver resolver;
        //private static ModuleAttributes moduleAttributes;
        //private static ModuleCharacteristics moduleCharacteristics;
        private static MethodInfo mtdinf_findext = AccessTools.Method(typeof(ModuleManagers), nameof(ModuleManagers.AddModuleExtension));
        private static bool debugLog = false;
        private static readonly ConstructorInfo ctorinf_iact = typeof(System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) });

        public static void LogOut(string msg)
        {
            if (debugLog)
            {
                Log.Out(msg);
            }
        }

        public static void InitModuleExtensions()
        {
            if (extensionScanned)
            {
                return;
            }
            var assemblies = ModManager.GetLoadedAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<TypeTargetExtensionAttribute>();
                    if (attr != null)
                    {
                        if ((bool)mtdinf_findext.MakeGenericMethod(attr.ModuleType).Invoke(null, new object[] { type }))
                        {
                            Log.Out($"Found Module Extension {type.FullName}");
                        }
                    }
                }
            }
            extensionScanned = true;
        }

        public static bool AddModuleExtension<T>(Type extType)
        {
            if (typeof(T).GetCustomAttribute<TypeTargetAttribute>() == null)
            {
                return false;
            }

            if (!ModuleExtensions<T>.extensions.Contains(extType))
                ModuleExtensions<T>.extensions.Add(extType);
            return true;
        }

        public static Type[] GetModuleExtensions<T>()
        {
            if (typeof(T).GetCustomAttribute<TypeTargetAttribute>() == null)
            {
                return Type.EmptyTypes;
            }

            return ModuleExtensions<T>.extensions.ToArray();
        }

        public static void AddAssemblySearchPath(string path)
        {
            if (Directory.Exists(path))
            {
                list_registered_path.Add(path);
            }
        }

        internal static void Init()
        {
            KFLibEvents.onXmlLoadingStart += InitNew;
            KFLibEvents.onXmlLoadingFinish += FinishAndLoad;

            Mod self = ModManager.GetMod("CommonUtilityLib");
            string path = Path.Combine(self.Path, "AssemblyOutput");
            if (Directory.Exists(path) && !IsUnderSymlinkOrJunction(path))
                Array.ForEach(Directory.GetFiles(path), File.Delete);
            else
                Directory.CreateDirectory(path);
        }

        private static bool IsUnderSymlinkOrJunction(string path)
        {
            var dir = new DirectoryInfo(path);

            while (dir != null)
            {
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    return true;

                dir = dir.Parent;
            }

            return false;
        }


        internal static void InitNew()
        {
            if (Inited)
            {
                return;
            }
            set_checked.Clear();
            InitModuleExtensions();
            WorkingAssembly = null;
            WorkingModule = null;
            string assname = "RuntimeAssembled" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Mod self = ModManager.GetMod("CommonUtilityLib");
            if (self == null)
            {
                Log.Warning("Failed to get mod!");
                self = ModManager.GetModForAssembly(typeof(ModuleManagers).Assembly);
            }
            DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(self.Path, "AssemblyOutput"));
            WorkingAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assname), AssemblyBuilderAccess.RunAndSave, dirInfo.FullName);
            WorkingAssembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(UnverifiableCodeAttribute).GetConstructor(Type.EmptyTypes), new object[0]));
            WorkingAssembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(SecurityPermissionAttribute).GetConstructor(new Type[] { typeof(SecurityAction) }),
                                                                          new object[] { SecurityAction.RequestMinimum },
                                                                          new[] { typeof(SecurityPermissionAttribute).GetProperty(nameof(SecurityPermissionAttribute.SkipVerification)),
                                                                                  typeof(SecurityAttribute).GetProperty(nameof(SecurityAttribute.Unrestricted)) },
                                                                          new object[] { true,
                                                                                         true }));
            WorkingModule = WorkingAssembly.DefineDynamicModule(assname, assname + ".dll", false);
            WorkingAssembly.SetMonoCorlibInternal(true);
            OnAssemblyCreated?.Invoke();
            Inited = true;
            Log.Out("======Init New======");
        }

        public static bool PatchType<T>(Type targetType, Type baseType, TypeBuilder parentType, string moduleNames, out Type result) where T : IModuleProcessor, new()
        {
            Type[] moduleTypes = moduleNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => new T().GetModuleTypeByName(s.Trim()))
                                            .Where(t => t.GetCustomAttribute<TypeTargetAttribute>().BaseType.IsAssignableFrom(targetType)).ToArray();
            return PatchType(targetType, baseType, parentType, moduleTypes, new T(), out result);
        }

        public static bool PatchType<T>(Type targetType, Type baseType, TypeBuilder parentType, Type[] moduleTypes, out Type result) where T : IModuleProcessor, new()
        {
            return PatchType(targetType, baseType, parentType, moduleTypes, new T(), out result);
        }

        public static bool PatchType<T>(Type targetType, Type baseType, TypeBuilder parentType, Type[] moduleTypes, T processor, out Type result) where T : IModuleProcessor
        {
            if (moduleTypes.Length == 0)
            {
                result = targetType;
                return false;
            }
            string typename = ModuleUtils.CreateTypeName(targetType, moduleTypes);
            if (!ModuleManagers.TryFindType(typename, out result) && !ModuleManagers.TryFindInCur(typename, out result))
                _ = new ModuleManipulator(ModuleManagers.WorkingAssembly, processor, targetType, baseType, parentType, out result, moduleTypes);
            return true;
        }

        internal static void FinishAndLoad()
        {
            if (!Inited)
            {
                return;
            }
            //output assembly
            if (WorkingAssembly != null)
            {
                if (WorkingAssembly.GetTypes().Length > 1)
                {
                    Log.Out("Assembly is valid!");
                    try
                    {
                        WorkingAssembly.Save(WorkingAssembly.GetName().Name + ".dll");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to save assembly file: {ex.Message}\n{ex.StackTrace}");
                    }
                    list_created.Add(WorkingAssembly);
                }

                Log.Out("======Finish and Load======");
                OnAssemblyFinished?.Invoke();
                Cleanup();
            }
        }

        //cleanup
        internal static void Cleanup()
        {
            Inited = false;
            WorkingAssembly = null;
            WorkingModule = null;
            set_checked.Clear();
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
            try
            {
                foreach (var assembly in list_created)
                {
                    type = assembly.GetType(name, false);
                    if (type != null)
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to find type {name} in runtime generated types!");
                Log.Error(ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Check if type is already generated in current working assembly definition.
        /// </summary>
        /// <param name="name">Full type name.</param>
        /// <param name="type">The retrieved type, null if not found.</param>
        /// <returns>true if found.</returns>
        public static bool TryFindInCur(string name, out Type type)
        {
            type = WorkingAssembly?.GetType(name);
            return type != null;
        }
    }
}

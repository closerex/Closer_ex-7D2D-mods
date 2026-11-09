#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace KFAssemblyPatcher
{
    public sealed class AssemblyPatchException : Exception
    {
        public AssemblyPatchException(string message) : base(message) { }
        public AssemblyPatchException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class VirtualizationResult
    {
        internal VirtualizationResult(int methodsChanged, int callSitesChanged, int typesChanged, int fieldsChanged)
        {
            MethodsChanged = methodsChanged;
            CallSitesChanged = callSitesChanged;
            TypesChanged = typesChanged;
            FieldsChanged = fieldsChanged;
        }
        public int MethodsChanged { get; }
        public int CallSitesChanged { get; }
        public int TypesChanged { get; }
        public int FieldsChanged { get; }
        public bool Changed => MethodsChanged != 0 || CallSitesChanged != 0 || TypesChanged != 0 || FieldsChanged != 0;
    }

    public static class AssemblyVirtualizer
    {
        // Repair rules: which game methods must become virtual so that module dispatch
        // reaches derived implementations. Owner + name + return type + the exact parameter
        // list identify a target. Types are spelled exactly as Cecil reports them, so a
        // nested type uses '/' (e.g. "ItemActionRanged/ItemActionDataRanged") and game types
        // this project does not reference are written out by name.
        private static readonly Rule[] Rules =
        {
            // Original repair, present since the first release. Both of these carry module-scoped
            // patches (several modules override TryExecuteAction, and CVarReplacer overrides
            // MinEventActionModifyStats.execute), which is what a vtable slot is for.
            new("ItemActionRanged", "TryExecuteAction", "System.Void", "ItemActionData"),
            new("MinEventActionModifyStats", "execute", "System.Void", "MinEventParams"),
            // Carried by module-scoped patches as well, so they need the same treatment:
            //   ActionModuleLimitedCombo.Prefix_canStartAttack
            //   ActionModuleInspectable.Prefix_SwapSelectedAmmo
            // These were removed with the infrastructure-only entries and are restored here
            // because module patches exist for them, unlike LateInit / SetAttackFinished / etc.
            new("ItemActionDynamicMelee", "canStartAttack", "System.Boolean", "ItemActionDynamicMelee/ItemActionDynamicMeleeData"),
            new("ItemActionRanged", "SwapSelectedAmmo", "System.Void", "EntityAlive", "System.Int32"),
        };

        // These four remain Harmony-only. They are static, and a static method can never own
        // a vtable slot, so no amount of repair can route module dispatch through them. They
        // are excluded from the repair, not forgotten:
        //   ItemAction.ExecuteBuffActions   static void (List<string>, int, EntityAlive, bool, EnumBodyPartHit, string)
        //   ItemActionAttack.Hit            static void (26 parameters)
        //   ItemClass.LateInitAll           static void ()
        //   MinEventActionBase.ParseAction  static MinEventActionBase (XElement)

        private sealed class Rule
        {
            internal Rule(string owner, string name, string returnType, params string[] parameterTypes)
            {
                Owner = owner;
                Name = name;
                ReturnType = returnType;
                ParameterTypes = parameterTypes;
            }

            internal string Owner { get; }
            internal string Name { get; }
            internal string ReturnType { get; }
            internal string[] ParameterTypes { get; }

            // Cheap pre-filter over a whole assembly's member references. It deliberately
            // does not test the return type: a rule must never miss its own call sites, and
            // a MemberRef's return type is not always reported the way the definition spells
            // it. ResolveTarget below performs the authoritative full-signature match.
            internal bool MatchesShape(MethodReference method)
            {
                if (method == null || method.Name != Name || method.Parameters.Count != ParameterTypes.Length)
                    return false;
                for (int i = 0; i < ParameterTypes.Length; i++)
                    if (method.Parameters[i].ParameterType.FullName != ParameterTypes[i])
                        return false;
                return true;
            }

            // Authoritative match against a method resolved inside the game assembly. The
            // return type is part of the identity on purpose: the game declares same-named
            // methods on unrelated types (LootContainer.ExecuteBuffActions,
            // AnimationEventBridge.Hit, Block.LateInitAll, ...) and overloads must not be
            // able to satisfy a rule written for a different shape.
            internal bool Matches(MethodDefinition method) =>
                method.DeclaringType.FullName == Owner && method.Name == Name &&
                method.ReturnType.FullName == ReturnType &&
                method.Parameters.Count == ParameterTypes.Length &&
                method.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(ParameterTypes);
        }

        public static VirtualizationResult Analyze(AssemblyDefinition game) =>
            Analyze(game, Array.Empty<AssemblyDefinition>());

        public static VirtualizationResult Analyze(AssemblyDefinition game,
            IEnumerable<AssemblyDefinition> modules) =>
            Visit(game, game,
                targets => targets.Values.Count(NeedsFlags),
                null, true, modules);

        public static VirtualizationResult AnalyzeMod(AssemblyDefinition mod, AssemblyDefinition game) =>
            AnalyzeMod(mod, game, Array.Empty<AssemblyDefinition>());

        public static VirtualizationResult AnalyzeMod(AssemblyDefinition mod, AssemblyDefinition game,
            IEnumerable<AssemblyDefinition> modules) =>
            // A mod contributes no method flags of its own: it is rewritten only where its own
            // call sites must reach the game's virtual dispatch.
            Visit(mod, game, null, null, false, modules);

        public static VirtualizationResult Transform(AssemblyDefinition game) =>
            Transform(game, Array.Empty<AssemblyDefinition>());

        public static VirtualizationResult Transform(AssemblyDefinition game,
            IEnumerable<AssemblyDefinition> modules) =>
            Visit(game, game,
                targets => targets.Values.Count(NeedsFlags),
                VirtualizeMethods, true, modules);

        public static VirtualizationResult TransformMod(AssemblyDefinition mod, AssemblyDefinition game) =>
            TransformMod(mod, game, Array.Empty<AssemblyDefinition>());

        public static VirtualizationResult TransformMod(AssemblyDefinition mod, AssemblyDefinition game,
            IEnumerable<AssemblyDefinition> modules) =>
            Visit(mod, game, null, RewriteCallSites, false, modules);

        // The game and the mods answer the same three questions through the same scan: which
        // rules resolve, which overrides exist across the whole collection, and which call
        // sites the repair would rewrite. Only what is done with the answers differs, so the
        // scan itself lives here once.
        //
        // countMethods is null for a mod: it contributes no method flags of its own, it is
        // rewritten only where its own call sites must reach the game's virtual dispatch.
        // The writability check belongs to the mod case and is therefore part of the count.
        private static VirtualizationResult Visit(AssemblyDefinition assembly, AssemblyDefinition game,
            Func<Dictionary<string, MethodDefinition>, int> countMethods,
            Action<Dictionary<string, MethodDefinition>, List<Instruction>> apply,
            bool requireCoverage, IEnumerable<AssemblyDefinition> modules)
        {
            int typesChanged = 0, fieldsChanged = 0;
            if (requireCoverage)
                InventoryLayout(game.MainModule, false, out typesChanged, out fieldsChanged);
            var targets = ResolveTargets(game);
            var calls = FindDispatchCalls(assembly, targets, FindOverrides(targets, game, modules));
            // Coverage is an invariant of the rule table, not of each candidate: a mod that
            // never calls a target is perfectly normal, while a rule whose target has no
            // rewritable call site anywhere would silently do nothing.
            if (requireCoverage) RequireRewritableCalls(targets, calls);
            int changed = calls.Count(i => i.OpCode == OpCodes.Call);
            int methods = countMethods?.Invoke(targets) ?? ReportModCalls(assembly, changed);
            apply?.Invoke(targets, calls);
            if (requireCoverage && apply != null)
                InventoryLayout(game.MainModule, true, out _, out _);
            return new VirtualizationResult(methods, changed, typesChanged, fieldsChanged);
        }

        public const string InventoryModuleTypeName = "KFCommonUtilityLib.InventoryModuleData";
        public const string InventoryModuleFieldName = "kfModuleData";

        public static bool HasInventoryModuleLayout(Type inventoryData)
        {
            var type = inventoryData.Assembly.GetType(InventoryModuleTypeName, false);
            if (!inventoryData.IsSealed || type == null || !type.IsPublic || type.IsSealed || type.IsAbstract ||
                type.BaseType != typeof(object) || type.ContainsGenericParameters)
                return false;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
            var data = inventoryData.GetField(InventoryModuleFieldName, flags);
            var owner = type.GetField("Owner", flags);
            var original = type.GetField("InventoryData", flags);
            var ctor = type.GetConstructor(new[] { inventoryData });
            return data != null && !data.IsInitOnly && data.FieldType == type &&
                owner != null && owner.IsInitOnly && owner.FieldType == inventoryData.Assembly.GetType("ItemClass") &&
                original != null && original.IsInitOnly && original.FieldType == inventoryData &&
                ctor != null && ctor.GetParameters()[0].Name == "_inventoryData" &&
                type.GetConstructors().Length == 1;
        }

        private static void InventoryLayout(ModuleDefinition module, bool apply, out int types, out int fields)
        {
            var native = module.GetType("ItemInventoryData") ??
                throw new AssemblyPatchException("ItemInventoryData no longer exists; update the repair rules.");
            if (!native.IsClass || native.IsValueType || native.IsAbstract || native.HasGenericParameters)
                throw new AssemblyPatchException("ItemInventoryData is not a concrete non-generic class.");
            var itemClass = module.GetType("ItemClass") ?? throw new AssemblyPatchException("ItemClass no longer exists.");
            var itemGetter = native.Methods.Single(m => m.Name == "get_item" && !m.IsStatic &&
                m.Parameters.Count == 0 && m.ReturnType.FullName == "ItemClass");
            var type = module.GetType(InventoryModuleTypeName);
            types = native.IsSealed ? 0 : 1;
            fields = 0;
            if (type == null)
            {
                types++;
                fields += 2;
                if (apply)
                {
                    type = new TypeDefinition("KFCommonUtilityLib", "InventoryModuleData",
                        TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit, module.TypeSystem.Object);
                    module.Types.Add(type);
                    var owner = new FieldDefinition("Owner", FieldAttributes.Public | FieldAttributes.InitOnly, itemClass);
                    var original = new FieldDefinition("InventoryData", FieldAttributes.Public | FieldAttributes.InitOnly, native);
                    type.Fields.Add(owner);
                    type.Fields.Add(original);
                    var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                    ctor.Parameters.Add(new ParameterDefinition("_inventoryData", ParameterAttributes.None, native));
                    type.Methods.Add(ctor);
                    var il = ctor.Body.GetILProcessor();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, new MethodReference(".ctor", module.TypeSystem.Void, module.TypeSystem.Object) { HasThis = true });
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Stfld, original);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, itemGetter);
                    il.Emit(OpCodes.Stfld, owner);
                    il.Emit(OpCodes.Ret);
                }
            }
            else
            {
                var owner = type.Fields.SingleOrDefault(f => f.Name == "Owner");
                var original = type.Fields.SingleOrDefault(f => f.Name == "InventoryData");
                var ctor = type.Methods.SingleOrDefault(m => m.IsConstructor);
                if (!type.IsPublic || !type.IsClass || type.IsValueType || type.IsSealed || type.IsAbstract || type.HasGenericParameters ||
                    type.BaseType?.FullName != "System.Object" || type.BaseType.Scope != module.TypeSystem.Object.Scope ||
                    type.Fields.Count != 2 || type.Methods.Count != 1 ||
                    owner == null || !owner.IsPublic || owner.IsStatic || !owner.IsInitOnly || owner.FieldType.FullName != "ItemClass" || owner.FieldType.Scope != module ||
                    original == null || !original.IsPublic || original.IsStatic || !original.IsInitOnly || original.FieldType.FullName != native.FullName || original.FieldType.Scope != module ||
                    ctor == null || !ctor.IsPublic || ctor.IsStatic || ctor.Parameters.Count != 1 ||
                    ctor.Parameters[0].Name != "_inventoryData" || ctor.Parameters[0].ParameterType.FullName != native.FullName ||
                    !ctor.HasBody || ctor.Body.Instructions.Count != 10)
                    throw new AssemblyPatchException("Injected inventory module base has an incompatible layout.");
                var body = ctor.Body.Instructions;
                var opcodes = new[] { OpCodes.Ldarg_0, OpCodes.Call, OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Stfld,
                    OpCodes.Ldarg_0, OpCodes.Ldarg_1, OpCodes.Call, OpCodes.Stfld, OpCodes.Ret };
                if (!body.Select(i => i.OpCode).SequenceEqual(opcodes) ||
                    body[1].Operand is not MethodReference objectCtor || objectCtor.Name != ".ctor" ||
                    objectCtor.DeclaringType.FullName != "System.Object" || objectCtor.Parameters.Count != 0 ||
                    body[4].Operand != original || body[7].Operand != itemGetter || body[8].Operand != owner)
                    throw new AssemblyPatchException("Injected inventory module constructor has an incompatible body.");
            }
            var matches = native.Fields.Where(f => f.Name == InventoryModuleFieldName).ToArray();
            if (matches.Length > 1)
                throw new AssemblyPatchException("Duplicate inventory module storage fields.");
            if (matches.Length == 0)
            {
                fields++;
                if (apply) native.Fields.Add(new FieldDefinition(InventoryModuleFieldName, FieldAttributes.Public, type));
            }
            else if (!matches[0].IsPublic || matches[0].IsStatic || matches[0].IsInitOnly ||
                matches[0].FieldType.FullName != InventoryModuleTypeName || matches[0].FieldType.Scope != module)
                throw new AssemblyPatchException("Inventory module storage field has an incompatible layout.");
            if (apply) native.IsSealed = true;
        }

        private static int ReportModCalls(AssemblyDefinition mod, int changed)
        {
            RequireWritableMod(mod, changed);
            return 0;
        }

        private static void VirtualizeMethods(Dictionary<string, MethodDefinition> targets,
            List<Instruction> calls)
        {
            foreach (var method in targets.Values)
            {
                method.IsVirtual = true;
                method.IsNewSlot = true;
                method.IsFinal = false;
            }
            RewriteCallSites(targets, calls);
        }

        private static void RewriteCallSites(Dictionary<string, MethodDefinition> targets,
            List<Instruction> calls)
        {
            foreach (var call in calls) call.OpCode = OpCodes.Callvirt;
        }

        private static void RequireWritableMod(AssemblyDefinition mod, int changes)
        {
            if (changes != 0 && mod.Name.HasPublicKey)
                throw new AssemblyPatchException("Cannot rewrite signed mod without its signing key: " + mod.Name.FullName);
        }

        public static void Verify(AssemblyDefinition game) =>
            Verify(game, Array.Empty<AssemblyDefinition>());

        public static void Verify(AssemblyDefinition game, IEnumerable<AssemblyDefinition> modules)
        {
            if (Analyze(game, modules).Changed)
                throw new AssemblyPatchException("Game type flags, methods or dispatch calls are not compatible.");
        }

        public static void VerifyMod(AssemblyDefinition mod, AssemblyDefinition game) =>
            VerifyMod(mod, game, Array.Empty<AssemblyDefinition>());

        public static void VerifyMod(AssemblyDefinition mod, AssemblyDefinition game,
            IEnumerable<AssemblyDefinition> modules)
        {
            if (AnalyzeMod(mod, game, modules).Changed)
                throw new AssemblyPatchException("Mod dispatch calls are not compatible: " + mod.Name.Name);
        }

        private static bool NeedsFlags(MethodDefinition method) =>
            !method.IsVirtual || !method.IsNewSlot || method.IsFinal;

        private static Dictionary<string, MethodDefinition> ResolveTargets(AssemblyDefinition game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var result = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);
            foreach (var rule in Rules)
            {
                var type = game.MainModule.GetType(rule.Owner);
                // No return-type test: rule.Matches already pins the exact parameter list, so
                // the shape is fully identified without it. Virtualization never touches a
                // signature, so a non-void return is not a reason to refuse a target.
                var matches = type == null
                    ? Array.Empty<MethodDefinition>()
                    : type.Methods.Where(m => !m.IsStatic && !m.IsConstructor && !m.HasGenericParameters &&
                        rule.Matches(m)).ToArray();
                if (matches.Length != 1)
                    throw new AssemblyPatchException(rule.Owner + "." + rule.Name +
                        " resolved " + matches.Length + " times.");
                var method = matches[0];
                if (!method.HasBody || method.IsAbstract || method.IsPInvokeImpl || method.IsPrivate ||
                    method.Overrides.Count != 0)
                    throw new AssemblyPatchException(method.FullName + " has an unsupported implementation or visibility.");
                if (method.IsVirtual && !method.IsNewSlot && OverridesBaseSlot(method))
                    throw new AssemblyPatchException(method.FullName + " now reuses an existing base virtual slot; update the repair rules.");
                // Two rules must never claim the same method: that would hide a duplicate row
                // and double-count MethodsChanged.
                if (result.ContainsKey(method.FullName))
                    throw new AssemblyPatchException(method.FullName + " is claimed by more than one repair rule.");
                result.Add(method.FullName, method);
            }
            return result;
        }

        private static bool OverridesBaseSlot(MethodDefinition method)
        {
            var parent = method.DeclaringType.BaseType;
            while (parent != null && parent.FullName != "System.Object")
            {
                var definition = parent.Resolve();
                if (definition == null) throw new AssemblyPatchException("Cannot resolve base type " + parent.FullName);
                if (definition.Methods.Any(m => m.IsVirtual && m.Name == method.Name &&
                    m.ReturnType.FullName == method.ReturnType.FullName &&
                    m.Parameters.Select(p => p.ParameterType.FullName)
                        .SequenceEqual(method.Parameters.Select(p => p.ParameterType.FullName))))
                    return true;
                parent = definition.BaseType;
            }
            return false;
        }

        private static bool PotentialTarget(MethodReference method) =>
            Rules.Any(rule => rule.MatchesShape(method));

        private static bool SameAssembly(AssemblyDefinition a, AssemblyDefinition b) =>
            a.Name.FullName == b.Name.FullName && a.MainModule.Mvid == b.MainModule.Mvid;

        private static MethodDefinition ResolveTarget(MethodReference reference,
            Dictionary<string, MethodDefinition> targets)
        {
            if (!PotentialTarget(reference)) return null;
            var method = reference.Resolve();
            if (method == null)
                throw new AssemblyPatchException("Cannot resolve possible target " + reference.FullName);
            return targets.TryGetValue(method.FullName, out var target) &&
                SameAssembly(method.Module.Assembly, target.Module.Assembly) ? target : null;
        }

        // The bodies of a target's overrides are the one place the repair must not touch:
        // that is where an author decides between base.X() (a direct call) and X() (a
        // virtual call), and only the author knows which is meant. Both spellings are read
        // from metadata rather than inferred -- an override is a virtual method that reuses
        // an existing slot (IsNewSlot == false) with the target's name and signature.
        //
        // Scanning the modules as well as the game is required: an override lives in a
        // module assembly while the call sites the repair rewrites live in the game.
        private static HashSet<string> FindOverrides(Dictionary<string, MethodDefinition> targets,
            AssemblyDefinition game, IEnumerable<AssemblyDefinition> modules)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in Enumerate(game, modules))
            {
                if (assembly == null) continue;
                foreach (var type in AllTypes(assembly.MainModule.Types))
                foreach (var method in type.Methods)
                {
                    if (!method.IsVirtual || method.IsNewSlot || method.IsConstructor) continue;
                    foreach (var target in targets.Values)
                        if (method.Name == target.Name &&
                            method.ReturnType.FullName == target.ReturnType.FullName &&
                            method.Parameters.Select(p => p.ParameterType.FullName)
                                .SequenceEqual(target.Parameters.Select(p => p.ParameterType.FullName)))
                        { result.Add(method.FullName); break; }
                }
            }
            return result;
        }

        private static IEnumerable<AssemblyDefinition> Enumerate(AssemblyDefinition first,
            IEnumerable<AssemblyDefinition> rest)
        {
            if (first != null) yield return first;
            if (rest == null) yield break;
            foreach (var assembly in rest) if (assembly != null) yield return assembly;
        }

        private static List<Instruction> FindDispatchCalls(AssemblyDefinition assembly,
            Dictionary<string, MethodDefinition> targets, HashSet<string> overrides)
        {
            var result = new List<Instruction>();
            bool isGame = targets.Values.Any(target => SameAssembly(assembly, target.Module.Assembly));
            // An external call to a game method needs a MemberRef. Skip the IL of
            // libraries with no matching references, even on a cold scan.
            if (!isGame && !assembly.Modules.Any(module =>
                module.GetMemberReferences().OfType<MethodReference>().Any(PotentialTarget)))
                return result;
            foreach (var module in assembly.Modules)
            foreach (var type in AllTypes(module.Types))
            foreach (var caller in type.Methods)
            {
                if (!caller.HasBody) continue;
                foreach (var instruction in caller.Body.Instructions)
                {
                    var reference = instruction.Operand as MethodReference;
                    if (!PotentialTarget(reference)) continue;
                    var target = ResolveTarget(reference, targets);
                    if (target == null || instruction.OpCode == OpCodes.Ldtoken) continue;
                    if (instruction.OpCode == OpCodes.Callvirt)
                    {
                        result.Add(instruction);
                        continue;
                    }
                    if (instruction.OpCode != OpCodes.Call)
                        throw new AssemblyPatchException("Unsupported " + instruction.OpCode.Name +
                            " for " + target.FullName + " in " + caller.FullName);
                    // Skip only a call that sits inside one of this target's own overrides:
                    // there the author owns the base/this decision. Every other call site is
                    // the repair's responsibility, including calls made from a derived type.
                    if (overrides.Contains(caller.FullName)) continue;
                    result.Add(instruction);
                }
            }
            return result;
        }

        // A target needs at least one call site the repair would actually rewrite. A target
        // whose every call site sits inside an override body has nothing to dispatch through,
        // and listing it would only abort the whole repair.
        private static void RequireRewritableCalls(
            Dictionary<string, MethodDefinition> targets, List<Instruction> calls)
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instruction in calls)
                if (instruction.Operand is MethodReference reference)
                {
                    var target = ResolveTarget(reference, targets);
                    if (target != null) covered.Add(target.FullName);
                }
            foreach (var target in targets.Values)
                if (!covered.Contains(target.FullName))
                    throw new AssemblyPatchException(target.FullName + " has no rewritable call sites.");
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                yield return type;
                foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
            }
        }
    }
}



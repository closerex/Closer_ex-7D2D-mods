using AllowComboKeys;
using Patches = AllowComboKeys.Patches;
using HarmonyLib;
using InControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static int assertions;
    private static int cases;
    private static bool useHarmony;
    private static readonly MethodInfo valuePrefix = AccessTools.Method(typeof(Patches), "Prefix_KeyBindingSource_GetValue");
    private static readonly MethodInfo clearBindingsPrefix = AccessTools.Method(typeof(Patches), "Prefix_PlayerAction_ClearBindings");
    private static readonly MethodInfo resetBindingsPrefix = AccessTools.Method(typeof(Patches), "Prefix_PlayerAction_ResetBindings");
    private static readonly MethodInfo prefix = AccessTools.Method(typeof(Patches), "Prefix_InputManager_UpdatePlayerActionSets");
    private static readonly MethodInfo finalizer = AccessTools.Method(typeof(Patches), "Finalizer_InputManager_UpdatePlayerActionSets");
    private static readonly MethodInfo reset = AccessTools.Method(typeof(Patches), "Prefix_InputManager_ResetInternal");
    private static readonly FieldInfo arbField = AccessTools.Field(typeof(Patches), "comboArbitrator");
    private static readonly PropertyInfo keyboardProperty = typeof(InputManager).GetProperty("KeyboardProvider");
    private static readonly PropertyInfo boundToProperty = typeof(BindingSource).GetProperty("BoundTo", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo listenerField = AccessTools.Field(typeof(PlayerActionSet), "listenWithAction");
    private static readonly FieldInfo regularField = AccessTools.Field(typeof(PlayerAction), "regularBindings");
    private static readonly FieldInfo visibleField = AccessTools.Field(typeof(PlayerAction), "visibleBindings");
    private static readonly Type axisType = typeof(OneAxisInputControl);
    private static FieldInfo AxisField(string name) => AccessTools.Field(axisType, name);
    private static ComboBindingArbitrator<KeyBindingSource> Arbitrator => (ComboBindingArbitrator<KeyBindingSource>)arbField.GetValue(null);

    private static int Main(string[] args)
    {
        StreamWriter report = null;
        Harmony harmony = null;
        var hasPatches = false;
        try
        {
            var reportArg = args.FirstOrDefault(a => a.StartsWith("--report="));
            if (reportArg != null)
            {
                report = new StreamWriter(reportArg.Substring(9)) { AutoFlush = true };
                Console.SetOut(report);
            }
            Console.WriteLine("READY " + typeof(PlayerAction).Assembly.FullName);
            if (args.Contains("--wait")) Console.ReadLine();
            useHarmony = args.Contains("--harmony");
            if (useHarmony)
            {
                harmony = new Harmony("AllowComboKeys.OfflineRegression");
                hasPatches = true;
                // Resolve and install the shipped attribute declarations, not hand-picked target methods.
                harmony.CreateClassProcessor(typeof(Patches)).Patch();
                Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerAction), "ClearBindings")).Owners.Contains(harmony.Id), "ClearBindings entry not patched");
                Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(PlayerAction), "ResetBindings")).Owners.Contains(harmony.Id), "ResetBindings entry not patched");
                var setterPatches = Harmony.GetPatchInfo(boundToProperty.GetSetMethod(true));
                Check(setterPatches == null || !setterPatches.Owners.Contains(harmony.Id), "tiny ownership setter unexpectedly patched");
                Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(KeyBindingSource), "GetValue")).Owners.Contains(harmony.Id), "value attribute target not patched");
                var clearPatches = Harmony.GetPatchInfo(AccessTools.Method(typeof(InputManager), "ClearInputState"));
                Check(clearPatches == null || !clearPatches.Owners.Contains(harmony.Id), "native ClearInputState unexpectedly patched");
            }

            Run("promotion", Promotion);
            Run("last-up-and-retap", Retap);
            Run("hold-alone-still-consumes-new-subset", HoldAlone);
            Run("hold-was-not-last-physical-down", HoldFirst);
            Run("stored-order", StoredOrder);
            foreach (var order in new[] { "012", "021", "102", "120", "201", "210" })
                Run("chain-" + order, w => Chain(w, order));
            Run("simultaneous-no-invented-release", Simultaneous);
            Run("equal-bindings-identity", EqualBindings);
            Run("modifier-implication", Modifiers);
            foreach (var parentEnabled in new[] { false, true })
            foreach (var childEnabled in new[] { false, true })
            {
                Run("new-eligibility-" + parentEnabled + "-" + childEnabled,
                    w => Eligibility(w, parentEnabled, childEnabled, false));
                Run("locked-eligibility-" + parentEnabled + "-" + childEnabled,
                    w => Eligibility(w, parentEnabled, childEnabled, true));
            }
            Run("disabled-parent-no-new-consumption", DisabledParent);
            Run("disabled-action-lock", DisabledAction);
            Run("disabled-native-edge-gate", DisabledEdges);
            Run("listener-existing-lock", ListenerLock);
            Run("listener-nonblocking-siblings", w => ListenerSiblings(w, false));
            Run("listener-blocking-siblings", w => ListenerSiblings(w, true));
            Run("listener-finishes-first", w => ListenerFinishes(w, true));
            Run("listener-finishes-last", w => ListenerFinishes(w, false));
            Run("clear-native-state-not-arbitration", ClearNative);
            Run("owner-move-old-first", w => OwnerMove(w, false));
            Run("owner-move-new-first", w => OwnerMove(w, true));
            Run("same-frame-same-owner-reattach", SameOwnerReattach);
            Run("restore-default-active", w => RestoreDefault(w, false));
            Run("restore-default-suppressed", w => RestoreDefault(w, true));
            Run("removal-without-setter", RemovalWithoutSetter);
            Run("equal-replacement", EqualReplacement);
            Run("save-load", SaveLoad);
            Run("in-place-ordered-and-excludes-load", InPlaceLoad);
            Run("empty-binding", EmptyBinding);
            Run("unfiltered-presence", UnfilteredPresence);
            Run("unavailable-provider", Unavailable);
            Run("other-source-keeps-action-held", OtherSource);
            Run("real-second-key-binding-keeps-action-held", SecondKeySource);
            Run("same-action-subset-and-parent", SameActionParent);
            Run("raw-read-and-evaluation-scope", EvaluationScope);
            Console.WriteLine("{\"result\":\"PASS\",\"cases\":" + cases + ",\"assertions\":" + assertions +
                ",\"harmonyInterception\":" + JsonBool(useHarmony) + "}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            if (hasPatches) harmony.UnpatchSelf();
            report?.Dispose();
        }
    }

    private static void Run(string name, Action<Fixture> test)
    {
        using (var world = new Fixture(name)) test(world);
        cases++;
        Console.WriteLine("PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void State(BoundKey key, ComboBindingState expected)
    {
        Check(Arbitrator.GetState(key.Binding) == expected,
            key.Action.Name + " expected state " + expected + " got " + Arbitrator.GetState(key.Binding));
    }

    private static void Edges(BoundKey key, bool held, bool pressed, bool released)
    {
        Check(key.Action.IsPressed == held && key.Action.WasPressed == pressed && key.Action.WasReleased == released,
            key.Action.Name + " expected edges " + held + "," + pressed + "," + released +
            " got " + key.Action.IsPressed + "," + key.Action.WasPressed + "," + key.Action.WasReleased);
    }

    // Named debugger observation boundary. These are the real DLL edge getters after the bridge.
    private static void Evidence(string label, Fixture world, BoundKey key)
    {
        var state = Arbitrator.GetState(key.Binding);
        var physical = key.Binding.Control.IsPressed;
        var held = key.Action.IsPressed;
        var pressed = key.Action.WasPressed;
        var released = key.Action.WasReleased;
        Console.WriteLine("{\"label\":\"" + label + "\",\"tick\":" + world.TickNumber + ",\"state\":\"" + state +
            "\",\"physical\":" + JsonBool(physical) + ",\"held\":" + JsonBool(held) + ",\"pressed\":" + JsonBool(pressed) + ",\"released\":" + JsonBool(released) + "}");
    }

    private static void Promotion(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A); Edges(a, true, true, false);
        w.Tick(Key.A, Key.B);
        State(a, ComboBindingState.Suppressed); Edges(a, false, false, true); Edges(ab, true, true, false);
        Evidence("subset-released", w, a); Evidence("parent-pressed", w, ab);
        // Non-consuming getters: reading release does not erase the other action's press fact.
        Check(a.Action.WasReleased && ab.Action.WasPressed && a.Action.WasReleased && ab.Action.WasPressed, "edges consumed");
        w.Tick(Key.B); State(ab, ComboBindingState.Active); Edges(ab, true, false, false);
        Evidence("non-hold-up-parent-held", w, ab);
    }

    private static void Retap(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B);
        for (var i = 0; i < 3; i++)
        {
            w.Tick(Key.A); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Idle); Edges(ab, false, false, true);
            if (i == 0) Evidence("last-up-parent-released", w, ab);
            w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed); Edges(ab, true, true, false);
        }
        w.Tick(); State(a, ComboBindingState.Idle); State(ab, ComboBindingState.Idle);
        w.Tick(Key.A); Edges(a, true, true, false);
    }

    private static void HoldAlone(Fixture w)
    {
        var set = w.Set(); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Tick(Key.B); State(ab, ComboBindingState.Active);
        var b = w.Key(set, Key.B);
        w.Tick(Key.B); State(b, ComboBindingState.Suppressed); Edges(b, false, false, false);
        State(ab, ComboBindingState.Active); Check(!ab.Binding.Control.IsPressed, "parent unexpectedly full match");
    }

    private static void HoldFirst(Fixture w)
    {
        var ab = w.Key(w.Set(), Key.A, Key.B);
        w.Tick(Key.B); State(ab, ComboBindingState.Idle);
        w.Tick(Key.A, Key.B); State(ab, ComboBindingState.Active); Edges(ab, true, true, false);
        w.Tick(Key.B); State(ab, ComboBindingState.Active);
    }

    private static void StoredOrder(Fixture w)
    {
        var ba = w.Key(w.Set(), Key.B, Key.A);
        w.Tick(Key.A, Key.B); w.Tick(Key.A); State(ba, ComboBindingState.Active);
        w.Tick(Key.B); State(ba, ComboBindingState.Idle); Edges(ba, false, false, true);
    }

    private static void Chain(Fixture w, string order)
    {
        var set = w.Set(); var keys = new BoundKey[3];
        var requirements = new[] { new[] { Key.A }, new[] { Key.A, Key.B }, new[] { Key.A, Key.B, Key.C } };
        foreach (var digit in order) { var i = digit - '0'; keys[i] = w.Key(set, requirements[i]); }
        w.Tick(Key.A); State(keys[0], ComboBindingState.Active);
        w.Tick(Key.A, Key.B); State(keys[0], ComboBindingState.Suppressed); State(keys[1], ComboBindingState.Active);
        w.Tick(Key.A, Key.B, Key.C); State(keys[1], ComboBindingState.Suppressed); State(keys[2], ComboBindingState.Active);
        w.Tick(Key.A, Key.C); State(keys[1], ComboBindingState.Suppressed); State(keys[2], ComboBindingState.Active);
        w.Tick(Key.A, Key.B, Key.C); State(keys[1], ComboBindingState.Suppressed);
        w.Tick(Key.A, Key.B); State(keys[2], ComboBindingState.Idle); State(keys[1], ComboBindingState.Suppressed);
        w.Tick(); foreach (var key in keys) State(key, ComboBindingState.Idle);
        w.Tick(Key.A, Key.B); State(keys[1], ComboBindingState.Active); State(keys[0], ComboBindingState.Suppressed);
    }

    private static void Simultaneous(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); Edges(a, false, false, false); Edges(ab, true, true, false);
    }

    private static void EqualBindings(Fixture w)
    {
        var a = w.Key(w.Set(), Key.A); var other = w.Key(w.Set(), Key.A);
        var repeated = w.Key(w.Set(), Key.A, Key.A);
        Check(a.Binding.Equals(other.Binding) && !ReferenceEquals(a.Binding, other.Binding), "identity setup");
        w.Tick(Key.A); State(a, ComboBindingState.Active); State(other, ComboBindingState.Active); State(repeated, ComboBindingState.Active);
    }

    private static void Modifiers(Fixture w)
    {
        var set = w.Set(); var generic = w.Key(set, Key.Control, Key.B); var specific = w.Key(set, Key.LeftControl, Key.B);
        w.Tick(Key.LeftControl, Key.B); State(generic, ComboBindingState.Suppressed); State(specific, ComboBindingState.Active);
        w.Tick(Key.B); State(generic, ComboBindingState.Suppressed); State(specific, ComboBindingState.Active);
        w.Tick(); State(generic, ComboBindingState.Idle);
    }

    private static void Eligibility(Fixture w, bool parentEnabled, bool childEnabled, bool locked)
    {
        var childSet = w.Set(); var parentSet = w.Set(); var a = w.Key(childSet, Key.A); var ab = w.Key(parentSet, Key.A, Key.B);
        if (locked) w.Tick(Key.A, Key.B);
        childSet.Enabled = childEnabled; parentSet.Enabled = parentEnabled;
        w.Tick(Key.A, Key.B);
        State(a, locked || (childEnabled && parentEnabled) ? ComboBindingState.Suppressed : childEnabled ? ComboBindingState.Active : ComboBindingState.Idle);
        State(ab, locked || parentEnabled ? ComboBindingState.Active : ComboBindingState.Idle);
        if (locked)
        {
            w.Tick(Key.A); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Idle);
            childSet.Enabled = true; w.Tick(Key.A); State(a, ComboBindingState.Suppressed);
            w.Tick(); State(a, ComboBindingState.Idle);
        }
    }

    private static void DisabledParent(Fixture w)
    {
        var parentSet = w.Set(); var ab = w.Key(parentSet, Key.A, Key.B);
        w.Tick(Key.A, Key.B); parentSet.Enabled = false;
        var a = w.Key(w.Set(), Key.A);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Active); State(ab, ComboBindingState.Active);
        parentSet.Enabled = true; w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed); Edges(a, false, false, true);
    }

    private static void DisabledAction(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); a.Action.Enabled = false; ab.Action.Enabled = false;
        w.Tick(Key.A); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Idle);
        w.Tick(); State(a, ComboBindingState.Idle);
        w.Tick(Key.A); State(a, ComboBindingState.Idle); Edges(a, false, false, false);
        a.Action.Enabled = true; w.Tick(Key.A); State(a, ComboBindingState.Active); Edges(a, true, false, false);
    }

    private static void DisabledEdges(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); set.Enabled = false;
        w.Tick(Key.A); Edges(a, false, false, false);
        set.Enabled = true; w.Tick(Key.A); Edges(a, true, false, false);
        set.Enabled = false; w.Tick(); Edges(a, false, false, false);
        set.Enabled = true; w.Tick(); Edges(a, false, false, false);
        w.Tick(Key.A); Edges(a, true, true, false);
    }

    private static void ListenerLock(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); Listen(set, ab.Action);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Active); Edges(ab, false, false, true);
        w.Tick(Key.A); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Idle);
        w.Tick(); State(a, ComboBindingState.Idle);
        Listen(set, null); w.Tick(Key.A); State(a, ComboBindingState.Active);
    }

    private static void ListenerSiblings(Fixture w, bool block)
    {
        var set = w.Set(); var listener = w.Key(set, Key.Z); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        set.PreventInputWhileListeningForBinding = block; Listen(set, listener.Action);
        w.Tick(Key.Z, Key.A, Key.B); State(listener, ComboBindingState.Idle);
        State(a, block ? ComboBindingState.Idle : ComboBindingState.Suppressed);
        State(ab, block ? ComboBindingState.Idle : ComboBindingState.Active);
    }

    private static void ListenerFinishes(Fixture w, bool first)
    {
        var set = w.Set(); BoundKey listener = null;
        if (first) listener = w.Key(set, Key.Z);
        var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        if (!first) listener = w.Key(set, Key.Z);
        Listen(set, listener.Action);
        w.AfterCommit = action => { if (ReferenceEquals(action, listener.Action)) Listen(set, null); };
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Idle); State(ab, ComboBindingState.Idle);
        // The snapshot saw the blocking listener. If it finishes before siblings update,
        // Idle falls back to native input this tick: BOTH A and AB can press before next-tick arbitration.
        Edges(a, first, first, false); Edges(ab, first, first, false);
        w.AfterCommit = null; w.Tick(Key.A, Key.B);
        State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Active);
        Edges(a, false, false, first); Edges(ab, true, !first, false);
    }

    private static void ClearNative(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); InputManager.ClearInputState();
        State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Active); Edges(ab, false, false, false);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed); Edges(ab, true, false, false);
        w.Tick(Key.A); Edges(ab, false, false, true); State(a, ComboBindingState.Suppressed);
        reset.Invoke(null, null); State(a, ComboBindingState.Idle); State(ab, ComboBindingState.Idle);
    }

    private static void OwnerMove(Fixture w, bool newFirst)
    {
        var oldSet = w.Set(); var newSet = w.Set(); var a = w.Key(oldSet, Key.A); var ab = w.Key(oldSet, Key.A, Key.B);
        var nextAction = newSet.NewAction(); w.Tick(Key.A, Key.B); w.Tick(Key.A); State(a, ComboBindingState.Suppressed);
        a.Action.RemoveBinding(a.Binding);
        Check(nextAction.AddBinding(a.Binding), "new owner attach failed");
        // Ownership changes are reconciled at the next real input scan, not an API-wide immediate event.
        if (newFirst) w.Graph.Reverse();
        w.Tick(Key.A); State(a, ComboBindingState.Active); Check(nextAction.IsPressed, "new owner not evaluated");
    }

    private static void SameOwnerReattach(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Tick(Key.A); State(a, ComboBindingState.Suppressed);
        ClearBindings(a.Action);
        Check(a.Action.AddBinding(a.Binding), "same-owner reattach failed");
        State(a, ComboBindingState.Idle); w.Tick(Key.A); State(a, ComboBindingState.Active);
    }

    private static void RestoreDefault(Fixture w, bool suppressed)
    {
        var set = w.Set(); var action = set.NewAction(); var binding = new KeyBindingSource(Key.A);
        action.AddDefaultBinding(binding); var a = new BoundKey(action, binding);
        var ab = w.Key(set, Key.A, Key.B); var c = w.Key(set, Key.C); var cd = w.Key(set, Key.C, Key.D);
        if (suppressed) w.Tick(Key.A, Key.B, Key.C, Key.D);
        else w.Tick(Key.A, Key.C, Key.D);
        w.Tick(Key.A, Key.C);
        State(a, suppressed ? ComboBindingState.Suppressed : ComboBindingState.Active);
        State(c, ComboBindingState.Suppressed);
        ResetBindings(action); // Same method called by XUiC_BindingEntry.ResetToDefault.
        Check(ReferenceEquals(action.UnfilteredBindings[0], binding), "native defaults should reuse the stored identity");
        State(a, ComboBindingState.Idle); State(c, ComboBindingState.Suppressed);
        w.Tick(Key.A, Key.C); State(a, ComboBindingState.Active); State(c, ComboBindingState.Suppressed);
        Edges(a, true, suppressed, false); // ResetBindings does not itself reset native action edges.
    }

    private static void RemovalWithoutSetter(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed);
        ((List<BindingSource>)regularField.GetValue(a.Action)).Remove(a.Binding);
        Check(ReferenceEquals(boundToProperty.GetValue(a.Binding), a.Action), "setter unexpectedly called");
        w.SnapshotOnly(null); State(a, ComboBindingState.Idle); State(ab, ComboBindingState.Active);
    }

    private static void EqualReplacement(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Tick(Key.A);
        var other = new KeyBindingSource(Key.A); Check(a.Action.ReplaceBinding(a.Binding, other), "replace failed");
        Check(a.Action.HasBinding(a.Binding), "expected native value-equality HasBinding behavior");
        w.Tick(Key.A); State(a, ComboBindingState.Idle); Check(Arbitrator.GetState(other) == ComboBindingState.Active, "new binding inherited old lock");
    }

    private static void SaveLoad(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Tick(Key.A); var saved = set.SaveData(); set.LoadData(saved);
        w.Tick(Key.A); State(a, ComboBindingState.Idle);
        var replacement = (KeyBindingSource)a.Action.UnfilteredBindings[0];
        Check(!ReferenceEquals(replacement, a.Binding), "load should create new identity");
        Check(Arbitrator.GetState(replacement) == ComboBindingState.Active, "load inherited removed lock");
    }

    private static void Reload(KeyBindingSource binding, KeyCombo combo)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) new KeyBindingSource(combo).Save(writer);
            stream.Position = 0;
            using (var reader = new BinaryReader(stream)) binding.Load(reader, 2);
        }
    }

    private static void InPlaceLoad(Fixture w)
    {
        var key = w.Key(w.Set(), Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Tick(Key.B); State(key, ComboBindingState.Active);
        Reload(key.Binding, new KeyCombo(Key.B, Key.A)); w.Tick(Key.B); State(key, ComboBindingState.Idle);
        w.Tick(Key.A); State(key, ComboBindingState.Idle);
        w.Tick(Key.A, Key.B); w.Tick(Key.A); State(key, ComboBindingState.Active);
        Reload(key.Binding, new KeyCombo(Key.B, Key.A).AndNot(Key.C));
        w.Tick(Key.A); State(key, ComboBindingState.Idle);
        w.Tick(Key.A, Key.B, Key.C); State(key, ComboBindingState.Idle);
        w.Tick(Key.A, Key.B); State(key, ComboBindingState.Active);
        w.Tick(Key.A, Key.C); State(key, ComboBindingState.Active); // excludes do not cancel an established hold
    }

    private static void EmptyBinding(Fixture w)
    {
        var set = w.Set(); var empty = w.Key(set); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); State(empty, ComboBindingState.Idle);
        Reload(ab.Binding, KeyCombo.Empty); w.Tick(Key.B); State(ab, ComboBindingState.Idle); Edges(ab, false, false, true);
    }

    private static void UnfilteredPresence(Fixture w)
    {
        var ab = w.Key(w.Set(), Key.A, Key.B); w.Tick(Key.A, Key.B);
        ((List<BindingSource>)visibleField.GetValue(ab.Action)).Clear();
        Check(ab.Action.Bindings.Count == 0 && ab.Action.UnfilteredBindings.Count == 1, "presence setup");
        w.Tick(Key.B); State(ab, ComboBindingState.Active);
    }

    private static void Unavailable(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); w.Keyboard.Down.Clear();
        w.SnapshotOnly(null); State(a, ComboBindingState.Suppressed); State(ab, ComboBindingState.Active);
        Reload(ab.Binding, new KeyCombo(Key.B, Key.C));
        w.SnapshotOnly(null); State(ab, ComboBindingState.Idle); State(a, ComboBindingState.Suppressed);
        w.Tick(); State(a, ComboBindingState.Idle);
    }

    private static void OtherSource(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.OtherValues[a.Action] = 1f; w.Tick(Key.A); Edges(a, true, true, false);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed); Edges(a, true, false, false);
        w.OtherValues[a.Action] = 0f; w.Tick(Key.A, Key.B); Edges(a, false, false, true);
    }

    private static void SecondKeySource(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        var alternate = new KeyBindingSource(Key.C);
        Check(a.Action.AddBinding(alternate), "attach real second key source");
        w.Tick(Key.A, Key.C); Edges(a, true, true, false);
        w.Tick(Key.A, Key.B, Key.C); State(a, ComboBindingState.Suppressed); Edges(a, true, false, false);
        Check(Arbitrator.GetState(alternate) == ComboBindingState.Active, "unrelated second key suppressed");
        w.Tick(Key.A, Key.B); Edges(a, false, false, true);
    }

    private static void SameActionParent(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A);
        var parent = new KeyBindingSource(Key.A, Key.B);
        Check(a.Action.AddBinding(parent), "attach same-action parent");
        w.Tick(Key.A); Edges(a, true, true, false);
        w.Tick(Key.A, Key.B); State(a, ComboBindingState.Suppressed);
        Check(Arbitrator.GetState(parent) == ComboBindingState.Active, "same-action parent not active");
        Edges(a, true, false, false); // A binding released, but AB keeps the SAME action held.
    }

    private static void EvaluationScope(Fixture w)
    {
        var set = w.Set(); var a = w.Key(set, Key.A); var ab = w.Key(set, Key.A, Key.B);
        w.Tick(Key.A, Key.B); Check(a.Binding.GetValue(null) == 1f, "GetValue outside action evaluation was changed");
        prefix.Invoke(null, new object[] { w.Graph });
        try
        {
            Check(ReadValue(a.Binding) == 0f && a.Binding.GetState(null), "suppression must not alter raw key state");
            Check(ReadValue(ab.Binding) == 1f, "active parent value");
        }
        finally { finalizer.Invoke(null, new object[] { null }); }
        w.Tick(Key.B); Check(ab.Binding.GetValue(null) == 0f, "outside evaluation should use native full match");
    }

    private static string JsonBool(bool value) => value ? "true" : "false";

    private static float ReadValue(BindingSource binding)
    {
        if (useHarmony || !(binding is KeyBindingSource)) return binding.GetValue(null);
        var args = new object[] { binding, 0f };
        return (bool)valuePrefix.Invoke(null, args) ? binding.GetValue(null) : (float)args[1];
    }

    // Only --harmony verifies these native entry points automatically invoke the production prefix.
    private static void ClearBindings(PlayerAction action)
    {
        if (!useHarmony) clearBindingsPrefix.Invoke(null, new object[] { action });
        action.ClearBindings();
    }

    private static void ResetBindings(PlayerAction action)
    {
        if (!useHarmony) resetBindingsPrefix.Invoke(null, new object[] { action });
        action.ResetBindings();
    }

    private static void Listen(TestSet set, PlayerAction action) => listenerField.SetValue(set, action);

    private sealed class BoundKey
    {
        public readonly PlayerAction Action;
        public readonly KeyBindingSource Binding;
        public BoundKey(PlayerAction action, KeyBindingSource binding) { Action = action; Binding = binding; }
    }

    private sealed class TestSet : PlayerActionSet
    {
        private int sequence;
        public PlayerAction NewAction() => CreatePlayerAction("action-" + sequence++);
    }

    private sealed class Keyboard : IKeyboardProvider
    {
        public readonly HashSet<Key> Down = new HashSet<Key>();
        public void Setup() { }
        public void Reset() => Down.Clear();
        public void Update() { }
        public bool AnyKeyIsPressed() => Down.Count != 0;
        public string GetNameForKey(Key key) => key.ToString();
        public bool GetKeyIsPressed(Key key)
        {
            if (Down.Contains(key)) return true;
            switch (key)
            {
                case Key.Control: return Down.Contains(Key.LeftControl) || Down.Contains(Key.RightControl);
                case Key.Shift: return Down.Contains(Key.LeftShift) || Down.Contains(Key.RightShift);
                case Key.Alt: return Down.Contains(Key.LeftAlt) || Down.Contains(Key.RightAlt);
                case Key.Command: return Down.Contains(Key.LeftCommand) || Down.Contains(Key.RightCommand);
                default: return false;
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Keyboard Keyboard = new Keyboard();
        public readonly List<PlayerActionSet> Graph = new List<PlayerActionSet>();
        public readonly Dictionary<PlayerAction, float> OtherValues = new Dictionary<PlayerAction, float>();
        public Action<PlayerAction> AfterCommit;
        public ulong TickNumber;
        private readonly string name;
        public Fixture(string name)
        {
            this.name = name;
            reset.Invoke(null, null);
            keyboardProperty.SetValue(null, Keyboard);
        }
        public TestSet Set() { var set = new TestSet(); Graph.Add(set); return set; }
        public BoundKey Key(TestSet set, params Key[] keys)
        {
            var action = set.NewAction(); var binding = new KeyBindingSource(keys);
            Check(action.AddBinding(binding), "attach " + name);
            return new BoundKey(action, binding);
        }
        public void SnapshotOnly(IKeyboardProvider keyboard)
        {
            keyboardProperty.SetValue(null, keyboard);
            try { prefix.Invoke(null, new object[] { Graph }); }
            finally { finalizer.Invoke(null, new object[] { null }); keyboardProperty.SetValue(null, Keyboard); }
        }
        public void Tick(params Key[] keys)
        {
            Keyboard.Down.Clear(); foreach (var key in keys) Keyboard.Down.Add(key);
            TickNumber++;
            prefix.Invoke(null, new object[] { Graph });
            try
            {
                foreach (var set in Graph)
                foreach (var action in set.Actions)
                {
                    var blocked = action.IsListeningForBinding || (set.IsListeningForBinding && set.PreventInputWhileListeningForBinding);
                    var bindings = (List<BindingSource>)regularField.GetValue(action);
                    var count = bindings.Count;
                    for (var i = count - 1; i >= 0; i--)
                    {
                        var binding = bindings[i];
                        if (!ReferenceEquals(boundToProperty.GetValue(binding), action))
                        {
                            bindings.RemoveAt(i); ((List<BindingSource>)visibleField.GetValue(action)).Remove(binding);
                        }
                        else if (!blocked) action.UpdateWithValue(ReadValue(binding), TickNumber, 0.016f);
                    }
                    if (!blocked && OtherValues.TryGetValue(action, out var other)) action.UpdateWithValue(other, TickNumber, 0.016f);
                    if (blocked || count == 0) action.UpdateWithValue(0f, TickNumber, 0.016f);
                    CommitWithoutUnityClock(action);
                    AxisField("ownerEnabled").SetValue(action, set.Enabled);
                    AfterCommit?.Invoke(action);
                }
            }
            finally { finalizer.Invoke(null, new object[] { null }); }
        }
        public void Dispose()
        {
            foreach (var set in Graph) set.Destroy();
            reset.Invoke(null, null);
            keyboardProperty.SetValue(null, null);
        }
    }

    // Keep native PrepareForUpdate/UpdateWithValue and edge getters. Only bridge Commit's
    // state transfer/clear branch; Unity's native realtime clock and repeat timing are not tested.
    private static void CommitWithoutUnityClock(OneAxisInputControl action)
    {
        var next = AxisField("nextState").GetValue(action);
        AxisField("pendingCommit").SetValue(action, false);
        AxisField("thisState").SetValue(action, next);
        if ((bool)AxisField("clearInputState").GetValue(action))
        {
            AxisField("lastState").SetValue(action, next);
            AxisField("clearInputState").SetValue(action, false);
        }
    }
}

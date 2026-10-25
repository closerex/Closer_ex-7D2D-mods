using HarmonyLib;
using InControl;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace AllowComboKeys
{
    public class AllowComboKeysInit : IModApi
    {
        private static bool inited = false;
        public void InitMod(Mod _modInstance)
        {
            if (inited)
                return;
            inited = true;
            Log.Out(" Loading Patch: " + GetType());
            var harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch]
    public static class Patches
    {
        private static readonly ComboBindingArbitrator<KeyBindingSource> comboArbitrator = new(KeyRequirementImplies);
        private static readonly List<ComboBindingSnapshot<KeyBindingSource>> comboSnapshots = new();
        private static readonly KeyBindingSnapshotBuilder snapshotBuilder = new();

        private static bool isEvaluatingPlayerActions;

        [HarmonyPatch(typeof(KeyBindingSourceListener), nameof(KeyBindingSourceListener.Listen))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_KeyBindingSourceListener_Listen(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            var fld_phase = AccessTools.Field(typeof(KeyBindingSourceListener), "detectPhase");

            for (int i = 0; i < codes.Count - 1; ++i)
            {
                if (codes[i].LoadsField(fld_phase) && codes[i + 1].opcode == OpCodes.Ldc_I4_1)
                {
                    var lbl_original = codes[i + 2].operand;
                    var lbl_new = generator.DefineLabel();
                    codes[i + 2].opcode = OpCodes.Beq_S;
                    codes[i + 2].operand = lbl_new;
                    codes[i + 3].WithLabels(lbl_new);
                    codes.InsertRange(i + 3, new[]
                    {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(KeyBindingSourceListener), "detectFound"),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, fld_phase),
                    CodeInstruction.CallClosure<Func<KeyCombo, KeyCombo, int, bool>>((input, cached, phase) =>
                    {
                        return phase == 2 && input.IncludeCount >= cached.IncludeCount;
                    }),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl_original),
                });
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(KeyCombo), nameof(KeyCombo.Detect))]
        [HarmonyPrefix]
        private static bool Prefix_KeyCombo_Detect(bool modifiersAsKeys, ref KeyCombo __result)
        {
            KeyCombo empty = KeyCombo.Empty;
            IKeyboardProvider keyboardProvider = InputManager.KeyboardProvider;
            if (keyboardProvider == null)
            {
                __result = empty;
                return false;
            }
            if (modifiersAsKeys)
            {
                for (Key key = Key.LeftShift; key <= Key.RightControl; key++)
                {
                    if (keyboardProvider.GetKeyIsPressed(key))
                    {
                        empty.AddInclude(key);
                        if (key == Key.LeftControl && keyboardProvider.GetKeyIsPressed(Key.RightAlt))
                        {
                            empty.AddInclude(Key.RightAlt);
                        }
                    }
                }
            }
            else
            {
                for (Key key2 = Key.Shift; key2 <= Key.Control; key2++)
                {
                    if (keyboardProvider.GetKeyIsPressed(key2))
                    {
                        empty.AddInclude(key2);
                    }
                }
            }
            for (Key key3 = Key.Escape; key3 <= Key.QuestionMark; key3++)
            {
                if (keyboardProvider.GetKeyIsPressed(key3))
                {
                    empty.AddInclude(key3);
                }
            }
            __result = empty;
            return false;
        }

        [HarmonyPatch(typeof(InputManager), "UpdatePlayerActionSets")]
        [HarmonyPrefix]
        private static void Prefix_InputManager_UpdatePlayerActionSets(List<PlayerActionSet> ___playerActionSets)
        {
            if (isEvaluatingPlayerActions)
            {
                throw new InvalidOperationException("Combo action evaluation was entered recursively.");
            }

            if (snapshotBuilder.Build(___playerActionSets, InputManager.KeyboardProvider, comboArbitrator, comboSnapshots))
            {
                comboArbitrator.Update(comboSnapshots);
            }
            isEvaluatingPlayerActions = true;
        }

        [HarmonyPatch(typeof(InputManager), "UpdatePlayerActionSets")]
        [HarmonyFinalizer]
        private static Exception Finalizer_InputManager_UpdatePlayerActionSets(Exception __exception)
        {
            isEvaluatingPlayerActions = false;
            return __exception;
        }

        [HarmonyPatch(typeof(KeyBindingSource), nameof(KeyBindingSource.GetValue))]
        [HarmonyPrefix]
        private static bool Prefix_KeyBindingSource_GetValue(KeyBindingSource __instance, ref float __result)
        {
            if (!isEvaluatingPlayerActions)
            {
                return true;
            }

            switch (comboArbitrator.GetState(__instance))
            {
                case ComboBindingState.Active:
                    __result = 1f;
                    return false;
                case ComboBindingState.Suppressed:
                    __result = 0f;
                    return false;
                default:
                    return true;
            }
        }

        // Real clear/default-reset flows can reuse the same default binding references.
        // Observe these entry points, not the tiny BoundTo setter or every mutation API.
        [HarmonyPatch(typeof(PlayerAction), nameof(PlayerAction.ClearBindings))]
        [HarmonyPrefix]
        private static void Prefix_PlayerAction_ClearBindings(PlayerAction __instance)
        {
            ForgetActionBindings(__instance);
        }

        [HarmonyPatch(typeof(PlayerAction), nameof(PlayerAction.ResetBindings))]
        [HarmonyPrefix]
        private static void Prefix_PlayerAction_ResetBindings(PlayerAction __instance)
        {
            ForgetActionBindings(__instance);
        }

        private static void ForgetActionBindings(PlayerAction action)
        {
            foreach (var source in action.UnfilteredBindings)
            {
                if (source is KeyBindingSource binding)
                    snapshotBuilder.ForgetBinding(binding, comboArbitrator);
            }
        }

        // Native ClearInputState still clears action edges, but must not clear our physical
        // hold/suppression sessions. Only a real manager reset clears the whole graph.
        [HarmonyPatch(typeof(InputManager), "ResetInternal")]
        [HarmonyPrefix]
        private static void Prefix_InputManager_ResetInternal()
        {
            ClearComboState();
        }

        private static bool KeyRequirementImplies(int specificValue, int generalValue)
        {
            if (specificValue == generalValue)
            {
                return true;
            }

            var specific = (Key)specificValue;
            switch ((Key)generalValue)
            {
                case Key.Shift:
                    return specific == Key.LeftShift || specific == Key.RightShift;
                case Key.Alt:
                    return specific == Key.LeftAlt || specific == Key.RightAlt;
                case Key.Command:
                    return specific == Key.LeftCommand || specific == Key.RightCommand;
                case Key.Control:
                    return specific == Key.LeftControl || specific == Key.RightControl;
                default:
                    return false;
            }
        }

        private static void ClearComboState()
        {
            isEvaluatingPlayerActions = false;
            comboArbitrator.Clear();
            comboSnapshots.Clear();
            snapshotBuilder.Clear();
        }
    }
}

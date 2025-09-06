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
    }
}

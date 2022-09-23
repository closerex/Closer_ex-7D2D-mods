using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Platform.EOS;

namespace NetConnectionSimpleLockfree.Harmony
{
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(NetworkClientLiteNetLib), "OnConnectedToServer")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnConnectedToServer_NetworkClientLiteNetLib(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(NetConnectionSimpleLockfree.NetConnectionSimple), new Type[] { typeof(int), typeof(ClientInfo), typeof(INetworkClient), typeof(string), typeof(int), typeof(int) });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(NetworkServerLiteNetLib), "OnPlayerConnected")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnPlayerConnected_NetworkServerLiteNetLib(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj && codes[i + 1].opcode == OpCodes.Stelem_Ref)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(NetConnectionSimpleLockfree.NetConnectionSimple), new Type[] { typeof(int), typeof(ClientInfo), typeof(INetworkClient), typeof(string), typeof(int), typeof(int) });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(NetworkClientEos), "OnConnectedToServer")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnConnectedToServer_NetworkClientEos(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(NetConnectionSimpleLockfree.NetConnectionSimple), new Type[] { typeof(int), typeof(ClientInfo), typeof(INetworkClient), typeof(string), typeof(int), typeof(int) });
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(NetworkServerEos), "OnPlayerConnected")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_OnPlayerConnected_NetworkServerEos(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Newobj && codes[i + 1].opcode == OpCodes.Stelem_Ref)
                {
                    codes[i].operand = AccessTools.Constructor(typeof(NetConnectionSimpleLockfree.NetConnectionSimple), new Type[] { typeof(int), typeof(ClientInfo), typeof(INetworkClient), typeof(string), typeof(int), typeof(int) });
                    break;
                }
            }

            return codes;
        }
    }
}

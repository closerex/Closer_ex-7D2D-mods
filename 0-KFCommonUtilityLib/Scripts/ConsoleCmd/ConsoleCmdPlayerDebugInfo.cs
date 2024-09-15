using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KFCommonUtilityLib.Scripts.ConsoleCmd
{
    public class ConsoleCmdPlayerDebugInfo : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            RenderTexture playerCamRT = player.playerCamera.targetTexture;
            if (playerCamRT != null)
                SaveTextureToFileUtility.SaveTextureToFile(playerCamRT, Application.dataPath + playerCamRT.name, playerCamRT.width, playerCamRT.height, SaveTextureToFileUtility.SaveTextureFileFormat.PNG, 95, true, (bool res) => Log.Out(res ? $"player camera rendertexture saved to {Application.dataPath}" : "failed to save player camera render texture!"));
            RenderTexture weaponCamRT = player.weaponCamera.targetTexture;
            if (weaponCamRT != null)
                SaveTextureToFileUtility.SaveTextureToFile(weaponCamRT, Application.dataPath + weaponCamRT.name, weaponCamRT.width, weaponCamRT.height, SaveTextureToFileUtility.SaveTextureFileFormat.PNG, 95, true, (bool res) => Log.Out(res ? $"weapon camera rendertexture saved to {Application.dataPath}" : "failed to save weapon camera render texture!"));
            RenderTexture finalCamRT = player.finalCamera.targetTexture;
            if (finalCamRT != null)
                SaveTextureToFileUtility.SaveTextureToFile(finalCamRT, Application.dataPath + finalCamRT.name, finalCamRT.width, finalCamRT.height, SaveTextureToFileUtility.SaveTextureFileFormat.PNG, 95, true, (bool res) => Log.Out(res ? $"final camera rendertexture saved to {Application.dataPath}" : "failed to save final camera render texture!"));
            Renderer[] renderers = player.gameObject.GetComponentsInChildren<Renderer>();
            Log.Out($"renderers layers: \n{string.Join("\n", renderers.Select(r => r.gameObject.name + " layer:" + r.gameObject.layer))})");
            Log.Out($"player transform values: {player.transform.name} {player.transform.position}/{player.transform.eulerAngles}");
            Log.Out($"weapon camera transform values: {player.weaponCamera.transform.parent.name}/{player.weaponCamera.gameObject.name} {player.weaponCamera.transform.localPosition}/{player.weaponCamera.transform.localEulerAngles} viewport {player.weaponCamera.rect} render layers {player.weaponCamera.cullingMask} fov {player.weaponCamera.fieldOfView}");
            Log.Out($"player camera transform values: {player.playerCamera.transform.parent.name}/{player.playerCamera.gameObject.name} {player.playerCamera.transform.localPosition}/{player.playerCamera.transform.localEulerAngles} viewport {player.playerCamera.rect} render layers {player.playerCamera.cullingMask} fov {player.playerCamera.fieldOfView}");
            Log.Out($"final camera transform values: {player.finalCamera.transform.parent.name}/{player.finalCamera.gameObject.name} {player.finalCamera.transform.localPosition}/{player.finalCamera.transform.localEulerAngles} viewport {player.finalCamera.rect} render layers {player.finalCamera.cullingMask} fov {player.finalCamera.fieldOfView}");
            Log.Out($"vp components list:\n{string.Join("\n", player.RootTransform.GetComponentsInChildren<vp_Component>().Select(c => c.GetType().Name + " on " + c.transform.name))}");
            foreach (var animator in player.RootTransform.GetComponentsInChildren<Animator>())
            {
                Log.Out($"animator transform {animator.name} values: {animator.transform.localPosition}/{animator.transform.localEulerAngles}");
            }
            Log.Out("PRINTING PLAYER HIERARCHY:");
            Log.Out(PrintTransform(player.RootTransform));

            Transform fpsArm = ((AvatarLocalPlayerController)player.emodel.avatarController).FPSArms.animator.transform;
            Log.Out($"FPS ARM:\nparent {fpsArm.parent.name}\n{PrintTransform(fpsArm)}");
        }

        private static string PrintTransform(Transform parent, string str = "", int indent = 0)
        {
            str += "".PadLeft(indent * 4) + $"{parent.name}/pos:{parent.transform.localPosition}/rot:{parent.localEulerAngles}" + "\n";
            indent++;
            foreach (Transform child in parent)
            {
                str = PrintTransform(child, str, indent);
            }
            return str;
        }

        public override string[] getCommands()
        {
            return new string[] { "printpinfo" };
        }

        public override string getDescription()
        {
            return "print player debug info.";
        }
    }
}

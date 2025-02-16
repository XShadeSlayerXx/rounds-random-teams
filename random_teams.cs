using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using RWF;
using RWF.UI;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;

namespace random_teams;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class RandomTeams : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        //Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        var harmony = new Harmony("ShadeSlayer.RandomTeams.Patch");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(PrivateRoomCharacterSelectionInstance), "RPCO_SelectFace")]
    class RPC_Hijack_Patch
    {
        static bool Prefix(ref PrivateRoomCharacterSelectionInstance __instance, int faceID, int eyeID)
        {
            Logger.LogInfo("Recieved a RPCO_SelectFace, with faceID: " + faceID + ", colorID: " + eyeID);
            //faceID is uniqueID, eyeID is colorID
            if (faceID > -1) { return true; } //don't intercept the call if the faceID is valid
            Logger.LogInfo("--got past the negative faceID check");
            //var realActor = -(faceID + 1);
            if (__instance.currentPlayer.uniqueID != faceID) return false; //discard, not meant for us
            Logger.LogInfo("----passed the actorID check");
            Logger.LogInfo("----NetAct: " + PhotonNetwork.LocalPlayer.ActorNumber + ", _instAct: " + __instance.currentPlayer.actorID + ", passedID: " + faceID);

            ChangeTeam_Override(ref __instance, eyeID);
            return false;
        }
    }

    public static void ChangeTeam_Override(ref PrivateRoomCharacterSelectionInstance instance, int newColorID)
    {
        //__instance.lastChangedTeams = Time.realtimeSinceStartup;
        Traverse.Create(instance).Field("lastChangedTeams").SetValue(Time.realtimeSinceStartup);

        //__instance.colorID = newColorID;
        Traverse.Create(instance).Field("_colorID").SetValue(newColorID);

        LobbyCharacter character = instance.currentPlayer;
        character.colorID = newColorID;
        LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();
        characters[character.localID] = character;
        PhotonNetwork.LocalPlayer.SetProperty("players", characters);

        instance.UpdateFaceColors();
    }

    [HarmonyPatch(typeof(PrivateRoomCharacterSelectionInstance), nameof(Update))]
    class CharacterSelect_Update_Patch
    {
        static void Postfix(ref PrivateRoomCharacterSelectionInstance __instance)
        {
            //same checks as the base method
            //RWF.PrivateRoomHandler is private, so getting it's .instance is kind of a pain. hopefully it's not required
            //if (RWF.PrivateRoomHandler.instance == null || PhotonNetwork.CurrentRoom == null || __instance.currentPlayer == null)
            if (PhotonNetwork.CurrentRoom == null || __instance.currentPlayer == null || !__instance.currentPlayer.IsMine || !PhotonNetwork.IsMasterClient)
            {
                return;
            }
            int zero = 0;
            Vector2 v_zero = new(0, 0);

            //if (pressed a numrow button)
            if (
                Input.GetKeyDown(KeyCode.Alpha1) ||
                Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) ||
                Input.GetKeyDown(KeyCode.Alpha4)
               )
            {
                PhotonView view = __instance.gameObject.GetComponent<PhotonView>();

                //we're the host/master client
                LobbyCharacter[] characters = PhotonNetwork.LocalPlayer.GetProperty<LobbyCharacter[]>("players").Where(p => p != null).ToArray();

                int playersPerTeam = 2; //set to the number that was pressed. initialize it to 2 first.
                if (Input.GetKeyDown(KeyCode.Alpha1))      { playersPerTeam = 1; }
                else if (Input.GetKeyDown(KeyCode.Alpha2)) { playersPerTeam = 2; }
                else if (Input.GetKeyDown(KeyCode.Alpha3)) { playersPerTeam = 3; }
                else if (Input.GetKeyDown(KeyCode.Alpha4)) { playersPerTeam = 4; }

                //int numCharacters = characters.Length; //necessary if we want # of teams instead
                //int maxColors = RWFMod.MaxColorsHardLimit;
                int counter = -1;
                int color = 0;
                foreach (var character in characters.OrderBy(_ => UnityEngine.Random.Range(0, 1)))
                {
                    if (counter++ == playersPerTeam)
                    {
                        counter = 0;
                        if (color++ == RWFMod.MaxColorsHardLimit)
                        {
                            color = 0;
                        }
                    }
                    character.colorID = color;
                    Logger.LogInfo("Randomized " + character.actorID + " to: " + character.colorID);
                    object[] test = { character.uniqueID, character.colorID, v_zero, zero, v_zero, zero, v_zero, zero, v_zero };
                    view.RPC("RPCO_SelectFace", RpcTarget.All, test);
                }

                PhotonNetwork.LocalPlayer.SetProperty("players", characters);
                //__instance.UpdateFaceColors();
            }

            //randomize all players for testing purposes
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                PhotonView view = __instance.gameObject.GetComponent<PhotonView>();
                LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();

                foreach (var character in characters)
                {
                    int random_color = UnityEngine.Random.RandomRangeInt(0, RWFMod.MaxColorsHardLimit);
                    character.colorID = random_color;
                    Logger.LogWarning("Randomized " + character.actorID + " to: " + random_color);
                    object[] test = { character.uniqueID, random_color, v_zero, zero, v_zero, zero, v_zero, zero, v_zero };
                    view.RPC("RPCO_SelectFace", RpcTarget.All, test);

                }

                PhotonNetwork.LocalPlayer.SetProperty("players", characters);
                //view.RPC("RPCA_ChangeTeam", RpcTarget.All, -1);
                //__instance.UpdateFaceColors();
            }
        }
    }
}
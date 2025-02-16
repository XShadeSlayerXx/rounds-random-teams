using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using RWF;
using RWF.UI;
using UnboundLib.Extensions;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;
using RWF.Patches;

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

    //[HarmonyPatch(typeof(PrivateRoomCharacterSelectionInstance), "RPCA_ChangeTeam")]
    //class RPC_Hijack_Path
    //{
    //    static bool Prefix(ref PrivateRoomCharacterSelectionInstance __instance, int newColorID)
    //    {
    //        if (newColorID < 0)
    //        {
    //            __instance.UpdateFaceColors();
    //            return false;
    //        }
    //        return true;
    //    }
    //}

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
            //var tmp_actor = PhotonNetwork.LocalPlayer.ActorNumber;
            //__instance.currentPlayer.actorID = PhotonNetwork.LocalPlayer.ActorNumber;
            //PhotonView view = __instance.gameObject.GetComponent<PhotonView>();
            //view.RPC("RPCA_ChangeTeam", RpcTarget.All, eyeID);

            ChangeTeam_Override(ref __instance, faceID, eyeID);
            //ChangeTeam_Override(faceID, eyeID);
            //if (__instance.currentPlayer.uniqueID == faceID)
            //{

            //}

            //__instance.UpdateFaceColors();
            return false;
        }
    }

    //[PunRPC]
    //public static void ChangeTeam_Override(int uniqueID, int newColorID)
    //{
    //    //Logger.LogInfo("Recieved a RPCO_ChangeTeam, with faceID: " + uniqueID + ", colorID: " + newColorID);
    //    LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();
    //    LobbyCharacter who = characters.Where(p => p.uniqueID == uniqueID).FirstOrDefault();
    //    who.colorID = newColorID;
    //    characters[who.localID] = who;
    //    PhotonNetwork.LocalPlayer.SetProperty("players", characters);
    //}
    //[PunRPC]
    //public void RPCNew_ChangeTeam(int uniqueID, int newColorID)
    //{
    //    Logger.LogInfo("Recieved a RPCO_ChangeTeam, with faceID: " + uniqueID + ", colorID: " + newColorID);
    //    LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();
    //    LobbyCharacter who = characters.Where(p => p.uniqueID == uniqueID).FirstOrDefault();
    //    who.colorID = newColorID;
    //    characters[who.localID] = who;
    //    PhotonNetwork.LocalPlayer.SetProperty("players", characters);
    //}

    //[PunRPC]
    public static void ChangeTeam_Override(ref PrivateRoomCharacterSelectionInstance instance, int uniqueID, int newColorID)
    {
        //if (uniqueID != __instance.uniqueID) return; //not directed at us
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
                    //object[] test = { character.uniqueID, character.colorID };
                    object[] test = { character.uniqueID, character.colorID, v_zero, zero, v_zero, zero, v_zero, zero, v_zero };
                    //traverse.Property("view").Method("RPC", nameof(PrivateRoomInstance_Extensions.ChangeTeamRPC), RpcTarget.All, test);
                    //view.RPC(nameof(RPCNew_ChangeTeam), RpcTarget.All, test);
                    view.RPC("RPCO_SelectFace", RpcTarget.All, test);
                }

                PhotonNetwork.LocalPlayer.SetProperty("players", characters);
                //__instance.UpdateFaceColors();
            }

            //randomize all players for testing purposes
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                PhotonView view = __instance.gameObject.GetComponent<PhotonView>();
                //LobbyCharacter[] characters = PhotonNetwork.LocalPlayer.GetProperty<LobbyCharacter[]>("players").Where(p => p != null).ToArray();
                LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();
                //LobbyCharacter[] characters = PhotonNetwork.LocalPlayer.GetProperty<LobbyCharacter[]>("players");

                foreach (var character in characters)
                {
                    //if (character == null) continue;
                    int random_color = UnityEngine.Random.RandomRangeInt(0, RWFMod.MaxColorsHardLimit);
                    character.colorID = random_color;
                    Logger.LogWarning("Randomized " + character.actorID + " to: " + random_color);
                    //int converted_actorID = -character.actorID - 1;
                    //object[] test = { character.uniqueID, random_color };
                    object[] test = { character.uniqueID, random_color, v_zero, zero, v_zero, zero, v_zero, zero, v_zero };
                    //view.RPC(nameof(RPCNew_ChangeTeam), RpcTarget.All, test);
                    view.RPC("RPCO_SelectFace", RpcTarget.All, test);
                    //NetworkingManager.RPC(typeof(Unbound_RPC_test), "ChangeToColorRequest", character.uniqueID, random_color);

                }

                PhotonNetwork.LocalPlayer.SetProperty("players", characters);
                //view.RPC("RPCA_ChangeTeam", RpcTarget.All, -1);
                //__instance.UpdateFaceColors();
            }
            ////need to cause a nullref to regenerate stuff for my debugger >:(
            //else if (Input.GetKeyDown(KeyCode.Alpha9))
            //{
            //    PhotonView view = __instance.gameObject.GetComponent<PhotonView>();
            //    //no check here on purpose for the nullref
            //    LobbyCharacter[] characters = PhotonNetwork.LocalPlayer.GetProperty<LobbyCharacter[]>("players");
            //    foreach (var character in characters)
            //    {
            //        character.colorID = UnityEngine.Random.RandomRangeInt(0, RWFMod.MaxColorsHardLimit);
            //        Logger.LogInfo("Randomized " + character.actorID + " to: " + character.colorID);

            //        object[] test = { character.uniqueID, character.colorID, v_zero, zero, v_zero, zero, v_zero, zero, v_zero };
            //        //view.RPC(nameof(RPCNew_ChangeTeam), RpcTarget.All, test);
            //        view.RPC("RPCO_SelectFace", RpcTarget.All, test);
            //    }
            //    var nullref = characters[999];
            //}
        }
    }
}

//public static class Unbound_RPC_test
//{
//    [UnboundLib.Networking.UnboundRPC]
//    public static void ChangeToColorRequest(this PrivateRoomCharacterSelectionInstance instance, int uniqueID, int colorID)
//    {
//        if (instance.uniqueID != uniqueID) { return; }
//        PhotonView view = instance.gameObject.GetComponent<PhotonView>();
//        view.RPC("RPCA_ChangeTeam", RpcTarget.All, colorID);
//    }
//}
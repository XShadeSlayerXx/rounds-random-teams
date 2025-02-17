using System.Collections.Generic;
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
        var harmony = new Harmony("ShadeSlayer.RandomTeams.Patch");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(PrivateRoomCharacterSelectionInstance), nameof(Update))]
    class CharacterSelect_Update_Patch
    {
        static void Postfix(ref PrivateRoomCharacterSelectionInstance __instance)
        {
            //same checks as the base method
            if (PrivateRoomHandler.instance == null || PhotonNetwork.CurrentRoom == null || __instance.currentPlayer == null || !__instance.currentPlayer.IsMine || !PhotonNetwork.IsMasterClient)
            {
                return;
            }

            //if (pressed a numrow button)
            if (
                Input.GetKeyDown(KeyCode.Alpha1) ||
                Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) ||
                Input.GetKeyDown(KeyCode.Alpha4) ||
                Input.GetKeyDown(KeyCode.Alpha5) ||
                Input.GetKeyDown(KeyCode.Alpha6) ||
                Input.GetKeyDown(KeyCode.Alpha7) ||
                Input.GetKeyDown(KeyCode.Alpha8) ||
                Input.GetKeyDown(KeyCode.Alpha9)
               )
            {
                //we're the host/master client
                LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();

                int playersPerTeam = 2; //set to the number that was pressed. initialize it to 2 first.
                if (Input.GetKeyDown(KeyCode.Alpha1))      { playersPerTeam = 1; }
                else if (Input.GetKeyDown(KeyCode.Alpha2)) { playersPerTeam = 2; }
                else if (Input.GetKeyDown(KeyCode.Alpha3)) { playersPerTeam = 3; }
                else if (Input.GetKeyDown(KeyCode.Alpha4)) { playersPerTeam = 4; }
                else if (Input.GetKeyDown(KeyCode.Alpha5)) { playersPerTeam = 5; }
                else if (Input.GetKeyDown(KeyCode.Alpha6)) { playersPerTeam = 6; }
                else if (Input.GetKeyDown(KeyCode.Alpha7)) { playersPerTeam = 7; }
                else if (Input.GetKeyDown(KeyCode.Alpha8)) { playersPerTeam = 8; }
                else if (Input.GetKeyDown(KeyCode.Alpha9)) { playersPerTeam = 9; }

                //int numCharacters = characters.Length; //necessary if we want # of teams instead
                //int maxColors = RWFMod.MaxColorsHardLimit;
                int counter = -1;
                int colorIndex = 0;
                List<int> colors = new(Enumerable.Range(0, RWFMod.MaxColorsHardLimit));

                //randomize the team colors so it's not just `orange -> blue -> etc` every time
                colors.Shuffle();
                characters.Shuffle();
                foreach (var character in characters)
                {
                    counter++;
                    if (counter == playersPerTeam)
                    {
                        counter = 0;
                        colorIndex++;
                        if (colorIndex == RWFMod.MaxColorsHardLimit)
                        {
                            colorIndex = 0;
                        }
                    }
                    //Logger.LogInfo("Randomized " + character.actorID + " to: " + character.colorID);
                    __instance.SendRPC_ChangeIDToTeam(character.uniqueID, colors[colorIndex]);
                }
            }

            //randomize all players
            else if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                LobbyCharacter[] characters = PhotonNetwork.CurrentRoom.Players.Values.ToList().Select(p => p.GetProperty<LobbyCharacter[]>("players")).SelectMany(p => p).Where(p => p != null).ToArray();

                foreach (var character in characters)
                {
                    int random_color = UnityEngine.Random.RandomRangeInt(0, RWFMod.MaxColorsHardLimit);
                    //character.colorID = random_color;
                    //Logger.LogWarning("Randomized " + character.actorID + " to: " + random_color);

                    __instance.SendRPC_ChangeIDToTeam(character.uniqueID, random_color);
                }
            }
        }
    }
}

public static class PrivateRoomCharacterSelectionInstance_Extensions
{
    public static void SendRPC_ChangeIDToTeam(this PrivateRoomCharacterSelectionInstance instance, int uniqueID,  int colorID)
    {
        var character = PrivateRoomHandler.instance.FindLobbyCharacter(uniqueID);
        var characterSelectorPhotonView = PrivateRoomHandler.instance.versusDisplay.PlayerSelectorGO(character.uniqueID).GetComponent<PhotonView>();
        characterSelectorPhotonView.RPC(nameof(PrivateRoomCharacterSelectionInstance.RPCA_ChangeTeam), RpcTarget.All, colorID);
    }
}

public static class ListExtension
{
    public static void Shuffle<T>(this IList<T> list)
    {
        var count = list.Count;
        var last = count - 1;
        for ( var i = 0; i < last; i++ )
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}
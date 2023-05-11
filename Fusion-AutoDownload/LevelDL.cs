using BoneLib.BoneMenu.Elements;
using LabFusion.BoneMenu;
using LabFusion.Network;
using LabFusion.Utilities;
using SLZ.Marrow.Forklift.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnhollowerBaseLib;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Il2Cpp = Il2CppSystem.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppMono.Globalization.Unicode;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon
    {
        public static List<(string, Action)> WaitingMapButtons = new List<(string, Action)> ();
        public static void OnLevelCrateAdded(string barcode)
        {
            (string, Action)[] pendingMapButtons = WaitingMapButtons.Where(bt => bt.Item1 == barcode).ToArray();
            foreach ((string, Action) mapButton in pendingMapButtons)
            {
                Msg("Map invoke");
                mapButton.Item2.Invoke();
                WaitingMapButtons.Remove(mapButton);
            }
        }

        // Patches
        [HarmonyPatch]
        public static class BoneMenuCreator_CreateLobby_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method("LabFusion.BoneMenu.BoneMenuCreator:CreateLobby", new[] { typeof(MenuCategory), typeof(LobbyMetadataInfo), typeof(INetworkLobby), typeof(LobbySortMode) });
            }

            public static LobbyMetadataInfo LatestLobbyInfo;
            public static INetworkLobby LatestLobby;
            [HarmonyPrefix]
            static void Prefix(object __instance, MenuCategory category, LobbyMetadataInfo info, INetworkLobby lobby, LobbySortMode sortMode)
            {
                LatestLobbyInfo = info;
                LatestLobby = lobby;
            }
        }
        [HarmonyPatch(typeof(MenuCategory), "CreateFunctionElement", new Type[] { typeof(string), typeof(Color), typeof(Action) })]
        class MenuCategory_CreateFunctionElement_Patch
        {
            private static Dictionary<string, FunctionElement> s_post2Pre = new Dictionary<string, FunctionElement>();
            [HarmonyPrefix]
            private static void Prefix(MenuCategory __instance, ref string name, ref Color color, ref Action action)
            {
                if (action != null && color == Color.white && name == "Join Server")
                {
                    LobbyMetadataInfo info = BoneMenuCreator_CreateLobby_Patch.LatestLobbyInfo;
                    INetworkLobby lobby = BoneMenuCreator_CreateLobby_Patch.LatestLobby;
                    action = () =>
                    {
                        info.ClientHasLevel = FusionSceneManager.HasLevel(info.LevelBarcode);
                        lobby.CreateJoinDelegate(info).Invoke();
                    };
                }
                if (action == null && color == Color.red && name.StartsWith("Level: "))
                {
                    Msg("Map Button Created!");
                    string mapBarcode = BoneMenuCreator_CreateLobby_Patch.LatestLobbyInfo.LevelBarcode;
                    string palletBarcode = mapBarcode.Substring(0, mapBarcode.IndexOf(".Level."));

                    if (AttemptedPallets.Contains(palletBarcode))
                        return;

                    if (!FusionSceneManager.HasLevel(mapBarcode))
                    {
                        Msg("Map not Installed!");
                        Msg(mapBarcode + " - " + palletBarcode);

                        if (ModListings.TryGetValue(palletBarcode, out (ModListing, ModTarget) foundMod))
                        {
                            Msg("Map found in some repo, Button Setup!");
  
                            color = Color.yellow;
                            name += " (Download)";

                            string finName = name;
                            action = () =>
                            {
                                if (AttemptedPallets.Contains(palletBarcode))
                                    return;
                                AttemptedPallets.Add(palletBarcode);

                                Msg("Downloading map!");
                                s_post2Pre[finName].SetColor(Color.blue);
                                s_post2Pre[finName].SetName("Downloading...");


                                LatestModDownloadManager.DownloadMod(foundMod.Item1, foundMod.Item2);

                                WaitingMapButtons.Add((mapBarcode, () =>
                                {
                                    if (s_post2Pre[finName] != null)
                                    {
                                        s_post2Pre[finName].SetColor(Color.green);
                                        s_post2Pre[finName].SetName("Download Complete!");
                                    }

                                    s_post2Pre.Remove(finName);
                                }));
                            };  
                        }
                    }
                }
            }
            [HarmonyPostfix]
            private static void Postfix(MenuCategory __instance, string name, Color color, Action action, FunctionElement __result)
            {
                if (action != null && color == Color.yellow && name.StartsWith("Level: ") && name.EndsWith(" (Download)"))
                {
                    if (!s_post2Pre.ContainsKey(name))
                        s_post2Pre.Add(name, __result);
                    else
                    {
                        if (s_post2Pre[name] == null)
                        {
                            s_post2Pre.Remove(name);
                            s_post2Pre.Add(name, __result);
                        }
                    }
                }
            }
        }
    }

}

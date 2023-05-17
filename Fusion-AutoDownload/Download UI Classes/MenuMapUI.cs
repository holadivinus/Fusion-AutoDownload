using BoneLib.BoneMenu.Elements;
using BoneLib.BoneMenu.UI;
using Cysharp.Threading.Tasks.Triggers;
using HarmonyLib;
using LabFusion.BoneMenu;
using LabFusion.Network;
using LabFusion.Utilities;
using SLZ.Marrow.Forklift.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FusionAutoDownload.UIClasses
{
    public class MenuMapUI : ProgressUI 
    {
        // BoneLib.BoneMenu.UI = Monobehaviour
        // BoneLib.BoneMenu.Elements = Handler

        public static readonly PropertyInfo FunctionElement_Action_setter = AccessTools.Property(typeof(FunctionElement), "Action");

        public MenuMapUI(FunctionElement functionElement, ModWrapper mod, string crateBarcode) : base()
        {
            _functionElement = functionElement; 
            CrateBarcode = crateBarcode;
            _mod = mod;
            _originalName = _functionElement.Name;
            _functionElement.SetName("Download " + _originalName);
            _functionElement.SetColor(Color.yellow);

            RepoWrapper.GetURLFileSize(_mod.Url, bytes =>
            {
                _functionElement.SetName(_originalName + $" ({Mathf.RoundToInt(bytes / 1e+6f)}mb)");
            });

            FunctionElement_Action_setter.SetValue(functionElement, (Action)onClick);
        }

        private string _originalName;
        private FunctionElement _functionElement;

        private bool _complete;
        private bool _downloadState;
        private void onClick()
        {
            if (!_mod.Downloading && !_mod.Installed)
            {
                _mod.TryDownload();
                if (_mod.Downloading)
                {
                    _functionElement.SetColor(Color.cyan);
                    _functionElement.SetName("Downloading...");
                }
                else
                {
                    _functionElement.SetColor(Color.red);
                    _functionElement.SetName("Download Blocked");
                }
            }
            else if (_mod.Downloading)
                _downloadState = !_downloadState;
        }

        protected override IEnumerator UpdateLoop()
        {
            while (!ModNull)
            {
                if (_mod.Downloading && _mod.MB != null && _mod.Percent != null)
                    _functionElement.SetName("Downloading... (" + (_downloadState ? _mod.MB : _mod.Percent).ToString() + ")");

                yield return null;
            }
        }

        protected override void OnMyCrateAdded()
        {
            _functionElement.SetName("Installed!");
            _functionElement.SetColor(Color.green);
            _mod = null;
        }

        // Relevant Patches

        // Supply
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
            public static void Prefix(object __instance, MenuCategory category, LobbyMetadataInfo info, INetworkLobby lobby, LobbySortMode sortMode)
            {
                LatestLobbyInfo = info;
                LatestLobby = lobby;
            }
        }

        [HarmonyPatch(typeof(MenuCategory), "CreateFunctionElement", new Type[] { typeof(string), typeof(Color), typeof(Action) })]
        class MenuCategory_CreateFunctionElement_Patch
        {
            private static bool _shouldRun;
            [HarmonyPostfix]
            public static void Postfix(MenuCategory __instance, string name, Color color, Action action, FunctionElement __result)
            {
                LobbyMetadataInfo info = BoneMenuCreator_CreateLobby_Patch.LatestLobbyInfo;
                if (action != null && color == Color.white && !__instance.Name.Contains("Manual") && name == "Join Server")
                {
                    INetworkLobby lobby = BoneMenuCreator_CreateLobby_Patch.LatestLobby;
                    FunctionElement_Action_setter.SetValue(__result, new Action(() =>
                    {
                        info.ClientHasLevel = true;// FusionSceneManager.HasLevel(info.LevelBarcode);
                        lobby.CreateJoinDelegate(info).Invoke();
                    }));
                } else if (action == null && color == Color.red && name.StartsWith("Level: "))
                {
                    var crateBarcode = RepoWrapper.GetPalletBarcode(info.LevelBarcode);
                    if (crateBarcode.HasValue)
                        if (RepoWrapper.Barcode2Mod.TryGetValue(crateBarcode.Value.Item1, out ModWrapper mod))
                            if (_shouldRun = !_shouldRun)
                                new MenuMapUI(__result, mod, info.LevelBarcode);
                }
            }
        }
        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

using HarmonyLib;
using LabFusion.Representation;
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Reflection;
using System.Collections;
using Il2CppMono.Globalization.Unicode;

namespace FusionAutoDownload
{
    public class AvatarUI : ProgressUI
    {
        public static Dictionary<PlayerRep, AvatarUI> PlayerUIs = new Dictionary<PlayerRep, AvatarUI>();
        private readonly static FieldInfo s_field_PlayerRep_isAvatarDirty = AccessTools.Field(typeof(PlayerRep), "_isAvatarDirty");


        public AvatarUI(PlayerRep player) : base() // U
        {
            // Inheritted
            CrateBarcode = player.avatarId;

            // !Inheritted (UI GameObject Setup)
            msg("AvatarUI Created for " + player.Username);
            UIPlayer = player;

            // Update PlayerRep Dict
            if (PlayerUIs.ContainsKey(UIPlayer))
                PlayerUIs.Remove(UIPlayer);
            PlayerUIs.Add(UIPlayer, this);

            // Remove Past UIs?
            for (int i = UIPlayer.repCanvasTransform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(UIPlayer.repCanvasTransform.GetChild(i).gameObject);

            // Create our ui
            _uiRoot = UnityEngine.Object.Instantiate(ProgressUI.UIAssetAvatar, UIPlayer.repCanvas.transform);

            // Fix Fonts
            _texts = _uiRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in _texts)
                text.font = player.repNameText.font;

            _statusImage = _uiRoot.transform.GetChild(1).GetComponent<Image>();
            _statusImage.color = Color.white;

            //_avatarImage = _uiRoot.GetComponentInChildren<RawImage>(true);

            _downloadBar = _uiRoot.transform.GetChild(0).GetChild(1).GetChild(0).Cast<RectTransform>();
            _barGroup = _uiRoot.transform.GetChild(0).GetComponent<CanvasGroup>();

            // Share fixed font with other UI
            if (SpawnableUI.FixedFont == null)
            {
                SpawnableUI.FixedFont = player.repNameText.font;
                SpawnableUI.FontFix.Invoke();
            }
        }

        // !Inheritted
        public PlayerRep UIPlayer;

        private readonly CanvasGroup _barGroup;
        private readonly RectTransform _downloadBar;
        private readonly TextMeshProUGUI[] _texts;
        private readonly Image _statusImage;
        //private readonly RawImage _avatarImage;

        /// <summary>
        /// Function
        /// </summary>
        /// <param name="crateBarcode"></param>
        /// <param name="success"></param>
        public void UpdateCrate(string crateBarcode, bool success) // U
        {
            if (!Nulling)
            {
                // Big props to Alfi's PeasantNulls for breaking one of the simplest rules (crate barcodes MUST be prefixed with the pallet's barcode)
                CrateBarcode = crateBarcode.Replace("Alfi.PeasantNullOPTIONALVARIANTS", "Alfi.PeasantNull")
                                           .Replace("Alfi.PeasantNullsEVILBODYEXPANSION", "Alfi.PeasantNullsFusionCustoms");

                var palletCrate = RepoWrapper.GetPalletBarcode(crateBarcode);

                if (palletCrate.HasValue && RepoWrapper.Barcode2Mod.TryGetValue(palletCrate.Value.Item1, out ModWrapper mod))
                {
                    _mod = mod;
                    _texts[0].text = mod.Barcode;
                    if (!success)
                    {
                        msg($"Avatar {crateBarcode} attempting download!");
                        mod.TryDownload(() =>
                        {
                            if (_statusImage != null)
                                _statusImage.color = mod.Downloading ? Color.yellow : Color.red;

                            msg($"Avatar {crateBarcode} " + (mod.Downloading ? "now downloading!" : "not downloading."));
                        });
                    }
                    else
                    {
                        _statusImage.color = Color.white;
                        _mod = null;
                        msg("Avatar not changed! Avatar is an installed mod. " + crateBarcode);
                    }
                }
                else
                {
                    _mod = null;
                    msg("Avatar not changed! " + (success ? "Avatar is base-game" : "Avatar was not found in Barcode2Mod Dict!"));
                    _statusImage.color = success ? Color.white : Color.red;
                }
            }
            else msg($"Error! Cant update, this UI no longer exists.");
        }

        // Inheritted
        protected override IEnumerator UpdateLoop()
        {
            while (!Nulling)
            {
                // Update Download UI
                _texts[1].text = _mod?.MB ?? "-1mb / -1mb";
                _texts[2].text = _mod?.Percent ?? "100%";

                Vector2 sizeDelta = _downloadBar.sizeDelta;
                sizeDelta.x = (_mod?.Progress ?? 1) * 1140;
                _downloadBar.sizeDelta = sizeDelta;

                _barGroup.alpha += ((_mod?.Downloading ?? false) ? 1 : -1) * Time.deltaTime;
                _barGroup.alpha = Mathf.Clamp01(_barGroup.alpha);

                yield return null;
            }
        }

        protected override void OnMyCrateAdded() // U
        {
            s_field_PlayerRep_isAvatarDirty.SetValue(UIPlayer, true);
        }

        // Relevant Patching

        [HarmonyPatch(typeof(PlayerRep), "OnSwapAvatar", new Type[] { typeof(bool) })]
        class Patch_PlayerRep_OnSwapAvatar
        {
            [HarmonyPrefix]
            public static void Prefix(PlayerRep __instance, bool success) // U
            {
                msg($"User \"{__instance.Username}\" attempted to swap into {__instance.avatarId}, success: {success}");
                if (PlayerUIs.TryGetValue(__instance, out AvatarUI avatarUI))
                {
                    msg($"AvatarUI found for {__instance.Username}, updating crate to {__instance.avatarId}");
                    avatarUI.UpdateCrate(__instance.avatarId, success);
                }
                else msg($"Error: {__instance.Username} has no AvatarUI registered in the PlayerUIs Dict!");
            }
        }

        [HarmonyPatch(typeof(PlayerRep), "CreateNametag")]
        class AvatarDownloadUI
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerRep __instance) => new AvatarUI(__instance); // U
        }

        private static void msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

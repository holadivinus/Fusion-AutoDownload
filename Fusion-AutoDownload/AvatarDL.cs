using System;
using System.Reflection;

using TMPro;
using SLZ.Marrow.Forklift.Model;

using HarmonyLib;
using LabFusion.Representation;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Networking;
using static MelonLoader.MelonLogger;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon
    {
        private static FieldInfo s_field_PlayerRep_isAvatarDirty = AccessTools.Field(typeof(PlayerRep), "_isAvatarDirty");

        public static void OnAvatarCrateAdded(string barcode)
        {
            foreach (PlayerRep plrRep in PlayerRepManager.PlayerReps)
            {
                if (plrRep.avatarId == barcode)
                {
                    s_field_PlayerRep_isAvatarDirty.SetValue(plrRep, true);
                    Msg($"Avatar Applied! ({plrRep.Username}, {plrRep.avatarId})");
                }
            }
        }

        public static void TryDownloadRepsAvatar(PlayerRep player)
        {
            string palletBarcode = player.avatarId.Substring(0, player.avatarId.IndexOf(".Avatar."));

            if (AttemptedPallets.Contains(palletBarcode))
                return;
            AttemptedPallets.Add(palletBarcode);

            Msg("Avatar failed to load: " + player.avatarId);

            if (ModListings.TryGetValue(palletBarcode, out (ModListing, ModTarget) foundMod))
            {
                Msg("Found Avatar's Pallet in some linked Repo!");
                Msg("Avatar Enqueued for download...");
                DownloadQueue.Enqueue(new Action(() =>
                {
                    Msg("Avatar now downloading: " + foundMod.Item1.Barcode.ID);
                    DownloadingMods.Add(foundMod.Item2.Cast<DownloadableModTarget>().Url, foundMod.Item1);
                    LatestModDownloadManager.DownloadMod(foundMod.Item1, foundMod.Item2);
                }));
            }
            else
            {
                Msg($"couldn't find the Avatar's Pallet ({palletBarcode}) in any Repos, cancelled.");
                AvatarDownloadUI.PlayerUIs[player].AvatarStateImage.color = new Color(1, 0, 0, .5f);
            }

            AttemptedPallets.Add(palletBarcode);
        }

        // Detect fail
        [HarmonyPatch(typeof(PlayerRep), "OnSwapAvatar", new Type[] { typeof(bool) })]
        class Patch_PlayerRep_OnSwapAvatar
        {
            [HarmonyPrefix]
            private static void Prefix(PlayerRep __instance, bool success)
            {
                if (!success)
                    TryDownloadRepsAvatar(__instance);
                else
                {
                    // Manage Succeed UI
                    AvatarDownloadUI.PlayerUIs[__instance].BarVisible = false;
                    AvatarDownloadUI.PlayerUIs[__instance].AvatarStateImage.color = new Color(1, 1, 1, .5f);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerRep), "CreateNametag")]
        class AvatarDownloadUI
        {
            public static void OnLateUpdate()
            {
                foreach (KeyValuePair<PlayerRep, DownloadUI> item in PlayerUIs)
                {
                    item.Value.OnLateUpdate();
                }
            }
            public class DownloadUI
            {
                public DownloadUI(PlayerRep player, GameObject root)
                {
                    Player = player;
                    Texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (TextMeshProUGUI text in Texts)
                    {
                        text.font = player.repNameText.font;
                    }

                    downloadImage = root.GetComponentInChildren<RawImage>(true);

                    downloadBar = root.transform.GetChild(0).GetChild(1).GetChild(0).Cast<RectTransform>();
                    BarGroup = root.transform.GetChild(0).GetComponent<CanvasGroup>();
                    AvatarStateImage = root.transform.GetChild(1).GetComponent<Image>();
                }
                public readonly PlayerRep Player;
                public readonly TextMeshProUGUI[] Texts;
                public readonly Image AvatarStateImage;
                private readonly RawImage downloadImage;
                private readonly RectTransform downloadBar;
                private readonly CanvasGroup BarGroup;
                public bool BarVisible;
                public void SetData(string avatarID, string mB, string percent)
                {
                    if (Texts != null && Texts[0] != null)
                    {
                        Texts[0].text = avatarID;
                        Texts[1].text = mB;
                        Texts[2].text = percent;
                    }
                    else OnNull();
                }
                public void SetImage(Texture img)
                {
                    if (downloadImage != null)
                        downloadImage.texture = img;
                    else OnNull();
                }
                public void SetWidth(float width)
                {
                    if (downloadBar != null)
                    {
                        Vector2 sizeDelta = downloadBar.sizeDelta;
                        sizeDelta.x = width * 1140;
                        downloadBar.sizeDelta = sizeDelta;
                    }
                    else OnNull();
                }
                public void OnLateUpdate()
                {
                    if (BarGroup != null)
                    {
                        BarGroup.alpha += (BarVisible ? 1 : -1) * Time.deltaTime;
                        BarGroup.alpha = Mathf.Clamp01(BarGroup.alpha);
                    }
                    else OnNull();
                }

                bool nulling = false;
                public void OnNull()
                {
                    if (!nulling)
                    {
                        // incase this is called during a foreach
                        DownloadQueue.Enqueue(() => { PlayerUIs.Remove(Player); });
                        nulling = true;
                    }
                }
            }
            public static Dictionary<PlayerRep, DownloadUI> PlayerUIs = new Dictionary<PlayerRep, DownloadUI>(); 

            [HarmonyPostfix]
            public static void Postfix(PlayerRep __instance)
            {
                // Remove Past UIs?
                for (int i = __instance.repCanvasTransform.childCount - 1; i >= 0; i--)
                {
                    GameObject.DestroyImmediate(__instance.repCanvasTransform.GetChild(i).gameObject);
                }

                if (PlayerUIs.ContainsKey(__instance))
                {
                    PlayerUIs.Remove(__instance);
                }

                DownloadUI playerUI = new DownloadUI(__instance, UnityEngine.Object.Instantiate(UIAssetAvatar, __instance.repCanvas.transform));
                PlayerUIs.Add(__instance, playerUI);

                // Garbage hacky fix for getting fonts on spawnable uis
                if (!PendingSpawnable.FontFound)
                {
                    PendingSpawnable.FoundFont = __instance.repNameText.font;
                    PendingSpawnable.FontFound = true;

                    foreach (PendingSpawnable ps in WaitingSpawnables)
                    {
                        foreach (TextMeshProUGUI text in ps.Texts)
                        {
                            text.font = PendingSpawnable.FoundFont;
                        }
                    }
                }
            }

            public static void OnDownloadProgress(UnityWebRequest uwr, ModListing mod, float progress)
            {
                foreach (var playerUI in PlayerUIs)
                {
                    if (playerUI.Key.avatarId.StartsWith(mod.Barcode.ID + ".Avatar."))
                    {
                        playerUI.Value.BarVisible = true;
                        playerUI.Value.SetWidth(progress);
                        playerUI.Value.SetData
                        (
                            mod.Barcode.ID,
                            $"{Mathf.RoundToInt(uwr.downloadedBytes / 1e+6f)}mb /{Mathf.RoundToInt((uwr.downloadedBytes / 1e+6f) / progress)}mb",
                            Mathf.RoundToInt(progress * 100).ToString() + "%"
                        );
                        playerUI.Value.AvatarStateImage.color = new Color(1, 240f / 254f, .5f);
                    }
                }
            }
        }
    }
}

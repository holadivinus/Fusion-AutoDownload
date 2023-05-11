using LabFusion.Representation;
using MelonLoader;
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SLZ.Marrow.Forklift.Model;
using Il2Cpp = Il2CppSystem.Collections.Generic;
using System.CodeDom;

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
                    LatestModDownloadManager.DownloadMod(foundMod.Item1, foundMod.Item2);
                }));
            }
            else Msg($"couldn't find the Avatar's Pallet ({palletBarcode}) in any Repos, cancelled.");

            AttemptedPallets.Add(palletBarcode);
        }
    }

    [HarmonyPatch(typeof(PlayerRep), "OnSwapAvatar", new Type[] { typeof(bool) })]
    class Patch_PlayerRep_OnSwapAvatar
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerRep __instance, bool success)
        {
            if (!success)
                AutoDownloadMelon.TryDownloadRepsAvatar(__instance);
        }
    }

    /*[HarmonyPatch(typeof(PlayerRep), "CreateNametag")]
    class Patch_PlayerRep_CreateNametag
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerRep __instance)
        {
            




        }
    }*/
}

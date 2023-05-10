using LabFusion.Representation;
using MelonLoader;
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SLZ.Marrow.Forklift.Model;
using Il2Cpp = Il2CppSystem.Collections.Generic;

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

            if (ModListings.TryGetValue(palletBarcode, out ModListing foundMod))
            {
                Msg("Found Avatar's Pallet in some linked Repo!");

                bool onPC = false;
                foreach (Il2Cpp.KeyValuePair<string, ModTarget> possibleTarg in foundMod.Targets)
                {
                    if (possibleTarg.key == "pc")
                    {
                        onPC = true;

                        Msg("Avatar supported on pc. Avatar Enqueued for download...");
                        DownloadQueue.Enqueue(new Action(() =>
                        {
                            Msg("Avatar now downloading: " + foundMod.Barcode.ID);
                            LatestModDownloadManager.DownloadMod(foundMod, possibleTarg.value);
                        }));
                        break;
                    }
                }
                if (!onPC)
                {
                    Msg("Avatar was unsupported on pc.");
                }
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
}

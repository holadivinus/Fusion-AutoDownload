using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

using UnityEngine;
using Il2Cpp = Il2CppSystem.Collections.Generic;

using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;
using Cysharp.Threading.Tasks;

using HarmonyLib;
using MelonLoader;
using BoneLib.BoneMenu.Elements;
using LabFusion.Representation;
using LabFusion.BoneMenu;
using LabFusion.Network;
using System.Reflection.Emit;
using LabFusion.Utilities;
using BoneLib.Nullables;
using LabFusion.Exceptions;
using SLZ.Marrow.Data;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon : MelonMod
    {
        /// <summary>
        /// Blacklisted Pallets to stop multiple downloads of the same thing
        /// </summary>
        public static List<string> AttemptedPallets = new List<string>();

        /// <summary>
        /// Queue of Actions where each one is dequeued and invoked.
        /// Use to space out downloads & stop crashes.
        /// </summary>
        public static Queue<Action> DownloadQueue = new Queue<Action>();

        /// <summary>
        /// Creates a new ModDownloadManager for every query; A one time use class
        /// </summary>
        public static ModDownloadManager LatestModDownloadManager { get => new ModDownloadManager(); }

        /// <summary>
        /// Connects a Barcode to its Pallet.
        /// </summary>
        public static Dictionary<string, ModListing> ModListings = new Dictionary<string, ModListing>();

        public override void OnLateInitializeMelon()
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.1)")
            .PatchAll();

            SetupFetchedRepositories(LatestModDownloadManager.FetchRepositoriesAsync(""));

            // On Warehouse Ready
            AssetWarehouse.OnReady(new Action(() =>
            {
                // On Crate Added
                AssetWarehouse.Instance.OnCrateAdded += new Action<string>(async name =>
                {
                    Msg("Crate added: " + name);
                    await Task.Delay(1000);

                    if (name.Contains(".Avatar."))
                        OnAvatarCrateAdded(name);
                    else if (name.Contains(".Level."))
                        OnLevelCrateAdded(name);
                    else if (name.Contains(".Spawnable."))
                        OnSpawnableCrateAdded(name);

                });
            }));

            DownloadQueueRunner();
        }

        public static void Msg(object msg)
        {
#if DEBUG
            MelonLogger.Msg(msg);
#endif
        }

        /// <summary>
        /// Async loop to dequeue Actions from DownloadQueue and invoke them over time
        /// </summary>
        private static async void DownloadQueueRunner()
        {
            // Space out downloads to avoid crash
            while (true)
            {
                await Task.Delay(2000);
                if (DownloadQueue.Count != 0)
                    DownloadQueue.Dequeue().Invoke();
            }
        }

        /// <summary>
        /// Initializes ModListings, and fills it up with barcode:Modlisting pairings
        /// </summary>
        /// <param name="fetcher">Unitask for fetching the Repo List</param>
        public static async void SetupFetchedRepositories(UniTask<Il2Cpp.List<ModRepository>> fetcher)
        {
            ModListings = new Dictionary<string, ModListing>();
            foreach (ModRepository modRepo in await fetcher)
            {
                foreach (ModListing mod in modRepo.Mods)
                {
                    if (!ModListings.ContainsKey(mod.Barcode.ID))
                    {
                        ModListings.Add(mod.Barcode.ID, mod);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using Il2Cpp = Il2CppSystem.Collections.Generic;

using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;
using Cysharp.Threading.Tasks;

using MelonLoader;

using System.Linq;
using System.Runtime.CompilerServices;
using SLZ.SaveData;

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
        /// Connects a Barcode to its Pallet & Downloader.
        /// </summary>
        public static Dictionary<string, (ModListing, ModTarget)> ModListings = new Dictionary<string, (ModListing, ModTarget)>();

        public override void OnLateInitializeMelon()
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.1)")
            .PatchAll();

            SetupFetchedRepositories(new ModDownloadManager().FetchRepositoriesAsync(""));

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
                await Task.Delay(1000);
                if (DownloadQueue.Count != 0)
                    DownloadQueue.Dequeue().Invoke();
                
                if (Input.GetKey(KeyCode.Y))
                {
                    ModListing mod = ModListings["Jass.FordCop"].Item1;
                    Msg(mod.Barcode.ID);
                    if (mod.Targets.ContainsKey("pc"))
                        Msg(mod.Targets["pc"].TryCast<DownloadableModTarget>());
                }
            }
        }

        /// <summary>
        /// Initializes ModListings, and fills it up with barcode:Modlisting pairings
        /// </summary>
        /// <param name="fetcher">Unitask for fetching the Repo List</param>
        public static async void SetupFetchedRepositories(UniTask<Il2Cpp.List<ModRepository>> fetcher)
        {
            int c = 0;
            ModListings = new Dictionary<string, (ModListing, ModTarget)>();
            foreach (ModRepository modRepo in await fetcher)
            {
                foreach (ModListing mod in modRepo.Mods)
                {
                    if (!ModListings.ContainsKey(mod.Barcode.ID))
                    {
                        if (mod.Targets.ContainsKey("pc"))
                        {
                            DownloadableModTarget downloadable = mod.Targets["pc"].TryCast<DownloadableModTarget>();
                            if (downloadable != null)
                            {
                                ModListings.Add(mod.Barcode.ID, (mod, mod.Targets["pc"]));
                                c++;
                            }
                        }
                    }
                }
            }
            Msg(c.ToString() + " DownloadableMods!");
        }
    }
}

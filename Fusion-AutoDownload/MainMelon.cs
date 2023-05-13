using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using Cysharp.Threading.Tasks;
using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;

using Il2Cpp = Il2CppSystem.Collections.Generic;
using MelonLoader;
using System.IO;
using LabFusion.Data;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine.Networking;

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
        /// Dictionary of downloading mod Urls & ModListings
        /// </summary>
        public static Dictionary<string, ModListing> DownloadingMods = new Dictionary<string, ModListing>();

        /// <summary>
        /// Creates a new ModDownloadManager for every query; A one time use class
        /// </summary>
        public static ModDownloadManager LatestModDownloadManager { get => new ModDownloadManager(); }

        /// <summary>
        /// Connects a Barcode to its Pallet & Downloader.
        /// </summary>
        public static Dictionary<string, (ModListing, ModTarget)> ModListings = new Dictionary<string, (ModListing, ModTarget)>();

        private static AssetBundle s_uiBundle;
        public static GameObject UIAssetAvatar
        {
            get
            {
                if (s_uiAssetAvatarInternal == null)
                {
                    s_uiAssetAvatarInternal = s_uiBundle.LoadAsset("Assets/UI/AutoDownload UI.prefab").Cast<GameObject>();
                }
                return s_uiAssetAvatarInternal;
            }
        }
        private static GameObject s_uiAssetAvatarInternal;
        public static GameObject UIAssetSpawnable
        {
            get
            {
                if (s_uiAssetSpawnableInternal == null)
                {
                    s_uiAssetSpawnableInternal = s_uiBundle.LoadAsset("Assets/UI/SpawnableUI.prefab").Cast<GameObject>();
                }
                return s_uiAssetSpawnableInternal;
            }
        }
        private static GameObject s_uiAssetSpawnableInternal;
        public static Sprite[] UISprites 
        { 
            get 
            {
                if (s_uiSpritesInternal == null || s_uiSpritesInternal[0] == null)
                {
                    s_uiSpritesInternal = new Sprite[2];
                    s_uiSpritesInternal[0] = s_uiBundle.LoadAsset("Assets/UI/AutoDownload UI/PersonIcon.png").Cast<Sprite>();
                    s_uiSpritesInternal[1] = s_uiBundle.LoadAsset("Assets/UI/AutoDownload UI/PersonIconX.png").Cast<Sprite>();
                }
                return s_uiSpritesInternal;
            }
        }
        private static Sprite[] s_uiSpritesInternal;
        public override void OnLateInitializeMelon()
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.1)")
            .PatchAll();

            SetupFetchedRepositories(new ModDownloadManager().FetchRepositoriesAsync(""));

            // On Warehouse Ready
            AssetWarehouse.OnReady(new Action(() =>
            {
                string[] crateTypes = { ".Avatar.", ".Level.", ".Spawnable." };
                // On Crate Added
                AssetWarehouse.Instance.OnCrateAdded += new Action<string>(async name =>
                {
                    Msg("Crate added: " + name);
                    await Task.Delay(1000);

                    DownloadQueue.Enqueue(() =>
                    {
                        if (name.Contains(".Avatar."))
                        {
                            OnAvatarCrateAdded(name);
                            if (ModListings.TryGetValue(name.Split(new string[] { ".Avatar." }, StringSplitOptions.None)[0], out var mod))
                            {
                                string url = mod.Item2.Cast<DownloadableModTarget>().Url;
                                if (DownloadingMods.ContainsKey(url))
                                {
                                    DownloadingMods.Remove(url);
                                }
                            }
                        }
                        else if (name.Contains(".Level."))
                        {
                            OnLevelCrateAdded(name);
                            if (ModListings.TryGetValue(name.Split(new string[] { ".Level." }, StringSplitOptions.None)[0], out var mod))
                            {
                                string url = mod.Item2.Cast<DownloadableModTarget>().Url;
                                if (DownloadingMods.ContainsKey(url))
                                {
                                    DownloadingMods.Remove(url);
                                }
                            }
                        }
                        else if (name.Contains(".Spawnable."))
                        {
                            OnSpawnableCrateAdded(name);
                            if (ModListings.TryGetValue(name.Split(new string[] { ".Spawnable." }, StringSplitOptions.None)[0], out var mod))
                            {
                                string url = mod.Item2.Cast<DownloadableModTarget>().Url;
                                if (DownloadingMods.ContainsKey(url))
                                {
                                    DownloadingMods.Remove(url);
                                }
                            }
                        }
                    });
                });
            }));

            s_uiBundle = EmbeddedAssetBundle.LoadFromAssembly(System.Reflection.Assembly.GetExecutingAssembly(), "FusionAutoDownload.uiassets");

            ModDownloadProgress_Patch.PatchMDMctor();
        }

        public static void Msg(object msg)
        {
#if DEBUG
            //MelonLogger.Msg(new System.Diagnostics.StackTrace());
            MelonLogger.Msg(msg);
#endif
        }

        /// <summary>
        /// Any usage of MarrowSDK MUST be in an Action, Enqueued to the DownloadQueue.
        /// Unfortunately any calls to MarrowSDK that arent on unity's main thread will
        /// instantly crash the entire game (with no related logs)
        /// </summary>
        public override void OnLateUpdate()
        {
            if (DownloadQueue.Count != 0)
                DownloadQueue.Dequeue().Invoke();

            AvatarDownloadUI.OnLateUpdate();

            foreach (PendingSpawnable ps in WaitingSpawnables)
                ps.OnLateUpdate();
        }

        /// <summary>
        /// Used to protect DownloadableModTargets from the garbage man.
        /// </summary>
        private static List<DownloadableModTarget> s_antiGarbageCollector = new List<DownloadableModTarget>();

        /// <summary>
        /// Initializes ModListings, and fills it up with valid barcode:(Modlisting, ModTarget) pairings.
        /// Additionally protects ModTargets from being eated by the IL2CPP Collector via s_antiGarbageCollector.
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
                                s_antiGarbageCollector.Add(downloadable);
                                c++;
                            }
                        }
                    }
                }
            }
            Msg(c.ToString() + " DownloadableMods!");
        }

        class ModDownloadProgress_Patch
        {
            private static MDMProgressPatchDelegate _original;

            public delegate void MDMProgressPatchDelegate(IntPtr instance, IntPtr FileDownloader, IntPtr taskItem, float progress, IntPtr method);

            // Exampled from main fusion mod
            public unsafe static void PatchMDMctor()
            {
                MDMProgressPatchDelegate patch = MDMProgress;

                // Mouthful
                string nativeInfoName = "NativeMethodInfoPtr_ModDownloadManager_OnDownloadProgressed_Private_Void_FileDownloader_TaskItem_Single_0";

                var tgtPtr = *(IntPtr*)(IntPtr)typeof(ModDownloadManager).GetField(nativeInfoName, AccessTools.all).GetValue(null);
                var dstPtr = patch.Method.MethodHandle.GetFunctionPointer();

                MelonUtils.NativeHookAttach((IntPtr)(&tgtPtr), dstPtr);
                _original = Marshal.GetDelegateForFunctionPointer<MDMProgressPatchDelegate>(tgtPtr);
            }

            private static void MDMProgress(IntPtr instance, IntPtr fileDownloader, IntPtr taskItem, float progress, IntPtr method)
            {
                UnityWebRequest uwr = new FileDownloader(fileDownloader)._inflight[0];
                if (DownloadingMods.TryGetValue(uwr.url, out ModListing mod))
                {
                    AvatarDownloadUI.OnDownloadProgress(uwr, mod, progress);
                    PendingSpawnable.OnDownloadProgress(uwr, mod, progress);
                }
                _original(instance, fileDownloader, taskItem, progress, method);
            }
        }
    }
}

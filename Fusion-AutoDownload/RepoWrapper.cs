using Cysharp.Threading.Tasks;
using HarmonyLib;
using Jevil;
using MelonLoader;
using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Il2Cpp = Il2CppSystem.Collections.Generic;

namespace FusionAutoDownload
{
    public static class RepoWrapper
    {
        public const string Platform = "pc";

        public static Dictionary<string, ModWrapper> Barcode2Mod = new Dictionary<string, ModWrapper>();
        public static Dictionary<string, ModWrapper> Url2Mod = new Dictionary<string, ModWrapper>();

        // Barcode2DownloadingMod
        public static Dictionary<string, ModWrapper> DownloadingMods = new Dictionary<string, ModWrapper>();

        #region Repo Fetching
        public static async void FetchRepos() // U
        {
            Il2Cpp.List<ModRepository> fetchedRepos = await AutoDownloadMelon.NewModDownloadManager.FetchRepositoriesAsync("");
            foreach (ModRepository modRepo in fetchedRepos)
            {
                AddRepo(modRepo);
            }
            Msg(Barcode2Mod.Count.ToString() + " Downloadable Mods!");
        }
        public static void AddRepo(ModRepository repo) // U
        {
            foreach (ModListing mod in repo.Mods)
            {
                if (mod.Targets.ContainsKey(Platform))
                {
                    if (mod.Targets[Platform].TryCast<DownloadableModTarget>() != null)
                        AddMod(mod, mod.Targets[Platform]);
                }
            }
        }
        public static void AddMod(ModListing mod, ModTarget modTarget) // U
        {
            ModWrapper newWrapper = new ModWrapper(mod, modTarget);

            // If we already have this Barcode
            if (Barcode2Mod.TryGetValue(newWrapper.Barcode, out ModWrapper oldWrapper))
            {
                // and this version is greater, replace
                if (newWrapper > oldWrapper)
                {
                    Barcode2Mod.Remove(oldWrapper.Barcode);
                    Url2Mod.Remove(oldWrapper.Url);

                    Barcode2Mod.Add(newWrapper.Barcode, newWrapper);
                    Url2Mod.Add(newWrapper.Url, newWrapper);
                }
                else return; // otherwise skip the mod
            }
            else // If we don't have this barcode
            {
                // And we don't have this URL, replace
                if (!Url2Mod.ContainsKey(newWrapper.Url))
                {
                    Barcode2Mod.Add(newWrapper.Barcode, newWrapper);
                    Url2Mod.Add(newWrapper.Url, newWrapper);
                }
                else return; // otherwise skip the mod
            }

            // If we're here, the mod's been added to Barcode2Mod & Url2Mod.
        }
        #endregion


        private static readonly Regex s_crates = new Regex(@"^(.*?)\.(Avatar|Level|Spawnable)\.");

        /// <summary>
        /// Get the Pallet Barcode & the Crate type from a Crate Barcode. Null is invalid.
        /// </summary>
        public static (string, string)? GetPalletBarcode(string crateBarcode)
        {
            Match match = s_crates.Match(crateBarcode);
            if (match.Success)
                return (match.Groups[1].Value, match.Groups[2].Value);
            else return null;
        }
        /*public static void TryGetCrate(string crateBarcode)
        {
            var palletBarcode = GetPalletBarcode(crateBarcode);
            if (palletBarcode.HasValue)
                if (Barcode2Mod.TryGetValue(palletBarcode.Value.Item1, out ModWrapper foundMod))
                    foundMod.TryDownload();
        }*/

        public static void OnLateUpdate() // U
        {
            foreach (ModWrapper mod in DownloadingMods.Values)
                mod.CacheDownloadProgressStrings();
        }

        public static void OnPalletProgress(UnityWebRequest uwr, float progress) // !U
        {
            if (Url2Mod.TryGetValue(uwr.url, out ModWrapper mod))
            {
                mod.OnDownloadProgress(uwr, progress);
            }
        }
        public static void OnCrateComplete(string crateBarcode) // U
        {
            AutoDownloadMelon.UnityThread.Enqueue(() =>
            {
                var crate = GetPalletBarcode(crateBarcode);
                if (crate.HasValue)
                    if (Barcode2Mod.TryGetValue(crate.Value.Item1, out ModWrapper mod))
                        mod.OnCrateComplete(crateBarcode);
            });
        }

        // Relevant Patching
        public class ModDownloadProgress_Patch
        {
            private static MDMProgressPatchDelegate _original;

            public delegate void MDMProgressPatchDelegate(IntPtr instance, IntPtr FileDownloader, IntPtr taskItem, float progress, IntPtr method);

            // Exampled from main fusion mod
            public unsafe static void Patch() // U
            {
                MDMProgressPatchDelegate patch = MDMProgress;

                // Mouthful
                string nativeInfoName = "NativeMethodInfoPtr_ModDownloadManager_OnDownloadProgressed_Private_Void_FileDownloader_TaskItem_Single_0";

                var tgtPtr = *(IntPtr*)(IntPtr)typeof(ModDownloadManager).GetField(nativeInfoName, AccessTools.all).GetValue(null);
                var dstPtr = patch.Method.MethodHandle.GetFunctionPointer();

                MelonUtils.NativeHookAttach((IntPtr)(&tgtPtr), dstPtr);
                _original = Marshal.GetDelegateForFunctionPointer<MDMProgressPatchDelegate>(tgtPtr);
            }

            private static void MDMProgress(IntPtr instance, IntPtr fileDownloader, IntPtr taskItem, float progress, IntPtr method) // !U
            {
                UnityWebRequest uwr = new FileDownloader(fileDownloader)._inflight[0];

                // Errors in a Native Patch insta-crash w/out logs.
                try
                {
                    OnPalletProgress(uwr, progress);
                }
                catch (Exception e)
                {
                    Msg("Error caught in Native Patch MDMProgress:");
                    Msg(e);
                }
                _original(instance, fileDownloader, taskItem, progress, method);
            }
        }

        public static async void GetURLFileSize(string url, Action<long> callback)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    if (response.Content.Headers.ContentLength.HasValue)
                    {
                        AutoDownloadMelon.UnityThread.Enqueue(() => 
                        {
                            callback.Invoke(response.Content.Headers.ContentLength.Value);
                        });
                        return;
                    }
                    else
                    {
                        AutoDownloadMelon.UnityThread.Enqueue(() =>
                        {
                            callback.Invoke(-1);
                        });
                        return;
                    }
                }
                catch (Exception)
                {
                    AutoDownloadMelon.UnityThread.Enqueue(() =>
                    {
                        callback.Invoke(-1);
                    });
                }
            }
        }


        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

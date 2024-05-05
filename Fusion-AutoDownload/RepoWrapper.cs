﻿using HarmonyLib;
using LabFusion.Data;
using LabFusion.Network;
using Newtonsoft.Json.Linq;
using SLZ.Marrow;
using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Il2Cpp = Il2CppSystem.Collections.Generic;

namespace FusionAutoDownload
{
    public static class RepoWrapper
    {
        public static AssetBundle UIBundle = EmbeddedAssetBundle.LoadFromAssembly(System.Reflection.Assembly.GetExecutingAssembly(), "FusionAutoDownload.uiassets");
        public const string Platform = "pc";

        public static ModWrapper[] AllMods;
        public static Dictionary<string, ModWrapper> Barcode2Mod = new Dictionary<string, ModWrapper>();
        public static Dictionary<string, ModWrapper> Url2Mod = new Dictionary<string, ModWrapper>();

        // Barcode2DownloadingMod
        public static Dictionary<string, ModWrapper> DownloadingMods = new Dictionary<string, ModWrapper>();

        #region Repo Fetching
        public static async void FetchRepos() // U
        { 
            Il2Cpp.List<ModRepository> fetchedRepos = await new ModDownloadManager().FetchRepositoriesAsync("Mods/");

            IEnumerable<string> blacklistedBarcodes = File.ReadAllText(AutoDownloadMelon.BlacklistPath)
                                                          .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                                          .Where(line => !line.StartsWith("#"));

            IEnumerable<string> updatingBarcodes = File.ReadAllText(AutoDownloadMelon.UpdatePath)
                                                          .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                                          .Where(line => !line.StartsWith("#"));

            Msg(updatingBarcodes.Count());

            foreach (ModRepository modRepo in fetchedRepos)
            {
                AddRepo(modRepo, blacklistedBarcodes, updatingBarcodes);
            }
            AllMods = Barcode2Mod.Values.ToArray();
            Msg(Barcode2Mod.Count.ToString() + " Downloadable Mods!");

            // Manage Auto-Update
            AutoDownloadMelon.UnityThread.Enqueue(() =>
            {
                foreach (ModWrapper mod in Barcode2Mod.Values.Where(mod => mod.Installed))
                    mod.TryUpdate();
            });
        }
        public static void AddRepo(ModRepository repo, IEnumerable<string> blacklisted, IEnumerable<string> updating) // U
        {
            foreach (ModListing mod in repo.Mods)
            {
                if (mod.Targets.ContainsKey(Platform))
                {
                    if (mod.Targets[Platform].TryCast<DownloadableModTarget>() != null)
                        AddMod(mod, mod.Targets[Platform], blacklisted, updating);
                }
            }
        }
        public static void AddMod(ModListing mod, ModTarget modTarget, IEnumerable<string> blacklisted, IEnumerable<string> updating) // U
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

            newWrapper.Blocked = blacklisted.Contains(newWrapper.Barcode);
            newWrapper.AutoUpdate = newWrapper.Installed && updating.Contains(newWrapper.Barcode);
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

        public static void OnCrateComplete(string crateBarcode) // !U
        {
            AutoDownloadMelon.UnityThread.Enqueue(() =>
            {
                Msg(crateBarcode);
                var crate = GetPalletBarcode(crateBarcode);
                if (crate.HasValue)
                    if (Barcode2Mod.TryGetValue(crate.Value.Item1, out ModWrapper mod))
                        mod.OnCrateComplete(crateBarcode);
            });
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

        public class SmallCrate
        {
            public string CrateTitle;
            public string CrateDescription;
            public string CrateType;
        }
        public static void GetPalletFromURL(string manifestURL, Action<List<SmallCrate>> onDownload)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(manifestURL);
            UnityWebRequestAsyncOperation req = webRequest.SendWebRequest();
            req.m_completeCallback += new Action<AsyncOperation>(operation =>
            {
                try
                {
                    JObject jsonObject = JObject.Parse(webRequest.downloadHandler.text);
                    JArray cratesArray = (JArray)jsonObject["objects"]["o:1"]["crates"];

                    List<SmallCrate> foundCrates = new List<SmallCrate>();

                    foreach (JObject crate in cratesArray)
                    {
                        string refId = crate["ref"].ToString();
                        string crateTitle = jsonObject["objects"][refId]["title"].ToString();
                        string crateDescription = jsonObject["objects"][refId]["description"].ToString();

                        string typeId = crate["type"].ToString();
                        string typeName = jsonObject["types"][typeId]["fullname"].ToString();

                        foundCrates.Add(new SmallCrate()
                        {
                            CrateTitle = crateTitle,
                            CrateDescription = crateDescription,
                            CrateType = typeName
                        });
                    }

                    AutoDownloadMelon.UnityThread.Enqueue(() => onDownload(foundCrates));
                }
                catch (Exception) { }
            });
        }

        [HarmonyPatch(typeof(SceneStreamer), nameof(SceneStreamer.Load), typeof(LevelCrateReference), typeof(LevelCrateReference))]
        public class AsyncPatch
        {
            private static bool s_clearMods = false;
            private static void Postfix(LevelCrateReference level, LevelCrateReference loadLevel)
            {
                s_clearMods = !s_clearMods;
                if (s_clearMods)
                    return;

                if (NetworkInfo.IsClient || NetworkInfo.IsServer)
                    return;

                // if not modded
                bool loadingModded = level.Barcode.ToString().Contains(".Level.");
                foreach (ModWrapper mod in RepoWrapper.AllMods)
                {
                    if (mod.Installed && !mod.Keeping && !level.Barcode.ToString().StartsWith(mod.Barcode))
                    {
                        mod.Installed = false;
                        AssetWarehouse.Instance.UnloadCrate(mod.Barcode);
                        try
                        {
                            Directory.Delete(Path.Combine(MarrowSDK.RuntimeModsPath, mod.Barcode), true);
                        }
                        catch { }

                        try
                        {
                            Directory.Delete(Path.Combine(Directory.GetParent(MarrowSDK.RuntimeModsPath).FullName, "Mods_Autodownloaded", mod.Barcode), true);
                        }
                        catch { }
                    }
                }
            }
        }

        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

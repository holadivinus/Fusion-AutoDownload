using Cysharp.Threading.Tasks;
using HarmonyLib;
using Jevil;
using LabFusion.Data;
using MelonLoader;
using MelonLoader.TinyJSON;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnhollowerBaseLib;
using UnityEngine;
using UnityEngine.Networking;
using static Il2CppSystem.Globalization.CultureInfo;
using Il2Cpp = Il2CppSystem.Collections.Generic;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Security.Util;

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

            foreach (ModRepository modRepo in fetchedRepos)
            {
                AddRepo(modRepo);
            }
            AllMods = Barcode2Mod.Values.ToArray();
            Msg(Barcode2Mod.Count.ToString() + " Downloadable Mods!");

            // Manage Auto-Update
            AutoDownloadMelon.UnityThread.Enqueue(() =>
            {
                foreach (ModWrapper mod in Barcode2Mod.Values.Where(mod => mod.Installed))
                    mod.TryUpdate();
                DownloadManager.StartDownload(Barcode2Mod["EXODUSKS.MilesFutureSuit"]);
            });
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

        public static void OnCrateComplete(string crateBarcode) // U
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

        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

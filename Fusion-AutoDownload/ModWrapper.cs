using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SLZ.Marrow;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using UnityEngine.Networking;
using MelonLoader;

namespace FusionAutoDownload
{
    public class ModWrapper
    {
        public ModWrapper(ModListing originalListing, ModTarget modTarget) 
        {
            ModListing = originalListing;
            ModTarget = modTarget;
            DownloadableModTarget = ModTarget.Cast<DownloadableModTarget>();

            Keeping = Installed = Directory.Exists(Path.Combine(MarrowSDK.RuntimeModsPath, Barcode));

            if (AutoDownloadMelon.ModSettings.TryGetValue(Barcode, out ModSettings settings))
            {
                Blocked = settings.Blocked;
                AutoUpdate = settings.AutoUpdate;
            }
            else AutoUpdate = AutoDownloadMelon.WillUpdateDefault;
        }

        public ModListing ModListing;
        public string Barcode { get => ModListing.Barcode.ID; }
        public string Version { get => ModListing.Version; }

        public ModTarget ModTarget;
        public DownloadableModTarget DownloadableModTarget;
        public Sprite Thumbnail;
        public string Url { get => DownloadableModTarget.Url; }

        public bool Installed;
        public bool Downloading;
        public bool Keeping;

        public bool NeedsSave { get => Blocked || ((AutoUpdate != AutoDownloadMelon.WillUpdateDefault) && Installed) ; }
        public bool Blocked;
        public bool AutoUpdate;

        public struct ModSettings
        {
            public bool Blocked;
            public bool AutoUpdate;
        }

        public void TryUpdate()
        {
            if (!AutoUpdate)
                return;

            try
            {
                string oldJson = File.ReadAllText(Path.Combine(MarrowSDK.RuntimeModsPath, Barcode, "pallet.json"));

                JObject jsonObject = JObject.Parse(oldJson);
                string crateVersion = jsonObject["objects"]["o:1"]["version"].Value<string>();

                bool needsUpdate = false;

                if (int.TryParse(Version, out int l) && int.TryParse(crateVersion, out int r))
                    needsUpdate = l > r;
                if (!needsUpdate)
                    needsUpdate = new Version(Version) > new Version(crateVersion);

                if (needsUpdate)
                {
                    string folderPath = Path.Combine(MarrowSDK.RuntimeModsPath, Barcode);
                    DirectoryInfo directory = new DirectoryInfo(folderPath);

                    AssetWarehouse.Instance.UnloadPallet(Barcode);

                    foreach (FileInfo file in directory.GetFiles())
                        file.Delete();

                    foreach (DirectoryInfo subdirectory in directory.GetDirectories())
                        subdirectory.Delete(true);
                    
                    Installed = false;

                    AutoDownloadMelon.UnityThread.Enqueue(() =>
                    {
                        TryDownload();
                        Msg("Updating!!");
                    });
                }
            } catch(Exception ex) 
            {
                Msg("Failed to compare versions:");
                Msg(ex);
            }
        }

        public void TryDownload(Action onTried = null) // U
        {
            if (!Downloading && !Installed && !Blocked)
            {
                Action onReady = () =>
                {
                    Downloading = true;

                    Msg("Downloading mod: " + Barcode);
                    RepoWrapper.DownloadingMods.Add(Barcode, this);
                    DownloadManager.StartDownload(this);

                    onTried?.Invoke();
                };
                if (AutoDownloadMelon.ModSizeLimit != -1)
                    RepoWrapper.GetURLFileSize(Url, (bytes) =>
                    {
                        if (bytes != -1 && ((bytes / 1e+6f) < AutoDownloadMelon.ModSizeLimit))
                            onReady.Invoke();
                    });
                else onReady.Invoke();
            }
        }

        public void OnDownloadProgress(UnityWebRequest uwr) // U
        {
            Progress = uwr.downloadProgress;
            Percent = Mathf.RoundToInt(uwr.downloadProgress * 100).ToString() + '%';
            MB = $"{Mathf.RoundToInt(uwr.downloadedBytes / 1e+6f)}mb / {Mathf.RoundToInt((uwr.downloadedBytes / 1e+6f) / uwr.downloadProgress)}mb";
            Msg($"{Barcode}: " + MB);
        }

        public float Progress;
        public string Percent;
        public string MB;

        public Action<string> CrateComplete = delegate { };
        public void OnCrateComplete(string crateBarcode) // U
        {
            if (Downloading && !Installed)
            {
                Downloading = false;
                Installed = true;

                if (RepoWrapper.DownloadingMods.ContainsKey(Barcode))
                    RepoWrapper.DownloadingMods.Remove(Barcode);
            }

            CrateComplete.Invoke(crateBarcode);
        }

        // Traditional overrides
        public static bool operator > (ModWrapper left, ModWrapper right) 
        {
            if (int.TryParse(left.Version, out int l) && int.TryParse(right.Version, out int r))
            {
                return l > r;
            }
            try
            {
                return new Version(left.Version) > new Version(right.Version);
            } catch (Exception)
            { 
                return false; 
            }
        }
        public static bool operator < (ModWrapper left, ModWrapper right) => right > left;
        public static bool operator == (ModWrapper left, ModWrapper right)
        {
            if (right is null)
                return left is null;
            if (left is null) 
                return right is null;

            if (int.TryParse(left.Version, out int l) && int.TryParse(right.Version, out int r))
            {
                return l == r;
            }
            try
            {
                return new Version(left.Version) == new Version(right.Version);
            }
            catch (Exception)
            {
                return true;
            }
        }
        public static bool operator != (ModWrapper left, ModWrapper right) => !(left == right);
        public override string ToString() => ModListing.ToString();
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj)
        {
            if (obj is ModWrapper)
                return this == (obj as ModWrapper);
            else return false;
        }

        private void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

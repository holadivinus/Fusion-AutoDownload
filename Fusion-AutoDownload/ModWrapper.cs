using Cysharp.Threading.Tasks;
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
using UnityEngine;
using UnityEngine.Networking;

namespace FusionAutoDownload
{
    public class ModWrapper
    {
        public ModWrapper(ModListing originalListing, ModTarget modTarget) 
        {
            ModListing = originalListing;
            ModTarget = modTarget;
            DownloadableModTarget = ModTarget.Cast<DownloadableModTarget>();

            Installed = Directory.Exists(Path.Combine(MarrowSDK.RuntimeModsPath, Barcode));
        }

        public ModListing ModListing;
        public string Barcode { get => ModListing.Barcode.ID; }
        public string Version { get => ModListing.Version; }

        public ModTarget ModTarget;
        public DownloadableModTarget DownloadableModTarget;
        public string Url { get => DownloadableModTarget.Url; }

        public bool Installed;
        public bool Downloading;
        public bool Blocked;

        public void TryDownload() // U
        {
            if (!Downloading && !Installed && !Blocked)
            {
                Downloading = true;

                Msg("Downloading mod: " + Barcode);
                RepoWrapper.DownloadingMods.Add(Barcode, this);
                AutoDownloadMelon.NewModDownloadManager.DownloadMod(ModListing, ModTarget);
            }
        }

        public void OnDownloadProgress(UnityWebRequest uwr, float progress) // !U
        {
            Progress = progress;
            _downloadedMB = uwr.downloadedBytes / 1e+6f;
        }

        public volatile float Progress; // ?
        private volatile float _downloadedMB; // ?
        public string Percent;
        public string MB;

        public void CacheDownloadProgressStrings()
        {
            Percent = Mathf.RoundToInt(Progress * 100).ToString() + '%';
            MB = $"{Mathf.RoundToInt(_downloadedMB)}mb / {Mathf.RoundToInt(_downloadedMB / Progress)}mb";

            Msg(MB + " " + Barcode);
        }

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

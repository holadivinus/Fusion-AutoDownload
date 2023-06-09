using Il2CppMono.Globalization.Unicode;
using MelonLoader;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FusionAutoDownload
{
    static class DownloadManager
    {
        public static void StartDownload(ModWrapper modWrapper) => MelonCoroutines.Start(ModDownload(modWrapper));

        static IEnumerator ModDownload(ModWrapper mod)
        {
            UnityWebRequest uwr = UnityWebRequest.Get(mod.Url);

            string downloadingModsPath = Path.Combine(MarrowSDK.InstallStagingPath, "FusionAutoDownloads");
            if (!Directory.Exists(downloadingModsPath))
                Directory.CreateDirectory(downloadingModsPath);

            string modPath = Path.Combine(downloadingModsPath, "Pallet." + mod.Barcode + ".zip");
            if (File.Exists(modPath))
                File.Delete(modPath);

            uwr.downloadHandler = new DownloadHandlerFile(modPath);

            //Start the download
            yield return uwr.SendWebRequest();

            while (!uwr.isDone)
            {
                yield return null;
                mod.OnDownloadProgress(uwr);
            }

            if (uwr.result == UnityWebRequest.Result.Success)
            {
                AssetWarehouse.Instance.UnloadPallet(mod.Barcode);
                string b = Path.Combine(MarrowSDK.RuntimeModsPath, mod.Barcode) + '\\';
                ExtractPalletFolderFromZip(modPath, b);
                AutoDownloadMelon.UnityThread.Enqueue(() => 
                { 
                    AssetWarehouse.Instance.ReloadPallet(mod.Barcode);
                    AssetWarehouse.Instance.LoadPalletFromFolderAsync(b, true);
                    Msg($"Download of {mod.Barcode} Complete!");
                });
            }
            else
            {
                Msg("Error while downloading: " + uwr.error);
            }
            
        }


        public static void ExtractPalletFolderFromZip(string zipPath, string destinationPath)
        {
            Msg(zipPath);
            Msg(destinationPath);

            // Create the destination directory if it does not exist
            Directory.CreateDirectory(destinationPath);

            // Open the zip archive
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry palletJson = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("pallet.json"));
                if (palletJson != null)
                {
                    string zipPalletRootPath = palletJson.FullName.Substring(0, palletJson.FullName.Length - 11);
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        Msg("a");
                        if (!entry.FullName.StartsWith(zipPalletRootPath) || entry.FullName.EndsWith("/"))
                            continue;

                        Msg("b");
                        string filePath = Path.Combine(destinationPath, entry.FullName.Substring(zipPalletRootPath.Length));
                        Msg(filePath);

                        Msg("c");
                        Directory.CreateDirectory(filePath.Substring(0, filePath.LastIndexOf('/')));

                        Msg("d");
                        entry.ExtractToFile(filePath);

                        Msg("e");
                    }
                }
            }
            Msg("Out!");
            File.Delete(zipPath);
        }

        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

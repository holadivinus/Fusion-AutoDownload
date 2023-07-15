using Il2CppMono.Globalization.Unicode;
using MelonLoader;
using SLZ.Marrow;
using SLZ.Marrow.Warehouse;
using Steamworks.Ugc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static SLZ.AI.TriggerManager;

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

            string modZipPath = Path.Combine(downloadingModsPath, "Pallet." + mod.Barcode + ".zip");
            if (File.Exists(modZipPath))
                File.Delete(modZipPath);

            uwr.downloadHandler = new DownloadHandlerFile(modZipPath);

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
                string unZippedPath = Path.Combine(Directory.GetParent(MarrowSDK.RuntimeModsPath).FullName, "Mods_Autodownloaded", mod.Barcode) + '\\';
                string symLinkPath = Path.Combine(MarrowSDK.RuntimeModsPath, mod.Barcode) + '\\';
                if (Directory.Exists(unZippedPath))
                {
                    Msg("ERROR HERE???? 1");
                    AssetWarehouse.Instance.UnloadPallet(mod.Barcode);
                    try
                    {
                        Directory.Delete(unZippedPath, true);
                    }catch(Exception ex)
                    {
                        Msg("Yes, error: " + ex.ToString());
                    }
                }
                if (Directory.Exists(symLinkPath))
                {
                    Msg("ERROR HERE???? 2");
                    try
                    {
                        Directory.Delete(symLinkPath, true);
                    }catch (Exception e)
                    {
                        Msg("Yes, error: " + e.ToString());
                    }
                }

                ExtractPalletFolderFromZipAsync(modZipPath, unZippedPath, () => 
                {
                    Msg("A");
                    AutoDownloadMelon.UnityThread.Enqueue(() =>
                    {
                        Msg($"unZippedPath: {unZippedPath}, symLinkPath: {symLinkPath}");
                        var psi = new ProcessStartInfo("cmd.exe", " /C mklink /J \"" + symLinkPath + "\" \"" + unZippedPath + "\"");
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;
                        Process.Start(psi).WaitForExit();

                        AssetWarehouse.Instance.ReloadPallet(mod.Barcode);
                        AssetWarehouse.Instance.LoadPalletFromFolderAsync(symLinkPath, true);
                        mod.AutoUpdate = AutoDownloadMelon.WillUpdateDefault;
                        mod.Keeping = !AutoDownloadMelon.WillDeleteDefault;
                        Msg($"Download of {mod.Barcode} Complete!");
                    });
                });
            }
            else
            {
                Msg("Error while downloading: " + uwr.error);
            }
        }


        public static void ExtractPalletFolderFromZipAsync(string zipPath, string destinationPath, Action onCompleteNON_U)
        {
            new Thread(() => 
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
                            if (!entry.FullName.StartsWith(zipPalletRootPath) || entry.FullName.EndsWith("/"))
                                continue;

                            string filePath = Path.Combine(destinationPath, entry.FullName.Substring(zipPalletRootPath.Length));
                            int lastSlash = Math.Max(filePath.LastIndexOf('/'), filePath.LastIndexOf('\\'));
                            Directory.CreateDirectory(filePath.Substring(0, lastSlash));

                            entry.ExtractToFile(filePath, true);
                        }
                    }
                }
                File.Delete(zipPath);

                onCompleteNON_U?.Invoke();
            }).Start();
        }

        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

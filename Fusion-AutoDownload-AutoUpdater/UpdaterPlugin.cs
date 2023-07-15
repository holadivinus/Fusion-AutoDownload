using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

using MelonLoader;
using Newtonsoft.Json;

namespace AutoDownloadAutoUpdater
{
    public class UpdaterPlugin : MelonPlugin
    {
        private const string GitHubApiUrl = @"https://api.github.com/repos/holadivinus/Fusion-AutoDownload/releases/latest";

        public override void OnPreModsLoaded()
        {
            string modsFolder = Path.Combine(MelonUtils.GameDirectory, "Mods");

            string[] modPaths = Directory.GetFiles(modsFolder);

            foreach (string modPath in modPaths) 
            {
                FileVersionInfo modInfo = FileVersionInfo.GetVersionInfo(modPath);
                if (modInfo.FileDescription == "Fusion-AutoDownload")
                {
                    updateMod(modPath, modInfo.FileVersion);
                    break;
                }
            }
        }

        private void updateMod(string modPath, string modVersion)
        {
            modVersion = modVersion.Substring(2);

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AcmeInc/1.0)");

                HttpResponseMessage response = httpClient.GetAsync(GitHubApiUrl).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    MelonLogger.Msg("Failed to Autoupdate: Github failed to respond.");
                    return;
                }
                
                string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                GitHubRelease release = JsonConvert.DeserializeObject<GitHubRelease>(responseText);

                // Tags will allways be version format
                try
                {
                    if (new Version(modVersion) >= new Version(release.tag_name))
                    {
                        MelonLogger.Msg($"Autoupdate not needed! Local Version: ({modVersion}), Remote Version: ({release.tag_name})");
                        return;
                    }

                    // Updating needed!
                    MelonLogger.Msg($"Autoupdate needed! Local Version: ({modVersion}), Remote Version: ({release.tag_name})");

                    foreach (GitHubAsset asset in release.assets)
                    {
                        if (asset.name == "Fusion-AutoDownload.dll")
                        {
                            MelonLogger.Msg("Downloading update from: " + asset.browser_download_url);

                            using (HttpResponseMessage downloadResponse = httpClient.GetAsync(asset.browser_download_url).GetAwaiter().GetResult())
                            {
                                if (!downloadResponse.IsSuccessStatusCode)
                                {
                                    MelonLogger.Msg("Update Failed! Couldn't download from the previously mentioned URL.");
                                    return;
                                }

                                File.Delete(modPath);
                                using 
                                (
                                    Stream contentStream = downloadResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult(),
                                    stream = new FileStream(modPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true)
                                )
                                {
                                    contentStream.CopyToAsync(stream).GetAwaiter().GetResult();
                                    MelonLogger.Msg("Mod updated correctly!");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) 
                { 
                    MelonLogger.Msg("Failed to Autoupdate:\n" + ex.ToString()); 
                }
                
            }
        }

        public class GitHubRelease
        {
            public string tag_name { get; set; }
            public List<GitHubAsset> assets { get; set; }
        }

        public class GitHubAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }
    }
}

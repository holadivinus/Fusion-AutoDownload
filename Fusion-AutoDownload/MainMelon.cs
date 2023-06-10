using System;
using System.Collections.Generic;

using SLZ.Marrow.Forklift;
using SLZ.Marrow.Warehouse;

using MelonLoader;
using System.IO;
using System.Collections.Concurrent;
using UnityEngine.SceneManagement;
using FusionAutoDownload.Download_UI_Classes;
using SLZ.Marrow;
using Newtonsoft.Json;
using SLZ.Marrow.Forklift.Model;
using System.Net.Http;
using System.Security.Policy;
using System.Threading;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon : MelonMod
    {
        #region Saved Preferences
        private static MelonPreferences_Category s_preferences = MelonPreferences.CreateCategory("Fusion-Autodownloader");

        public class ModsSettings : Dictionary<string, ModWrapper.ModSettings> { }
        private static MelonPreferences_Entry<string[]> s_modsSettings = s_preferences.CreateEntry("ModsSettings", new string[] { });
        public static ModsSettings ModSettings;

        private static MelonPreferences_Entry<int> s_modSizeLimit = s_preferences.CreateEntry("ModSizeLimit", -1);
        public static int ModSizeLimit { get => s_modSizeLimit.Value; set => s_modSizeLimit.Value = value; }

        private static MelonPreferences_Entry<bool> s_willDeleteDefault = s_preferences.CreateEntry("WillDeleteDefault", true);
        public static bool WillDeleteDefault { get => s_willDeleteDefault.Value; set => s_willDeleteDefault.Value = value; }

        private static MelonPreferences_Entry<bool> s_willUpdateDefault = s_preferences.CreateEntry("WillUpdateDefault", true);
        public static bool WillUpdateDefault { get => s_willUpdateDefault.Value; set => s_willUpdateDefault.Value = value; }
        #endregion 

        public override void OnEarlyInitializeMelon()
        {
            // Add Default repositories.txt
            string modDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modDirectory, "repositories.txt");
            string contentToWrite = "https://blrepo.laund.moe/repository.json";
            Msg(filePath);

            if (!File.Exists(filePath))
            {
                // Create the file if it doesn't exist
                using (StreamWriter writer = File.CreateText(filePath))
                {
                    writer.Write(contentToWrite);
                    Msg("repo file not found, default for modio created!");
                }
            }

            // Parse saved ModSettings, cant use any normal serialization or deserialization for garbage reasons
            ModSettings = new ModsSettings();
            foreach (string setting in s_modsSettings.Value)
            {
                string[] sets = setting.Split(',');
                ModWrapper.ModSettings curSettings = new ModWrapper.ModSettings();
                for (int i = 0; i < sets.Length; i++)
                {
                    switch (i)
                    {
                        case 1:
                            if (bool.TryParse(sets[i], out bool blocked))
                                curSettings.Blocked = blocked;
                            break;
                        case 2:
                            if (bool.TryParse(sets[i], out bool updSave))
                                curSettings.AutoUpdate = updSave;
                            else curSettings.AutoUpdate = WillDeleteDefault;
                            break;
                    }
                }
                ModSettings.Add(sets[0], curSettings);
            }
        }

        public override void OnLateInitializeMelon()
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.7)")
            .PatchAll();

            RepoWrapper.FetchRepos();


            // On Warehouse Ready -> OnCrateAdded -> RepoWrapper.OnCrateComplete
            AssetWarehouse.OnReady(new Action(() => AssetWarehouse.Instance.OnCrateAdded += (Action<string>)RepoWrapper.OnCrateComplete)); // < !U

            AutoDownloadMenu.Setup();
        }
        public static void Msg(object msg)
        {
#if DEBUG
            //MelonLogger.Msg(new System.Diagnostics.StackTrace());
            MelonLogger.Msg(msg);
#endif
        }

        public static ConcurrentQueue<Action> UnityThread = new ConcurrentQueue<Action>();
        public override void OnLateUpdate()
        {
            if (UnityThread.Count > 0)
                if (UnityThread.TryDequeue(out Action code))
                    code.Invoke();
        }

        public override void OnApplicationQuit()
        {
            List<string> modSettingsSave = new List<string>();

            foreach (ModWrapper mod in RepoWrapper.AllMods)
            {
                if (mod.Installed && !mod.Keeping)
                {
                    Directory.Delete(Path.Combine(MarrowSDK.RuntimeModsPath, mod.Barcode), true);
                }

                if (mod.NeedsSave) 
                {
                    ModWrapper.ModSettings settings;

                    if (!ModSettings.TryGetValue(mod.Barcode, out settings))
                        ModSettings.Add(mod.Barcode, settings = new ModWrapper.ModSettings());

                    settings.Blocked = mod.Blocked;
                    settings.AutoUpdate = mod.AutoUpdate;

                    modSettingsSave.Add($"{mod.Barcode},{mod.Blocked},{mod.AutoUpdate}");
                }
            }
            s_modsSettings.Value = modSettingsSave.ToArray();
        }
    }
}

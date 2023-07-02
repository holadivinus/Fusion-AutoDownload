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
using LabFusion.Utilities;
using SLZ.Marrow.SceneStreaming;
using TMPro;
using UnityEngine;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon : MelonMod
    {
        #region Saved Preferences
        private static MelonPreferences_Category s_preferences = MelonPreferences.CreateCategory("Fusion-Autodownloader");


        private static MelonPreferences_Entry<int> s_modSizeLimit = s_preferences.CreateEntry("ModSizeLimit", -1);
        public static int ModSizeLimit { get => s_modSizeLimit.Value; set => s_modSizeLimit.Value = value; }

        private static MelonPreferences_Entry<bool> s_willDeleteDefault = s_preferences.CreateEntry("WillDeleteDefault", true);
        public static bool WillDeleteDefault { get => s_willDeleteDefault.Value; set => s_willDeleteDefault.Value = value; }

        private static MelonPreferences_Entry<bool> s_willUpdateDefault = s_preferences.CreateEntry("WillUpdateDefault", true);
        public static bool WillUpdateDefault { get => s_willUpdateDefault.Value; set => s_willUpdateDefault.Value = value; }

        private static MelonPreferences_Entry<string[]> s_modsLastDownloadLinks = s_preferences.CreateEntry("ModsLastDownloadLinks", new string[] { });
        public static string[] ModsLastDownloadLinks { get => s_modsLastDownloadLinks.Value; set => s_modsLastDownloadLinks.Value = value; }

        public static string BlacklistPath;
        public static string UpdatePath;
        #endregion 

        public override void OnEarlyInitializeMelon()
        {
            // Add Default repositories.txt
            string modDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(modDirectory, "repositories.txt");
            string contentToWrite = "https://blrepo.laund.moe/repository.json";

            if (!File.Exists(filePath))
            {
                // Create the file if it doesn't exist
                using (StreamWriter writer = File.CreateText(filePath))
                {
                    writer.Write(contentToWrite);
                    Msg("repo file not found, default for modio created!");
                }
            }

            // Add Blacklist file
            string userdataPath = Path.Combine(Directory.GetParent(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)).FullName, "UserData");
            BlacklistPath = Path.Combine(userdataPath, "AutoDownload-BLACKLIST.txt");
            UpdatePath = Path.Combine(userdataPath, "AutoDownload-UPDATE.txt");


            if (!File.Exists(BlacklistPath))
                using (StreamWriter writer = File.CreateText(BlacklistPath))
                    writer.Write("# Blacklisted Barcodes go here.");

            if (!File.Exists(UpdatePath))
                using (StreamWriter writer = File.CreateText(UpdatePath))
                    writer.Write("# Updating Barcodes go here.");
        }

        public override void OnLateInitializeMelon()
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.9)")
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
            using (StreamWriter writer = File.CreateText(BlacklistPath))
            {
                writer.WriteLine("# Blacklisted Barcodes go here.");
                foreach (ModWrapper mod in RepoWrapper.AllMods)
                    if (mod.Blocked)
                        writer.WriteLine(mod.Barcode);
            }

            using (StreamWriter writer = File.CreateText(UpdatePath))
            {
                writer.WriteLine("# Auto-Updating Barcodes go here.");
                foreach (ModWrapper mod in RepoWrapper.AllMods)
                    if (mod.AutoUpdate)
                        writer.WriteLine(mod.Barcode);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using Cysharp.Threading.Tasks;
using SLZ.Marrow.Forklift;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;

using Il2Cpp = Il2CppSystem.Collections.Generic;
using MelonLoader;
using System.IO;
using LabFusion.Data;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Linq;
using UnityEngine.Networking;
using System.Collections.Concurrent;
using static FusionAutoDownload.UIClasses.MenuMapUI;
using UnityEngine.SceneManagement;
using SLZ.Marrow.SceneStreaming;
using static FusionAutoDownload.Download_UI_Classes.LevelLoadUI;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon : MelonMod
    {
        /// <summary>
        /// Creates a new ModDownloadManager for every query; A one time use class
        /// </summary>
        public static ModDownloadManager NewModDownloadManager { get => new ModDownloadManager(); }

        public override void OnLateInitializeMelon() // U
        {
            new HarmonyLib.Harmony($"Holadivinus.{nameof(AutoDownloadMelon)}.(0.0.6)")
            .PatchAll();

            RepoWrapper.ModDownloadProgress_Patch.Patch();

            RepoWrapper.FetchRepos();
            
            // On Warehouse Ready -> OnCrateAdded -> RepoWrapper.OnCrateComplete
            AssetWarehouse.OnReady(new Action(() => AssetWarehouse.Instance.OnCrateAdded += (Action<string>)RepoWrapper.OnCrateComplete)); // < !U

            SceneManager.activeSceneChanged += new Action<Scene, Scene>((a, b) => 
            {
                Msg("From: " + a.name);
                Msg("To: " + b.name);
            });
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
            RepoWrapper.OnLateUpdate();

            if (UnityThread.Count > 0)
                if (UnityThread.TryDequeue(out Action code))
                    code.Invoke();

            if (Input.GetKeyDown(KeyCode.Y))
            {
                Msg("Y DOWN!!");
                SceneStreamer.Session.End();
                SceneStreamer.Load(SceneManager_LoadSceneAsync_Patch.TargetServerScene);
            }
        }
    }
}

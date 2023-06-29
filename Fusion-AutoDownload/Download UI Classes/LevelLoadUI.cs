using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine;
using HarmonyLib;
using LabFusion.Utilities;
using LabFusion.Network;
using System.Reflection;
using SLZ.Marrow;
using SLZ.Marrow.SceneStreaming;
using System.IO.Pipes;
using SLZ.Marrow.Warehouse;
using LabFusion.Patching;
using MelonLoader;
using Il2CppMono.Globalization.Unicode;

namespace FusionAutoDownload.Download_UI_Classes
{
    public class LevelLoadUI : ProgressUI
    {
        public static LevelLoadUI CurrentLoading;

        public LevelLoadUI(ModWrapper targetMod, string barcode) : base()
        {
            _mod = targetMod; CrateBarcode = barcode;

            CurrentLoading = this;


            _curNotif = new FusionNotification()
            {
                title = "Downloading custom map!",
                isMenuItem = false,
                isPopup = true,
                message = $"Downloading \"{barcode}\" from the host!",
                popupLength = 15
            };
            FusionNotifier.Send(_curNotif);
            _updateWait = _curNotif.popupLength + 1;
        }

        FusionNotification _curNotif;
        float _updateWait;

        protected override void OnMyCrateAdded()
        {

            if (CurrentLoading == this)
            {
                SceneLoadPatch.IgnorePatches = true;
                SceneStreamer.Load(FusionSceneManager_Internal_UpdateTargetScene_Patch._targetServerScene);
                SceneLoadPatch.IgnorePatches = false;
                CurrentLoading = null;
            }
        }

        protected override IEnumerator UpdateLoop()
        {
            while (true)
            {
                if (CurrentLoading != this)
                    break;
                yield return null;

                if (_updateWait <= 0)
                {
                    _updateWait += 1.5f;
                    _curNotif.title = "Downloading custom map at " + _mod.MB;
                    _curNotif.message = $"\"{CrateBarcode}\" {_mod.Percent}";
                    _curNotif.popupLength = 1;
                    FusionNotifier.Send(_curNotif);
                }
                if (!NetworkInfo.IsClient)
                    CurrentLoading = null;
                _updateWait -= Time.deltaTime;
            }
        }

        // Activation Patching
        [HarmonyPatch(typeof(FusionSceneManager), "Internal_UpdateTargetScene")]
        public class FusionSceneManager_Internal_UpdateTargetScene_Patch
        {
            private static readonly FieldInfo _targetServerScene_getter = AccessTools.Field(typeof(FusionSceneManager), "_targetServerScene");
            private static readonly FieldInfo _hasStartedLoadingTarget_getter = AccessTools.Field(typeof(FusionSceneManager), "_hasStartedLoadingTarget");
            public static string _targetServerScene { get => (string)_targetServerScene_getter.GetValue(null); set => _targetServerScene_getter.SetValue(null, value); }
            public static bool _hasStartedLoadingTarget { get => (bool)_hasStartedLoadingTarget_getter.GetValue(null); set => _hasStartedLoadingTarget_getter.SetValue(null, value); }


            [HarmonyPrefix]
            public static bool PreFix()
            {
                if (!(FusionSceneManager.IsDelayedLoadDone() && !_hasStartedLoadingTarget && !string.IsNullOrEmpty(_targetServerScene)))
                    return true;

                var palletBarcode = RepoWrapper.GetPalletBarcode(_targetServerScene);
                if (palletBarcode.HasValue && RepoWrapper.Barcode2Mod.TryGetValue(palletBarcode.Value.Item1, out ModWrapper mod))
                {
                    mod.TryDownload(() => 
                    { 
                        new LevelLoadUI(mod, palletBarcode.Value.Item1);
                        _hasStartedLoadingTarget = true;
                    });
                }
                return true;
            }
        }
        public static void Msg(object msg) => AutoDownloadMelon.Msg(msg);
    }
}

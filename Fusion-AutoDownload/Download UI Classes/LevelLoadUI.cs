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

namespace FusionAutoDownload.Download_UI_Classes
{
    public class LevelLoadUI : ProgressUI
    {
        protected override void OnMyCrateAdded()
        {
            
        }

        protected override IEnumerator UpdateLoop()
        {
            yield return null;
        }


        [HarmonyPatch(typeof(SceneManager), "LoadSceneAsync", new Type[] { typeof(string), typeof(LoadSceneParameters) })]
        public class SceneManager_LoadSceneAsync_Patch
        {
            private static readonly FieldInfo _targetServerScene_getter = AccessTools.Field(typeof(FusionSceneManager), "_targetServerScene");
            public static string TargetServerScene { get => (string)_targetServerScene_getter.GetValue(null); set => _targetServerScene_getter.SetValue(null, value); }
            [HarmonyPostfix]
            private static void Postfix(ref AsyncOperation __result)
            {
                if (NetworkInfo.IsClient)
                {
                    var palletBarcode = RepoWrapper.GetPalletBarcode(TargetServerScene);
                    if (palletBarcode.HasValue)
                    {
                        if (RepoWrapper.Barcode2Mod.TryGetValue(palletBarcode.Value.Item1, out ModWrapper mod))
                        {
                            mod.TryDownload();
                            if (mod.Downloading)
                            {
                                __result = null;
                                
                            }
                        }
                    }
                }
            }
        }
    }
}

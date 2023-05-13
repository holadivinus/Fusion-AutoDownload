using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using SLZ.Marrow.Data;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;

using HarmonyLib;
using BoneLib.Nullables;
using LabFusion.Network;
using UnityEngine.Networking;
using TMPro;
using LabFusion.Extensions;
using LabFusion.Data;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon
    {
        public static List<PendingSpawnable> WaitingSpawnables = new List<PendingSpawnable>();
        public static void OnSpawnableCrateAdded(string barcode)
        {
            PendingSpawnable[] pendingSpawnables = WaitingSpawnables.Where(ps => ps.Barcode == barcode).ToArray();
            foreach (PendingSpawnable spawnable in pendingSpawnables)
            {
                Msg("Spawnable invoke");
                spawnable.Visible = false;
                spawnable.OnComplete.Invoke();
            }
        }

        public class PendingSpawnable
        {
            public static void OnDownloadProgress(UnityWebRequest uwr, ModListing mod, float progress)
            {
                foreach (PendingSpawnable ps in WaitingSpawnables)
                {
                    if (ps.Barcode.StartsWith(mod.Barcode.ID))
                    {
                        ps.SetData
                        (
                            mod.Barcode.ID,
                            $"{Mathf.RoundToInt(uwr.downloadedBytes / 1e+6f)}mb /{Mathf.RoundToInt((uwr.downloadedBytes / 1e+6f) / progress)}mb",
                            Mathf.RoundToInt(progress * 100).ToString() + "%"
                        );
                        ps.SetWidth(progress);
                    }
                }
            }

            // hacky "fix", set by avatar dl
            public static bool FontFound;
            public static TMP_FontAsset FoundFont;
            public PendingSpawnable(SpawnResponseData data, Action onComplete)
            {
                Barcode = data.barcode; OnComplete = onComplete;
                uiRoot = GameObject.Instantiate(UIAssetSpawnable, data.serializedTransform.position, Quaternion.identity);
                uiRoot.transform.parent = RigData.RigReferences.RigManager.transform.parent;

                CanvasGroup = uiRoot.GetComponent<CanvasGroup>();
                CanvasGroup.alpha = 0.011f;
                Texts = uiRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                downloadBar = uiRoot.transform.GetChild(1).GetChild(0).Cast<RectTransform>();
                if (FontFound)
                    foreach (TextMeshProUGUI text in Texts)
                        text.font = FoundFont;
                SetWidth(0);
                Visible = true;
            }
            public readonly string Barcode;
            public readonly Action OnComplete;

            GameObject uiRoot;
            public bool Visible;
            CanvasGroup CanvasGroup;
            public TextMeshProUGUI[] Texts;
            RectTransform downloadBar;

            public void SetData(string palletBarcode, string percent, string mB)
            {
                Texts[0].text = mB;
                Texts[1].text = percent;
                Texts[2].text = palletBarcode;
            }
            public void SetWidth(float width)
            {
                if (downloadBar != null)
                {
                    Vector2 sizeDelta = downloadBar.sizeDelta;
                    sizeDelta.y = width * 512;
                    downloadBar.sizeDelta = sizeDelta;
                }
            }
            public void OnLateUpdate()
            {
                if (CanvasGroup != null)
                {
                    CanvasGroup.alpha += (Visible ? 1 : -1) * Time.deltaTime;
                    CanvasGroup.alpha = Mathf.Clamp01(CanvasGroup.alpha);
                    uiRoot.transform.LookAtPlayer();

                    if (CanvasGroup.alpha < .001)
                    {
                        
                        DownloadQueue.Enqueue(() => { WaitingSpawnables.Remove(this); UnityEngine.Object.DestroyImmediate(uiRoot); });
                    }
                } else DownloadQueue.Enqueue(() => { WaitingSpawnables.Remove(this); });
            }
        }
        // Patches
        [HarmonyPatch(typeof(SpawnResponseMessage), "HandleMessage", new Type[] { typeof(byte[]), typeof(bool) })]
        class SpawnResponseMessage_HandleMessage_Patch
        {
            private static ushort? s_lastSyncId = null;
            [HarmonyPrefix]
            public static bool Prefix(byte[] bytes, bool isServerHandled)
            {
                if (!isServerHandled)
                {
                    FusionReader reader = FusionReader.Create(bytes);
                    SpawnResponseData data = reader.ReadFusionSerializable<SpawnResponseData>();

                    if (s_lastSyncId == null)
                    {
                        s_lastSyncId = data.syncId;
                    }
                    else
                    {
                        if (s_lastSyncId == data.syncId)
                        {
                            s_lastSyncId = null;
                            return false;
                        }
                        else
                        {
                            s_lastSyncId = null;
                        }
                    }
                    if (AssetWarehouse.Instance.GetCrate<SpawnableCrate>(data.barcode) == null)
                    {
                        string palletBarcode = data.barcode.Substring(0, data.barcode.IndexOf(".Spawnable."));
                        if (AttemptedPallets.Contains(palletBarcode))
                        {
                            ActuallyProcess(data);
                            return false;
                        }
                        AttemptedPallets.Add(palletBarcode);

                        Msg("Spawnable failed to load! " + data.barcode);
                        if (ModListings.TryGetValue(palletBarcode, out (ModListing, ModTarget) foundMod))
                        {
                            Msg("Spawnable Found in some Repo, queued for download!");

                            DownloadQueue.Enqueue(() =>
                            {
                                DownloadingMods.Add(foundMod.Item2.Cast<DownloadableModTarget>().Url, foundMod.Item1);
                                LatestModDownloadManager.DownloadMod(foundMod.Item1, foundMod.Item2);

                                Msg("Creating Spawnable!");
                                PendingSpawnable spawnable = new PendingSpawnable(data, () =>
                                {
                                    Msg("Custom download Spawnable Spawning!!! " + data.barcode);
                                    ActuallyProcess(data);
                                });
                                Msg(spawnable.Barcode);
                                WaitingSpawnables.Add(spawnable);
                            });
                        }
                        else { Msg($"Spawnable not found an any repo. ({data.barcode})"); ActuallyProcess(data); }
                    }
                    else ActuallyProcess(data);
                }
                return false; // sorry original
            }
            public static void ActuallyProcess(SpawnResponseData data)
            {
                var crateRef = new SpawnableCrateReference(data.barcode);

                var spawnable = new Spawnable()
                {
                    crateRef = crateRef,
                    policyData = null
                };
                
                SLZ.Marrow.Pool.AssetSpawner.Register(spawnable);

                byte owner = data.owner;
                string barcode = data.barcode;
                ushort syncId = data.syncId;
                string path = data.spawnerPath;
                var hand = data.hand;

                NullableMethodExtensions.PoolManager_Spawn(spawnable, data.serializedTransform.position, data.serializedTransform.rotation.Expand(), null,
                    true, null, (Action<GameObject>)((go) => {SpawnResponseMessage.OnSpawnFinished(owner, barcode, syncId, go, path, hand);}), null);

            }
        }
    }
}

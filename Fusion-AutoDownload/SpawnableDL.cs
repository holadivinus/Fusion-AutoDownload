using BoneLib.Nullables;
using HarmonyLib;
using LabFusion.Network;
using SLZ.Marrow.Data;
using SLZ.Marrow.Forklift.Model;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerBaseLib;
using UnityEngine;
using Il2Cpp = Il2CppSystem.Collections.Generic;

namespace FusionAutoDownload
{
    public partial class AutoDownloadMelon
    {
        public static List<(string, Action)> WaitingSpawnables = new List<(string, Action)>();
        public static void OnSpawnableCrateAdded(string barcode)
        {
            (string, Action)[] pendingSpawnable = WaitingSpawnables.Where(sp => sp.Item1 == barcode).ToArray();
            foreach ((string, Action) spawnable in pendingSpawnable)
            {
                Msg("Spawnable invoke");
                spawnable.Item2.Invoke();
                WaitingSpawnables.Remove(spawnable);
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
                    using (FusionReader reader = FusionReader.Create(bytes))
                    {
                        using (SpawnResponseData data = reader.ReadFusionSerializable<SpawnResponseData>())
                        {
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

                                Msg("Spawnable failed to load!");
                                if (ModListings.TryGetValue(palletBarcode, out (ModListing, ModTarget) foundMod))
                                {
                                    Msg("Spawnable Found in some Repo, queued for download!");

                                    DownloadQueue.Enqueue(() =>
                                    {
                                        LatestModDownloadManager.DownloadMod(foundMod.Item1, foundMod.Item2);
                                        WaitingSpawnables.Add((data.barcode, () =>
                                        {
                                            Msg("Custom download Spawnable Spawning!!!");
                                            ActuallyProcess(data);
                                        }
                                        ));
                                    });
                                }
                                else { Msg($"Spawnable not found an any repo. ({data.barcode})"); ActuallyProcess(data); }
                            }
                            else ActuallyProcess(data);
                        }
                    }
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
                    true, null, (Action<GameObject>)((go) => { SpawnResponseMessage.OnSpawnFinished(owner, barcode, syncId, go, path, hand); }), null);

            }
        }
    }
}

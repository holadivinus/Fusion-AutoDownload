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
    }

    // Spawnable
    [HarmonyPatch(typeof(SpawnResponseMessage), "HandleMessage", new Type[] { typeof(byte[]), typeof(bool) })]
    class SpawnResponseMessage_HandleMessage_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(byte[] bytes, bool isServerHandled)
        {
            if (!isServerHandled)
            {
                using (FusionReader reader = FusionReader.Create(bytes))
                {
                    using (SpawnResponseData data = reader.ReadFusionSerializable<SpawnResponseData>())
                    {
                        if (AssetWarehouse.Instance.GetCrate<SpawnableCrate>(data.barcode) == null)
                        {
                            string palletBarcode = data.barcode.Substring(0, data.barcode.IndexOf(".Spawnable."));
                            if (AutoDownloadMelon.AttemptedPallets.Contains(palletBarcode))
                            {
                                ActuallyProcess(data);
                                return true;
                            }
                            AutoDownloadMelon.AttemptedPallets.Add(palletBarcode);

                            AutoDownloadMelon.Msg("Spawnable failed to load!");
                            if (AutoDownloadMelon.ModListings.TryGetValue(palletBarcode, out ModListing foundMod))
                            {
                                AutoDownloadMelon.Msg("Spawnable Found in some Repo!");
                                bool isPC = false;
                                foreach (Il2Cpp.KeyValuePair<string, ModTarget> possibleTarg in foundMod.Targets)
                                {
                                    if (possibleTarg.key == "pc")
                                    {
                                        isPC = true;
                                        AutoDownloadMelon.Msg("Spawnable on PC!");
                                        AutoDownloadMelon.DownloadQueue.Enqueue(() =>
                                        {
                                            AutoDownloadMelon.LatestModDownloadManager.DownloadMod(foundMod, possibleTarg.Value);
                                            AutoDownloadMelon.WaitingSpawnables.Add((data.barcode, () =>
                                            {
                                                AutoDownloadMelon.Msg("Custom download Spawnable Spawning!!!");
                                                ActuallyProcess(data);
                                            }
                                            ));
                                        });
                                        break;
                                    }
                                }
                                if (!isPC)
                                    AutoDownloadMelon.Msg("Spawnable not on PC.");
                            }
                            else AutoDownloadMelon.Msg($"Spawnable not found an any repo. ({data.barcode})");
                        }
                        else ActuallyProcess(data);
                    }
                }
            }
            return true; // sorry original
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

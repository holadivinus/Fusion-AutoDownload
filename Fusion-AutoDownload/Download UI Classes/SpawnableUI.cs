using HarmonyLib;
using System;
using UnityEngine;
using LabFusion.Data;
using TMPro;
using LabFusion.Network;
using BoneLib.Nullables;
using SLZ.Marrow.Data;
using SLZ.Marrow.Warehouse;
using LabFusion.Exceptions;
using LabFusion.Extensions;
using System.Collections;

namespace FusionAutoDownload
{
    public class SpawnableUI : ProgressUI
    {
        public static Action FontFix = delegate { };
        public static TMP_FontAsset FixedFont;
        public SpawnableUI(SpawnResponseData data, ModWrapper mod, Action onComplete) : base() // U
        { 
            // Inheritted
            CrateBarcode = data.barcode;

            // !Inheritted (UI GameObject Setup)
            _mod = mod; _onComplete = onComplete; 

            _uiRoot = UnityEngine.Object.Instantiate(UIAssetSpawnable, data.serializedTransform.position, Quaternion.identity);
            _uiRoot.transform.parent = RigData.RigReferences.RigManager.transform.parent;

            _canvasGroup = _uiRoot.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0.00001f;
            _visible = true;

            _texts = _uiRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

            if (FixedFont != null)
                setFont(FixedFont);
            else FontFix += fontFix;

            _texts[2].text = CrateBarcode;

            _downloadBar = _uiRoot.transform.GetChild(1).GetChild(0).Cast<RectTransform>();
            // Completed UI GameObject Setup
        }

        // !Inheritted
        private readonly Action _onComplete;

        private readonly CanvasGroup _canvasGroup;
        private readonly TextMeshProUGUI[] _texts;
        private readonly RectTransform _downloadBar;
        private bool _visible;

        private void fontFix()
        {
            setFont(FixedFont);
            FontFix -= fontFix;
        }
        private void setFont(TMP_FontAsset font)
        {
            foreach (TextMeshProUGUI text in _texts)
                text.font = FixedFont;
        }

        // Inheritted
        protected override IEnumerator UpdateLoop() // U
        {
            while (!Nulling)
            {
                _uiRoot.transform.LookAtPlayer();

                // Fade UI
                _canvasGroup.alpha += (_visible ? 1 : -1) * Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(_canvasGroup.alpha);

                if (_canvasGroup.alpha == 0)
                {
                    UnityEngine.Object.DestroyImmediate(_uiRoot);
                }

                // Set UI download Data
                _texts[0].text = _mod.Percent;
                _texts[1].text = _mod.MB;

                try // why is this a problem?
                {
                    Vector2 sizeDelta = _downloadBar.sizeDelta;
                    sizeDelta.y = _mod.Progress * 512;
                    _downloadBar.sizeDelta = sizeDelta;
                } catch(Exception) { } 

                yield return null;
            }

        }
        protected override void OnMyCrateAdded() // U
        {
            if (!Nulling)
            {
                _visible = false;
                _onComplete.Invoke();
            }
        }

        // Patches
        [HarmonyPatch(typeof(SpawnResponseMessage), "HandleMessage", new Type[] { typeof(byte[]), typeof(bool) })]
        class SpawnResponseMessage_HandleMessage_Patch
        {
            private static ushort? s_lastSyncId = null;
            [HarmonyPrefix]
            public static bool Prefix(byte[] bytes, bool isServerHandled) // U
            {
                if (!isServerHandled)
                {
                    FusionReader reader = FusionReader.Create(bytes);
                    SpawnResponseData data = reader.ReadFusionSerializable<SpawnResponseData>();

                    // Prevent double spawning.
                    // This prefix gets called twice for some unknown reason
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

                    var palletBarcode = RepoWrapper.GetPalletBarcode(data.barcode);

                    // if valid barcode
                    if (palletBarcode.HasValue)
                    {
                        // if in a repo
                        if (RepoWrapper.Barcode2Mod.TryGetValue(palletBarcode.Value.Item1, out ModWrapper mod))
                        {
                            mod.TryDownload();
                            if (mod.Downloading == true)
                                new SpawnableUI(data, mod, () => { ActuallyProcess(data); });
                            else ActuallyProcess(data);
                            return false;
                        }
                        else // not in a repo
                        {
                            ActuallyProcess(data);
                            return false;
                        }
                    }
                    else //invalid barcode
                    {
                        ActuallyProcess(data);
                        return false;
                    }
                }
                else 
                    throw new ExpectedClientException();
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

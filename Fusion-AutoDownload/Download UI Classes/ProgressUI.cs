using BoneLib;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MelonLoader.MelonLogger;
using UnityEngine;
using LabFusion.Data;
using static RootMotion.FinalIK.GrounderQuadruped;
using SLZ.VRMK;
using System.IO.Pipes;
using SLZ.Marrow.Forklift.Model;
using SLZ.Rig;
using Il2CppSystem.Globalization;
using Il2CppSystem.Threading;
using Cysharp.Threading.Tasks;
using MelonLoader;
using static Il2CppSystem.Globalization.CultureInfo;
using System.Collections;

namespace FusionAutoDownload
{
    public abstract class ProgressUI
    {
        #region Asset Loading
        public static GameObject UIAssetAvatar
        {
            get
            {
                if (s_uiAssetAvatarInternal == null)
                {
                    s_uiAssetAvatarInternal = RepoWrapper.UIBundle.LoadAsset("Assets/UI/AutoDownload UI.prefab").Cast<GameObject>();
                }
                return s_uiAssetAvatarInternal;
            }
        }
        private static GameObject s_uiAssetAvatarInternal;
        public static GameObject UIAssetSpawnable
        {
            get
            {
                if (s_uiAssetSpawnableInternal == null)
                {
                    s_uiAssetSpawnableInternal = RepoWrapper.UIBundle.LoadAsset("Assets/UI/SpawnableUI.prefab").Cast<GameObject>();
                }
                return s_uiAssetSpawnableInternal;
            }
        }
        private static GameObject s_uiAssetSpawnableInternal;
        public static Sprite[] UISprites
        {
            get
            {
                if (s_uiSpritesInternal == null || s_uiSpritesInternal[0] == null)
                {
                    s_uiSpritesInternal = new Sprite[2];
                    s_uiSpritesInternal[0] = RepoWrapper.UIBundle.LoadAsset("Assets/UI/AutoDownload UI/PersonIcon.png").Cast<Sprite>();
                    s_uiSpritesInternal[1] = RepoWrapper.UIBundle.LoadAsset("Assets/UI/AutoDownload UI/PersonIconX.png").Cast<Sprite>();
                }
                return s_uiSpritesInternal;
            }
        }
        private static Sprite[] s_uiSpritesInternal;
        #endregion
        protected ProgressUI()
        {
            // After child ctor
            AutoDownloadMelon.UnityThread.Enqueue(() =>
            {
                MelonCoroutines.Start(UpdateLoop());
            });
        }

        private void OnCrateAdded(string crateBarcode)
        {
            if (crateBarcode == CrateBarcode)
                OnMyCrateAdded();
        }

        protected abstract void OnMyCrateAdded(); // U

        protected abstract IEnumerator UpdateLoop();
        public string CrateBarcode { get; protected set; } // U

        protected ModWrapper _mod
        {
            get => _mod_field;
            set
            {
                if (_mod_field != null)
                    _mod_field.CrateComplete -= OnCrateAdded;

                _mod_field = value;

                if (_mod_field != null)
                    _mod_field.CrateComplete += OnCrateAdded;
            }
        }
        protected ModWrapper _mod_field;
        protected bool ModNull => ReferenceEquals(_mod, null);

        protected GameObject _uiRoot;

        protected bool Nulling 
        { 
            get 
            {
                if (!_nulling_field)
                {
                    _nulling_field = _uiRoot == null;
                    if (_nulling_field)
                        _mod = null;
                    return _nulling_field;
                }
                else return true;  
            }
        }
        private bool _nulling_field;
    }
}

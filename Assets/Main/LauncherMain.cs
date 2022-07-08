using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Main
{
    public class LauncherMain : MonoBehaviour
    {
        private static AssetBundle UpdateScreenBundle { get; set; }
        private void Start()
        {
            LoadUpdateScene();
            Debug.Log("LauncherMain Start");
        }

        private void LoadUpdateScene()
        {
            var p = Path.Combine(Application.persistentDataPath, "launcher");
            if (!File.Exists(p))
                p = Path.Combine(Application.streamingAssetsPath, "launcher");
            UpdateScreenBundle = AssetBundle.LoadFromFile(p, 0, 0);
#if !UNITY_EDITOR
              TextAsset dllBytes1 = UpdateScreenBundle.LoadAsset<TextAsset>("HotFixLauncher.dll.bytes");
            System.Reflection.Assembly.Load(dllBytes1.bytes);
#endif
            var updateGo = UpdateScreenBundle.LoadAsset<GameObject>("UpdaterPrefab");
            Instantiate(updateGo);
        }
    }
}
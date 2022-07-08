using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UpdaterMono : MonoBehaviour
{

    public Slider SliderUpdater;
    public Text TxtUpdater;

    public string HostName = "http://172.16.50.30/";
    private string appVersion = "version.txt";

    public GameObject MessageBox;
    public Button BtnQuit;
    public Button BtnRetry;
    public Button BtnReboot;
    public Text TxtMessageBox;
    public Text TxtReboot;
    public GameObject RebootBox;
    private void Start()
    {
        NetworkMonitor.Instance.onReachabilityChanged += OnReachablityChanged;
        BtnQuit.onClick.AddListener(() =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif

        });
        BtnRetry.onClick.AddListener((() =>
        {
            HideMessageBox();
            StartCoroutine(Updater());    
        }));
        Debug.Log("start updater");
        StartCoroutine(Updater());
    }
    
    private void OnReachablityChanged(NetworkReachability reachability) {
        // 手机上关闭 wifi 和 4g
        if (reachability == NetworkReachability.NotReachable) {
            OnMessage("网络错误");
        }
    }

    private void EnterLogicMain()
    {
        if (isNeedReboot)
        {
            RebootBox.SetActive(true);
            BtnReboot.onClick.AddListener((() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            }));
            TxtReboot.text = "此次更新需要重启！";
            return;
        }
        
        Debug.Log($"isReboot{isNeedReboot}");

        LoadDll();
        
        var p = Path.Combine(Application.persistentDataPath, "scene");
        if (!File.Exists(p))
            p = Path.Combine(Application.streamingAssetsPath, "scene");
        var ab = AssetBundle.LoadFromFile(p, 0, 0);
        SceneManager.LoadScene("LogicScene");
    }

    private void LoadDll()
    {
        var common = Path.Combine(Application.persistentDataPath, "common");
        if (!File.Exists(common))
            common = Path.Combine(Application.streamingAssetsPath, "scene");
        var commonAb = AssetBundle.LoadFromFile(common, 0, 0);
        
        TextAsset dllBytes1 = commonAb.LoadAsset<TextAsset>("HotFix.dll.bytes");
        System.Reflection.Assembly.Load(dllBytes1.bytes);
        TextAsset dllBytes2 = commonAb.LoadAsset<TextAsset>("HotFix2.dll.bytes");
        System.Reflection.Assembly.Load(dllBytes2.bytes);
    }

    private void ShowMessageBox(string boxData)
    {
        TxtMessageBox.text = boxData;
        MessageBox.SetActive(true);
    }

    private void HideMessageBox()
    {
        MessageBox.SetActive(false);
    }
    
    private void OnMessage(string msg) {
        TxtUpdater.text = msg;
    }

    private void OnUpdater(float val)
    {
        SliderUpdater.value = val;
    }

    IEnumerator Updater()
    {
        OnUpdater(0);
        var webRequest = UnityWebRequest.Get($"{HostName}{appVersion}");
        OnMessage("检查更新中 ...");
        yield return webRequest.SendWebRequest();
        if (!string.IsNullOrEmpty(webRequest.error))
        {
            Debug.LogError(webRequest.error);
            ShowMessageBox("检查更新失败，是否重试！");
            OnMessage("检查更新失败");
            yield return null;
        }
        else
        {
            yield return new WaitForEndOfFrame();
            if (downloadInfos.Count > 0)
                StartCoroutine(Download());
            else
                EnterLogicMain();
        }
    }

    IEnumerator Download()
    {
        foreach (var downloadInfo in downloadInfos)
        {
            var webRequest = UnityWebRequest.Get(downloadInfo.Url);
            Debug.Log($"download {downloadInfo.Url}");
            yield return webRequest.SendWebRequest();
            if (string.IsNullOrEmpty(webRequest.error))
            {
                curSize += webRequest.downloadedBytes;
                OnUpdater(1 - curSize / allSize );
                OnMessage($"{curSize / 1024}kb / {allSize / 1024} kb");
                if (File.Exists(downloadInfo.SavePath))
                    File.Delete(downloadInfo.SavePath);
                File.WriteAllBytes(downloadInfo.SavePath, webRequest.downloadHandler.data);
            }
            else
            {
                ShowMessageBox("更新失败，是否重试！");
                Debug.LogError($"更新失败 {webRequest.error}");
                OnMessage("更新失败");
                yield return null;
            }
            
        }
        yield return new WaitForEndOfFrame();
        OnMessage("更新完成");
        OnUpdater(1);
        EnterLogicMain();
        //热更完成进入其他main场景
    }

    private class DownloadInfo
    {
        public string Url;
        public uint Size;
        public string SavePath;
    }
    
    /// <summary>
    ///     获取一个文件的md5值
    /// </summary>
    /// <param name="fileName">文件路径</param>
    /// <returns>md5值</returns>
    public static string Md5(string fileName)
    {
        try
        {
            var file = new FileStream(fileName, FileMode.Open);
            MD5 md5 = new MD5CryptoServiceProvider();
            var retVal = md5.ComputeHash(file);
            file.Close();

            var sb = new StringBuilder();
            for (var i = 0; i < retVal.Length; i++) sb.Append(retVal[i].ToString("x2"));

            return sb.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
        }
    }

    private List<DownloadInfo> downloadInfos;
    private ulong allSize;
    private ulong curSize;
    private const string StrLauncherAssetBundle = "launcher";
    private bool isNeedReboot;
    private void GenUpdaterInfo(string version)
    {
        var infos = version.Split('\n');
        var localDir = Application.persistentDataPath;
        downloadInfos = new List<DownloadInfo>();
        OnMessage("校验文件中 ...");
        var cnt = 0;
        foreach (var s in infos)
        {
            if (string.IsNullOrEmpty(s))
                return;
            var d = s.Split('|');
            var fName = d[0];
            var md5 = d[1];
            var localFile = Path.Combine(localDir, fName);
            Debug.Log($"localFile {localFile}");
            //如果本地没有这个文件，或者文件md5值不同说明需要热更，简单判断
            if (!File.Exists(localFile) || !string.Equals(md5, Md5(localFile)))
            {
                var down = new DownloadInfo()
                {
                    Url = $"{HostName}{d[0]}",
                    Size = uint.Parse(d[2]),
                    SavePath = localFile,
                };
                //如果更新到启动资源说明需要重启
                if (d[0] == StrLauncherAssetBundle)
                    isNeedReboot = true;
                allSize += down.Size;
                downloadInfos.Add(down);
            }

            cnt++;
            OnUpdater((float) cnt / infos.Length);
        }
        OnUpdater(1);
    }
}
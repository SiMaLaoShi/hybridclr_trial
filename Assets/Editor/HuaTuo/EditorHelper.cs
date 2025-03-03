using Huatuo.Generators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using FileMode = System.IO.FileMode;

namespace Huatuo
{
    /// <summary>
    /// 这里仅仅是一个流程展示
    /// 简单说明如果你想将huatuo的dll做成自动化的简单实现
    /// </summary>
    public class EditorHelper
    {

        private static void CreateDirIfNotExists(string dirName)
        {
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
        }

        public static string ToReleateAssetPath(string s)
        {
            return s.Substring(s.IndexOf("Assets/"));
        }

        private static void CompileDll(string buildDir, BuildTarget target)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);

            ScriptCompilationSettings scriptCompilationSettings = new ScriptCompilationSettings();
            scriptCompilationSettings.group = group;
            scriptCompilationSettings.target = target;
            CreateDirIfNotExists(buildDir);
            ScriptCompilationResult scriptCompilationResult = PlayerBuildInterface.CompilePlayerScripts(scriptCompilationSettings, buildDir);
            foreach (var ass in scriptCompilationResult.assemblies)
            {
                Debug.LogFormat("compile assemblies:{0}", ass);
            }
        }

        public static string DllBuildOutputDir => Path.GetFullPath($"{Application.dataPath}/../Temp/Huatuo/build");

        public static string GetDllBuildOutputDirByTarget(BuildTarget target)
        {
            return $"{DllBuildOutputDir}/{target}";
        }

        [MenuItem("Huatuo/CompileDll/ActiveBuildTarget")]
        public static void CompileDllActiveBuildTarget()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/CompileDll/Win64")]
        public static void CompileDllWin64()
        {
            var target = BuildTarget.StandaloneWindows64;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/CompileDll/Linux64")]
        public static void CompileDllLinux()
        {
            var target = BuildTarget.StandaloneLinux64;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/CompileDll/OSX")]
        public static void CompileDllOSX()
        {
            var target = BuildTarget.StandaloneOSX;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/CompileDll/Android")]
        public static void CompileDllAndroid()
        {
            var target = BuildTarget.Android;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/CompileDll/IOS")]
        public static void CompileDllIOS()
        {
            //var target = EditorUserBuildSettings.activeBuildTarget;
            var target = BuildTarget.iOS;
            CompileDll(GetDllBuildOutputDirByTarget(target), target);
        }

        public static string HuatuoBuildCacheDir => Application.dataPath + "/HuatuoBuildCache";

        public static string AssetBundleOutputDir => $"{HuatuoBuildCacheDir}/AssetBundleOutput";

        public static string AssetBundleSourceDataTempDir => $"{HuatuoBuildCacheDir}/AssetBundleSourceData";

        public static string HuatuoDataDir => $"{Application.dataPath}/../HuatuoData";

        public static string AssembliesPostIl2CppStripDir => $"{HuatuoDataDir}/AssembliesPostIl2CppStrip";

        public static string MethodBridgeCppDir => $"{HuatuoDataDir}/LocalIl2CppData/il2cpp/libil2cpp/huatuo/interpreter";

        public static string GetAssetBundleOutputDirByTarget(BuildTarget target)
        {
            return $"{AssetBundleOutputDir}/{target}";
        }

        public static string GetAssetBundleTempDirByTarget(BuildTarget target)
        {
            return $"{AssetBundleSourceDataTempDir}/{target}";
        }

        /// <summary>
        /// 将HotFix.dll和HotUpdatePrefab.prefab打入common包.
        /// 将HotUpdateScene.unity打入scene包.
        /// </summary>
        /// <param name="tempDir"></param>
        /// <param name="outputDir"></param>
        /// <param name="target"></param>
        private static void BuildAssetBundles(string tempDir, string outputDir, BuildTarget target)
        {
            CreateDirIfNotExists(tempDir);
            CreateDirIfNotExists(outputDir);

            List<string> notSceneAssets = new List<string>();

            CompileDll(GetDllBuildOutputDirByTarget(target), target);

            var hotfixDlls = new List<string>()
            {
                "HotFix.dll",
                "HotFix2.dll",
            };

            foreach(var dll in hotfixDlls)
            {
                string dllPath = $"{GetDllBuildOutputDirByTarget(target)}/{dll}";
                string dllBytesPath = $"{tempDir}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);
                notSceneAssets.Add(dllBytesPath);
            }
            
            var launcherDlls = new List<string>()
            {
                "HotFixLauncher.dll",
            };
            List<string> launcherAssets = new List<string>();
            foreach(var dll in launcherDlls)
            {
                string dllPath = $"{GetDllBuildOutputDirByTarget(target)}/{dll}";
                string dllBytesPath = $"{tempDir}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);
                launcherAssets.Add(dllBytesPath);
            }

            var aotDlls = new string[]
            {
                "mscorlib.dll",
                "System.dll",
                "System.Core.dll", // 如果使用了Linq，需要这个
            };

            string aotDllDir = $"{AssembliesPostIl2CppStripDir}/{target}";
            foreach (var dll in aotDlls)
            {
                string dllPath = $"{aotDllDir}/{dll}";
                if(!File.Exists(dllPath))
                {
                    Debug.LogError($"ab中添加AOT补充元数据dll:{dllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                    continue;
                }
                string dllBytesPath = $"{tempDir}/{dll}.bytes";
                File.Copy(dllPath, dllBytesPath, true);
                notSceneAssets.Add(dllBytesPath);
            }

            string testPrefab = $"{Application.dataPath}/Prefabs/HotUpdatePrefab.prefab";
            notSceneAssets.Add(testPrefab);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            List<AssetBundleBuild> abs = new List<AssetBundleBuild>();
            AssetBundleBuild notSceneAb = new AssetBundleBuild
            {
                assetBundleName = "common",
                assetNames = notSceneAssets.Select(s => ToReleateAssetPath(s)).ToArray(),
            };
            abs.Add(notSceneAb);

            //启动需要的资源单独打一个包
            launcherAssets.Add($"{Application.dataPath}/Prefabs/UpdaterPrefab.prefab");
            AssetBundleBuild launcherAb = new AssetBundleBuild
            {
                assetBundleName = "launcher",
                assetNames = launcherAssets.Select(s => ToReleateAssetPath(s)).ToArray(),
            };

            abs.Add(launcherAb);
            
            string testScene = $"{Application.dataPath}/Scenes/HotUpdateScene.unity";
            
            string[] sceneAssets =
            {
                testScene,
                $"{Application.dataPath}/Scenes/LogicScene.unity",
            };
            AssetBundleBuild sceneAb = new AssetBundleBuild
            {
                assetBundleName = "scene",
                assetNames = sceneAssets.Select(s => s.Substring(s.IndexOf("Assets/"))).ToArray(),
            };

            abs.Add(sceneAb);

            BuildPipeline.BuildAssetBundles(outputDir, abs.ToArray(), BuildAssetBundleOptions.None, target);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            string streamingAssetPathDst = $"{Application.streamingAssetsPath}";
            GenVersion(outputDir);
            CreateDirIfNotExists(streamingAssetPathDst);

            foreach (var ab in abs)
            {
                AssetDatabase.CopyAsset(ToReleateAssetPath($"{outputDir}/{ab.assetBundleName}"),
                    ToReleateAssetPath($"{streamingAssetPathDst}/{ab.assetBundleName}"));
            }
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

        /// <summary>
        ///     非递归获取某个文件夹下所有文件
        /// </summary>
        /// <param name="files">存放文件路径的列表</param>
        /// <param name="dir">文件夹路径</param>
        /// <param name="pattern">文件类型</param>
        public static void NotRecursiveGetAllFile(ref List<string> files, string dir, string pattern = null)
        {
            var dirs = Directory.GetDirectories(dir, "*.*", SearchOption.AllDirectories);
            foreach (var path in dirs)
                files.AddRange(Directory.GetFiles(path, pattern ?? "*.*", SearchOption.AllDirectories));
        }
        
        /// <summary>
        ///     递归获取某个文件夹下所有文件
        /// </summary>
        /// <param name="files">存放文件路径的列表</param>
        /// <param name="dir">文件夹路径</param>
        /// <param name="pattern">文件类型</param>
        public static void RecursiveGetAllFile(ref List<string> files, string dir, string pattern = null)
        {
            files.AddRange(pattern == null
                ? Directory.GetFiles(dir)
                : Directory.GetFiles(dir, pattern, SearchOption.AllDirectories));

            var dirs = Directory.GetDirectories(dir);
            foreach (var d in dirs)
                RecursiveGetAllFile(ref files, d);
        }

        static void GenVersion(string dir)
        {
            var files = new List<string>();
            RecursiveGetAllFile(ref files, dir);
            var sw = File.CreateText(Path.Combine(dir, "version.txt"));
            foreach (var file in files)
            {
                if (file.EndsWith(".meta") || file.EndsWith("version.txt") || file.EndsWith(".manifest"))
                    continue;
                var f = new FileInfo(file);
                sw.WriteLine($"{Path.GetFileName(file)}|{Md5(file)}|{f.Length}");
            }
            sw.Close();
        }

        [MenuItem("Huatuo/BuildBundles/ActiveBuildTarget")]
        public static void BuildSeneAssetBundleActiveBuildTarget()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/BuildBundles/Win64")]
        public static void BuildSeneAssetBundleWin64()
        {
            var target = BuildTarget.StandaloneWindows64;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/BuildBundles/OSX")]
        public static void BuildSeneAssetBundleOSX64()
        {
            var target = BuildTarget.StandaloneOSX;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/BuildBundles/Linux64")]
        public static void BuildSeneAssetBundleLinux64()
        {
            var target = BuildTarget.StandaloneLinux64;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/BuildBundles/Android")]
        public static void BuildSeneAssetBundleAndroid()
        {
            var target = BuildTarget.Android;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        [MenuItem("Huatuo/BuildBundles/Build Version")]
        public static void BuildVersionAndroid()
        {
            GenVersion(GetAssetBundleOutputDirByTarget(BuildTarget.Android));
        }
        

        [MenuItem("Huatuo/BuildBundles/IOS")]
        public static void BuildSeneAssetBundleIOS()
        {
            var target = BuildTarget.iOS;
            BuildAssetBundles(GetAssetBundleTempDirByTarget(target), GetAssetBundleOutputDirByTarget(target), target);
        }

        private static void CleanIl2CppBuildCache()
        {
            string il2cppBuildCachePath = $"{Application.dataPath}/../Library/Il2cppBuildCache";
            if (!Directory.Exists(il2cppBuildCachePath))
            {
                return;
            }
            Debug.Log($"clean il2cpp build cache:{il2cppBuildCachePath}");
            Directory.Delete(il2cppBuildCachePath, true);
        }

        [MenuItem("Huatuo/Generate/MethodBridge_X64")]
        public static void MethodBridge_X86()
        {
            string outputFile = $"{MethodBridgeCppDir}/MethodBridge_x64.cpp";
            var g = new MethodBridgeGenerator(new MethodBridgeGeneratorOptions()
            {
                CallConvention = CallConventionType.X64,
                Assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList(),
                OutputFile = outputFile,
            });

            g.PrepareMethods();
            g.Generate();
            Debug.LogFormat("== output:{0} ==", outputFile);
            CleanIl2CppBuildCache();
        }

        [MenuItem("Huatuo/Generate/MethodBridge_Arm64")]
        public static void MethodBridge_Arm64()
        {
            string outputFile = $"{MethodBridgeCppDir}/MethodBridge_arm64.cpp";
            var g = new MethodBridgeGenerator(new MethodBridgeGeneratorOptions()
            {
                CallConvention = CallConventionType.Arm64,
                Assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList(),
                OutputFile = outputFile,
            });

            g.PrepareMethods();
            g.Generate();
            Debug.LogFormat("== output:{0} ==", outputFile);
            CleanIl2CppBuildCache();
        }
    }
}
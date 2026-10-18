using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace TextureFireWorks.Editor
{
    /// <summary>Batch checks run only in the dedicated disposable verification project.</summary>
    public static class FireworkValidation
    {
        [InitializeOnLoadMethod]
        private static void ResumeResourceImport()
        {
            if (!Application.isBatchMode || Argument("-executeMethod") != "TextureFireWorks.Editor.FireworkValidation.ImportResources") return;
            // Importing TMP settings can reload the domain before the completion callback.
            EditorApplication.delayCall += CheckResources;
        }

        private static void CheckResources()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CheckResources;
                return;
            }
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null) return;
            Debug.Log("TFW_TMP_IMPORTED");
            EditorApplication.Exit(0);
        }

        public static void ImportResources()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.textmeshpro");
            AssetDatabase.importPackageCompleted += ResourcesImported;
            AssetDatabase.importPackageFailed += ResourcesFailed;
            AssetDatabase.ImportPackage(package.resolvedPath + "/Package Resources/TMP Essential Resources.unitypackage", false);
        }

        private static void ResourcesImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ResourcesImported;
            AssetDatabase.importPackageFailed -= ResourcesFailed;
            AssetDatabase.Refresh();
            Debug.Log("TFW_TMP_IMPORTED " + packageName);
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        private static void ResourcesFailed(string packageName, string reason)
        {
            Debug.LogError(packageName + ": " + reason);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        public static void Run()
        {
            string output = Argument("-tfwOutput");
            string fontFile = Argument("-tfwFont");
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(fontFile))
                throw new ArgumentException("検証には -tfwOutput と -tfwFont を指定してください。専用の検証用プロジェクトで実行します。");
            Directory.CreateDirectory(output);
            var results = new List<string>();
            var settings = ScriptableObject.CreateInstance<FireworkSettings>();
            try
            {
                if (!AssetDatabase.IsValidFolder("Assets/TFWValidation")) AssetDatabase.CreateFolder("Assets", "TFWValidation");
                string fontPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TFWValidation/VerificationFont" + Path.GetExtension(fontFile));
                File.Copy(fontFile, fontPath);
                AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                Check(font != null, "検証用フォントを読み込める");
                settings.font = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                settings.font.name = "Verification TMP Font";
                string tmpPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TFWValidation/VerificationTMP.asset");
                AssetDatabase.CreateAsset(settings.font, tmpPath);
                AssetDatabase.AddObjectToAsset(settings.font.material, settings.font);
                foreach (Texture2D atlas in settings.font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, settings.font);
                settings.message = "ありがとう！";
                List<Vector3> japanese = FireworkBaker.SampleText(settings);
                Check(japanese.Count > 100, "日本語の輪郭を点へ変換");
                results.Add("PASS 日本語: " + japanese.Count + "点");
                var min = japanese.Aggregate(Vector3.Min);
                var max = japanese.Aggregate(Vector3.Max);
                Check(((min + max) * 0.5f).magnitude < 0.001f, "文字を中央に配置");
                settings.message = "THANK YOU! 2026";
                Check(FireworkBaker.SampleText(settings).Count > 100, "英数字の描画");
                results.Add("PASS 英数字");
                settings.message = "また会おう\nこの空の下で";
                List<Vector3> multiline = FireworkBaker.SampleText(settings);
                Check(multiline.Max(p => p.y) - multiline.Min(p => p.y) > settings.characterSize, "複数行の高さ");
                results.Add("PASS 複数行: " + multiline.Count + "点");
                settings.message = " \n ";
                ExpectFailure(() => settings.Validate(), "空文字");
                settings.message = "\u0378";
                ExpectFailure(() => settings.Validate(), "未収録文字");
                settings.message = "ありがとう！";
                settings.maxPoints = 1;
                ExpectFailure(() => FireworkBaker.SampleText(settings), "点数上限");
                settings.maxPoints = 8000;
                results.Add("PASS 空文字・未収録文字・上限超過の検出");
                var mesh = FireworkBaker.CreateMesh(japanese);
                var again = FireworkBaker.CreateMesh(japanese);
                Check(mesh.vertices.SequenceEqual(again.vertices) && mesh.uv2.SequenceEqual(again.uv2), "全クライアント用の決定的な点配置");
                Check(mesh.vertexCount == (japanese.Count + 252) * 4, "メッシュの点数");
                UnityEngine.Object.DestroyImmediate(again);
                Material material = FireworkBaker.CreateMaterial(settings);
                Check(!ShaderUtil.ShaderHasError(material.shader), "シェーダーのコンパイル");
                float[] times = { -1, 0.8f, 2.15f, 3.5f, 6.2f, settings.Duration + 1 };
                int[] counts = new int[times.Length];
                for (int i = 0; i < times.Length; i++)
                    counts[i] = Render(mesh, material, settings, times[i], output + "/phase-" + i + ".png");
                Check(counts[0] == 0 && counts[5] == 0, "開始前・終了後は非表示");
                Check(counts[1] > 0 && counts[2] > 0 && counts[3] > 100 && counts[4] > 0, "全演出段階の描画");
                Mesh multilineMesh = FireworkBaker.CreateMesh(multiline);
                Check(Render(multilineMesh, material, settings, 3.5f, output + "/multiline.png") > 100, "複数行のGPU描画");
                UnityEngine.Object.DestroyImmediate(multilineMesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(material);
                results.Add("PASS GPU描画（上昇・展開・文字・落下・非表示）");
                EnsurePrograms();
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = false });
                foreach (string guid in AssetDatabase.FindAssets("t:UdonSharpProgramAsset", new[] { FireworkPackageExporter.SourceRoot }))
                {
                    var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    Check(program.GetRealProgram() != null, "Udonプログラムの生成");
                }
                results.Add("PASS UdonSharp変換");
                string prefabPath = FireworkPrefabFactory.Save(settings, japanese, "Assets/TFWValidation/ThankYou.prefab");
                string second = FireworkPrefabFactory.Save(settings, japanese, "Assets/TFWValidation/ThankYou.prefab");
                Check(prefabPath != second, "既存Prefabを上書きしない");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Check(prefab != null, "Prefabの再読み込み");
                var launcher = prefab.GetComponent<FireworkLauncher>();
                Check(launcher != null && launcher.effectRenderer != null && launcher.launchPoint != null && launcher.burstPoint != null, "Prefabの発射台参照");
                var button = prefab.GetComponentInChildren<FireworkButton>();
                Check(button != null && button.launcher == launcher, "操作ボタンの接続");
                Check(!AssetDatabase.GetDependencies(prefabPath, true).Contains(tmpPath), "実行時フォント依存がない");
                results.Add("PASS Prefab生成・再読み込み・別名保存・ボタン接続・フォント非依存");
                settings.message = "また会おう\nこの空の下で";
                string multiPrefab = FireworkPrefabFactory.Save(settings, multiline, "Assets/TFWValidation/Multiline.prefab");
                BuildDemo(prefabPath, multiPrefab);
                string exportPath = output + "/TextureFireWorks-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".unitypackage";
                FireworkPackageExporter.Export(exportPath, prefabPath);
                results.Add("PASS 導入用unitypackage出力: " + exportPath);
                results.Add("PENDING VRChatの2クライアント同期・途中参加・所有者退出・ワールドのSDKビルド");
                AssetDatabase.SaveAssets();
                File.WriteAllLines(output + "/validation.txt", results);
                File.WriteAllText(output + "/prefab-paths.txt", prefabPath + "\n" + multiPrefab);
                Debug.Log("TFW_VALIDATION_PASSED\n" + string.Join("\n", results));
            }
            catch (Exception e)
            {
                results.Add("FAIL " + e);
                File.WriteAllLines(output + "/validation.txt", results);
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        private static void EnsurePrograms()
        {
            string definitionPath = FireworkPackageExporter.SourceRoot + "/Runtime/TextureFireWorks.Udon.asset";
            var definition = AssetDatabase.LoadAssetAtPath<UdonSharpAssemblyDefinition>(definitionPath);
            var assembly = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(FireworkPackageExporter.SourceRoot + "/Runtime/TextureFireWorks.Runtime.asmdef");
            if (definition == null) throw new InvalidOperationException("U#アセンブリ定義を読み込めません。");
            if (definition.sourceAssembly != assembly)
            {
                definition.sourceAssembly = assembly;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(definitionPath, ImportAssetOptions.ForceUpdate);
            }
            foreach (string scriptName in new[] { "FireworkLauncher", "FireworkButton" })
            {
                string path = FireworkPackageExporter.SourceRoot + "/Runtime/" + scriptName;
                var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset");
                if (program == null)
                {
                    program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
                    AssetDatabase.CreateAsset(program, path + ".asset");
                }
                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildDemo(string first, string second)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.1f);
            RenderSettings.skybox = null;
            var a = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(first));
            a.transform.position = new Vector3(-7, 0, 0);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(second));
            b.transform.position = new Vector3(7, 0, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "観覧エリア";
            floor.transform.localScale = new Vector3(8, 1, 8);
            var cameraObject = new GameObject("観覧カメラ（開花位置から30m）");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.008f, 0.025f);
            camera.fieldOfView = 50;
            Vector3 burst = a.GetComponent<FireworkLauncher>().burstPoint.position;
            camera.transform.position = burst + Vector3.back * 30;
            camera.transform.LookAt(burst);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
            string scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/TFWValidation/FireworksDemo.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static int Render(Mesh mesh, Material material, FireworkSettings settings, float elapsed, string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                var go = new GameObject("Sparks");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                material.SetFloat("_Elapsed", elapsed);
                var cameraObject = new GameObject("Verification Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 50;
                camera.transform.position = new Vector3(0, settings.launchHeight, -30);
                camera.transform.LookAt(Vector3.up * settings.launchHeight);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32().Count(c => c.r > 12 || c.g > 12 || c.b > 12);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }

        private static void ExpectFailure(Action action, string label)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException(label + "が拒否されませんでした。");
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

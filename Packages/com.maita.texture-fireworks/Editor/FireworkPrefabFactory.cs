using System;
using System.Collections.Generic;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextureFireWorks.Editor
{
    public static class FireworkPrefabFactory
    {
        public static string Save(FireworkSettings settings, List<Vector3> points, string requestedPath)
        {
            if (string.IsNullOrEmpty(requestedPath) || !requestedPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("保存先はプロジェクトのAssetsフォルダー内にしてください。");
            settings.Validate();
            if (points == null || points.Count == 0 || points.Count > settings.maxPoints)
                throw new ArgumentException("プレビューを生成してから保存してください。");
            string parent = Path.GetDirectoryName(requestedPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(requestedPath);
            string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + stem);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new IOException("保存フォルダーを作成できませんでした。");
            GameObject root = null;
            try
            {
                Mesh mesh = FireworkBaker.CreateMesh(points);
                Material material = FireworkBaker.CreateMaterial(settings);
                AssetDatabase.CreateAsset(mesh, folder + "/Sparks.asset");
                AssetDatabase.CreateAsset(material, folder + "/Sparks.mat");
                // The authoring settings stay separate from the runtime Prefab.
                var settingsCopy = UnityEngine.Object.Instantiate(settings);
                settingsCopy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(settingsCopy, folder + "/Settings.asset");
                root = new GameObject(stem);
                GameObject visual = Child(root.transform, "文字花火（向きはこの親で調整）", Vector3.zero);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // The shader is invisible until the launcher sets its elapsed time.
                var launcher = root.AddUdonSharpComponent<FireworkLauncher>();
                launcher.effectRenderer = renderer;
                launcher.launchPoint = Child(root.transform, "発射位置", Vector3.zero).transform;
                launcher.burstPoint = Child(root.transform, "開花位置", Vector3.up * settings.launchHeight).transform;
                launcher.ascentDuration = settings.ascentDuration;
                launcher.spreadDuration = settings.spreadDuration;
                launcher.holdDuration = settings.holdDuration;
                launcher.fadeDuration = settings.fadeDuration;
                launcher.ascentAudio = AddAudio(launcher.launchPoint, settings.ascentClip);
                launcher.burstAudio = AddAudio(launcher.burstPoint, settings.burstClip);
                var buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                buttonObject.name = "打ち上げボタン";
                buttonObject.transform.SetParent(root.transform, false);
                buttonObject.transform.localPosition = new Vector3(0, 1, -3);
                buttonObject.transform.localScale = new Vector3(0.6f, 0.25f, 0.6f);
                var buttonMaterial = new Material(Shader.Find("Standard")) { name = "Launch Button", color = new Color(0.08f, 0.4f, 0.7f) };
                AssetDatabase.CreateAsset(buttonMaterial, folder + "/Button.mat");
                buttonObject.GetComponent<MeshRenderer>().sharedMaterial = buttonMaterial;
                var button = buttonObject.AddUdonSharpComponent<FireworkButton>();
                button.launcher = launcher;
                UdonSharpEditorUtility.CopyProxyToUdon(launcher);
                UdonSharpEditorUtility.CopyProxyToUdon(button);
                var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(button);
                backing.interactText = "文字花火を打ち上げる";
                backing.proximity = 3;
                var launcherBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(launcher);
                launcherBacking.interactText = "文字花火を打ち上げる";
                // No collider on the launcher: the separate button is the interaction target.
                string prefabPath = folder + "/" + stem + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null) throw new IOException("Prefabを保存できませんでした。");
                AssetDatabase.SaveAssets();
                return prefabPath;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("生成に失敗しました。途中のデータは「" + folder + "」に残しています。\n" + e.Message, e);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject Child(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }

        private static AudioSource AddAudio(Transform parent, AudioClip clip)
        {
            var source = parent.gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.minDistance = 5;
            source.maxDistance = 100;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }
}

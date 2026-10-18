using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextureFireWorks.Editor
{
    public class FireworkEditorWindow : EditorWindow
    {
        [SerializeField] private FireworkSettings settings;
        private FireworkSettings temporarySettings;
        private PreviewRenderUtility preview;
        private Mesh mesh;
        private Material material;
        private List<Vector3> points;
        private string error;
        private bool playing;
        private float elapsed;
        private double lastUpdate;
        private Vector2 scroll;
        private float cameraDistance = 35f;

        [MenuItem("Tools/TextureFireWorks/文字花火エディター")]
        public static void Open()
        {
            GetWindow<FireworkEditorWindow>("文字花火").Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(480, 640);
            if (settings == null)
            {
                temporarySettings = CreateInstance<FireworkSettings>();
                temporarySettings.hideFlags = HideFlags.HideAndDontSave;
                settings = temporarySettings;
            }
            else if (!EditorUtility.IsPersistent(settings)) temporarySettings = settings;
            lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            ClearPreview();
            preview?.Cleanup();
            preview = null;
            if (temporarySettings != null) DestroyImmediate(temporarySettings);
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && settings != null)
            {
                elapsed += (float)(now - lastUpdate);
                if (elapsed >= settings.Duration) { elapsed = settings.Duration; playing = false; }
                Repaint();
            }
            lastUpdate = now;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("夜空に、文章を咲かせる。", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("フォントと文章を指定 → プレビュー生成 → Prefab保存。生成したPrefabをワールドへ配置してください。", MessageType.Info);
            var selected = (FireworkSettings)EditorGUILayout.ObjectField("保存済み設定", EditorUtility.IsPersistent(settings) ? settings : null, typeof(FireworkSettings), false);
            if (selected != null && selected != settings) { settings = selected; ClearPreview(); }
            if (settings == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var serialized = new SerializedObject(settings);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            Property(serialized, "message", "文章（改行できます）");
            Property(serialized, "font", "フォント（TMP）");
            Property(serialized, "characterSize", "文字の大きさ（m）");
            Property(serialized, "lineSpacing", "行間");
            Property(serialized, "density", "火花の密度");
            Property(serialized, "maxPoints", "文字の点数上限");
            Property(serialized, "color", "火花の色");
            Property(serialized, "sparkSize", "火花の大きさ（m）");
            Property(serialized, "launchHeight", "打ち上げ高さ（m）");
            Property(serialized, "ascentDuration", "上昇時間（秒）");
            Property(serialized, "spreadDuration", "広がる時間（秒）");
            Property(serialized, "holdDuration", "文字を残す時間（秒）");
            Property(serialized, "fadeDuration", "落下・消失時間（秒）");
            Property(serialized, "gravity", "落下加速度");
            Property(serialized, "ascentClip", "上昇音（任意）");
            Property(serialized, "burstClip", "開花音（任意）");
            bool changed = EditorGUI.EndChangeCheck();
            serialized.ApplyModifiedProperties();
            if (changed) ClearPreview();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("サンプル：ありがとう！")) SetSample("ありがとう！");
                if (GUILayout.Button("サンプル：複数行")) SetSample("また会おう\nこの空の下で");
            }
            if (GUILayout.Button("プレビューを生成", GUILayout.Height(28))) BakePreview();
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (mesh != null)
            {
                EditorGUILayout.LabelField("文字の点数：" + points.Count + " / " + settings.maxPoints + "　（装飾252点を追加）");
                elapsed = EditorGUILayout.Slider("再生位置（秒）", elapsed, 0, settings.Duration);
                cameraDistance = EditorGUILayout.Slider("観覧距離（m）", cameraDistance, 10, 100);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? "一時停止" : "再生"))
                    {
                        if (elapsed >= settings.Duration) elapsed = 0;
                        playing = !playing;
                        lastUpdate = EditorApplication.timeSinceStartup;
                    }
                    if (GUILayout.Button("停止")) { playing = false; elapsed = 0; }
                    if (GUILayout.Button("文字の完成時点")) { playing = false; elapsed = settings.ascentDuration + settings.spreadDuration + 0.2f; }
                }
                Rect rect = GUILayoutUtility.GetRect(200, 270, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint) DrawPreview(rect);
                if (GUILayout.Button("Prefabを保存（毎回別フォルダー）", GUILayout.Height(30))) SavePrefab();
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Property(SerializedObject settingsObject, string name, string label)
        {
            EditorGUILayout.PropertyField(settingsObject.FindProperty(name), new GUIContent(label));
        }

        private void SetSample(string message)
        {
            Undo.RecordObject(settings, "サンプル文章を設定");
            settings.message = message;
            EditorUtility.SetDirty(settings);
            ClearPreview();
        }

        private void BakePreview()
        {
            ClearPreview();
            try
            {
                points = FireworkBaker.SampleText(settings);
                mesh = FireworkBaker.CreateMesh(points);
                material = FireworkBaker.CreateMaterial(settings);
                elapsed = settings.ascentDuration + settings.spreadDuration + 0.2f;
            }
            catch (Exception e) { error = e.Message; }
        }

        private void DrawPreview(Rect rect)
        {
            if (preview == null) preview = new PreviewRenderUtility();
            preview.BeginPreview(rect, GUIStyle.none);
            var camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.008f, 0.025f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300;
            camera.fieldOfView = 50;
            camera.transform.position = new Vector3(0, settings.launchHeight * 0.55f, -cameraDistance);
            camera.transform.LookAt(new Vector3(0, settings.launchHeight * 0.55f, 0));
            material.SetFloat("_Elapsed", elapsed);
            preview.DrawMesh(mesh, Matrix4x4.identity, material, 0);
            preview.Render();
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void SavePrefab()
        {
            string destination = EditorUtility.SaveFilePanelInProject("文字花火を保存", "TextFirework", "prefab", "Assets内の保存先を選択してください。専用フォルダーを作成します。");
            if (string.IsNullOrEmpty(destination)) return;
            try
            {
                // Settings can also be edited in another Inspector while this window is open.
                var currentPoints = FireworkBaker.SampleText(settings);
                string path = FireworkPrefabFactory.Save(settings, currentPoints, destination);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                ShowNotification(new GUIContent("文字花火を保存しました"));
            }
            catch (Exception e) { error = e.Message; }
        }

        private void ClearPreview()
        {
            playing = false;
            error = null;
            points = null;
            if (mesh != null) DestroyImmediate(mesh);
            if (material != null) DestroyImmediate(material);
            mesh = null;
            material = null;
        }
    }
}

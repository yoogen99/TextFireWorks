using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TextureFireWorks.Editor
{
    public static class FireworkBaker
    {
        public static List<Vector3> SampleText(FireworkSettings settings)
        {
            settings.Validate();
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            Material textMaterial = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var textObject = new GameObject("Text mask");
                SceneManager.MoveGameObjectToScene(textObject, scene);
                var text = textObject.AddComponent<TextMeshPro>();
                text.font = settings.font;
                text.text = settings.message.Replace("\r\n", "\n").Replace("\r", "\n");
                text.fontSize = 100;
                text.lineSpacing = settings.lineSpacing;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.richText = false;
                text.color = Color.white;
                textMaterial = new Material(settings.font.material);
                if (textMaterial.HasProperty("_FaceColor")) textMaterial.SetColor("_FaceColor", Color.white);
                if (textMaterial.HasProperty("_OutlineWidth")) textMaterial.SetFloat("_OutlineWidth", 0);
                textMaterial.DisableKeyword("UNDERLAY_ON");
                text.fontSharedMaterial = textMaterial;
                text.ForceMeshUpdate(true, true);
                Bounds bounds = text.textBounds;
                if (bounds.size.x < 0.001f || bounds.size.y < 0.001f)
                    throw new ArgumentException("描画できる文字がありません。");
                float em = settings.font.faceInfo.lineHeight / settings.font.faceInfo.pointSize * text.fontSize * 0.1f;
                float units = settings.characterSize / Mathf.Max(0.001f, em);
                float height = bounds.size.y * units;
                float width = bounds.size.x * units;
                // Padding prevents clipped SDF edges; cap texture dimensions for long messages.
                float pixelsPerUnit = Mathf.Min(64f / settings.characterSize, 2040f / Mathf.Max(width, height));
                int textureWidth = Mathf.Max(8, Mathf.CeilToInt(width * pixelsPerUnit) + 8);
                int textureHeight = Mathf.Max(8, Mathf.CeilToInt(height * pixelsPerUnit) + 8);
                var cameraObject = new GameObject("Text mask camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20f;
                camera.orthographicSize = textureHeight / pixelsPerUnit / units * 0.5f;
                camera.aspect = (float)textureWidth / textureHeight;
                camera.transform.position = bounds.center + Vector3.back * 10;
                target = RenderTexture.GetTemporary(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true);
                pixels.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
                pixels.Apply();
                Color32[] mask = pixels.GetPixels32();
                int stride = Mathf.Max(1, Mathf.RoundToInt(1f / settings.density));
                var points = new List<Vector3>();
                for (int y = stride / 2; y < textureHeight; y += stride)
                for (int x = stride / 2; x < textureWidth; x += stride)
                {
                    if (mask[y * textureWidth + x].a < 100) continue;
                    points.Add(new Vector3((x + 0.5f - textureWidth * 0.5f) / pixelsPerUnit,
                        (y + 0.5f - textureHeight * 0.5f) / pixelsPerUnit, 0));
                }
                if (points.Count == 0) throw new ArgumentException("火花の点が生成されませんでした。フォント素材と密度を確認してください。");
                if (points.Count > settings.maxPoints)
                    throw new ArgumentException("文字の点数が" + points.Count + "点で、上限" + settings.maxPoints + "点を超えています。火花密度を下げてください。");
                // Center the actual sampled glyphs, rather than the padded texture.
                var min = points[0]; var max = min;
                foreach (Vector3 point in points) { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
                Vector3 center = (min + max) * 0.5f;
                for (int i = 0; i < points.Count; i++) points[i] -= center;
                return points;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (textMaterial != null) UnityEngine.Object.DestroyImmediate(textMaterial);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static Mesh CreateMesh(List<Vector3> points)
        {
            // All randomness is generated once, so clients display the same sparks.
            var random = new System.Random(7319);
            int total = points.Count + 180 + 1 + 70 + 1;
            var vertices = new Vector3[total * 4];
            var corners = new Vector2[total * 4];
            var data = new Vector2[total * 4];
            var indices = new int[total * 6];
            var quad = new[] { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
            float radius = 2f;
            foreach (Vector3 p in points) radius = Mathf.Max(radius, p.magnitude);
            for (int i = 0; i < total; i++)
            {
                int kind = i < points.Count ? 0 : i < points.Count + 180 ? 1 : i == points.Count + 180 ? 2 : i == total - 1 ? 4 : 3;
                float seed = (float)random.NextDouble();
                Vector3 destination = i < points.Count ? points[i] : new Vector3(
                    (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1,
                    (float)random.NextDouble() - 0.5f).normalized * radius * (0.3f + seed * 0.8f);
                for (int k = 0; k < 4; k++)
                {
                    vertices[i * 4 + k] = destination;
                    corners[i * 4 + k] = quad[k];
                    data[i * 4 + k] = new Vector2(seed, kind);
                }
                int v = i * 4, t = i * 6;
                indices[t] = v; indices[t+1] = v+1; indices[t+2] = v+2;
                indices[t+3] = v; indices[t+4] = v+2; indices[t+5] = v+3;
            }
            var mesh = new Mesh { name = "Baked Text Sparks", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = corners;
            mesh.uv2 = data;
            mesh.triangles = indices;
            // The vertex shader moves the quads far beyond their baked positions.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
            return mesh;
        }

        public static Material CreateMaterial(FireworkSettings settings)
        {
            Shader shader = Shader.Find("TextureFireWorks/Text Sparks");
            if (shader == null) throw new InvalidOperationException("文字花火のシェーダーが見つかりません。");
            var material = new Material(shader) { name = "Text Sparks" };
            material.SetColor("_Color", settings.color);
            material.SetFloat("_SparkSize", settings.sparkSize);
            material.SetFloat("_Gravity", settings.gravity);
            material.SetFloat("_Elapsed", -1);
            material.SetVector("_Durations", settings.Durations);
            material.SetVector("_LaunchPoint", Vector3.zero);
            material.SetVector("_BurstPoint", Vector3.up * settings.launchHeight);
            return material;
        }
    }
}

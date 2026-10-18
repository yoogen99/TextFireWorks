using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace TextureFireWorks.Editor
{
    [CustomEditor(typeof(FireworkLauncher))]
    public class FireworkLauncherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("「発射位置」「開花位置」をScene上で移動できます。文字面は花火の親の向きに従います。青い矢印が観覧側です。", MessageType.Info);
            if (EditorApplication.isPlaying && GUILayout.Button("打ち上げる"))
                ((FireworkLauncher)target).SendCustomEvent(nameof(FireworkLauncher.RequestLaunch));
        }

        private void OnSceneGUI()
        {
            var launcher = (FireworkLauncher)target;
            MoveMarker(launcher.launchPoint, "発射位置");
            MoveMarker(launcher.burstPoint, "開花位置");
            if (launcher.launchPoint == null || launcher.burstPoint == null) return;
            Handles.color = new Color(1, 0.65f, 0.2f);
            Handles.DrawDottedLine(launcher.launchPoint.position, launcher.burstPoint.position, 4);
            Handles.color = Color.cyan;
            Vector3 audience = -launcher.transform.forward;
            Handles.ArrowHandleCap(0, launcher.burstPoint.position, Quaternion.LookRotation(audience), 3, EventType.Repaint);
        }

        private static void MoveMarker(Transform marker, string label)
        {
            if (marker == null) return;
            Handles.Label(marker.position + Vector3.up * 0.4f, label);
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(marker.position, marker.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(marker, label + "を移動");
                marker.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            }
        }
    }
}

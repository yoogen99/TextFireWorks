using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace TextureFireWorks.Editor
{
    // A unitypackage is a gzip-compressed tar of GUID/{asset,asset.meta,pathname}.
    // Map UPM source paths to Assets so the same tools can be imported without UPM.
    public static class FireworkPackageExporter
    {
        public const string PackageRoot = "Packages/com.maita.texture-fireworks";

        [MenuItem("Tools/TextureFireWorks/導入用unitypackageを書き出す")]
        private static void ExportMenu()
        {
            string path = EditorUtility.SaveFilePanel("導入用パッケージを書き出す", "", "TextureFireWorks", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
            string prefab = selected.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? selected : null;
            try
            {
                Export(path, prefab);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e) { EditorUtility.DisplayDialog("書き出しに失敗", e.Message, "閉じる"); }
        }

        public static void Export(string output, string selectedPrefab = null)
        {
            AssetDatabase.SaveAssets();
            string sourcePath = AssetDatabase.GUIDToAssetPath("6a697c94df9a447d981c211bb524ed12");
            if (string.IsNullOrEmpty(sourcePath)) throw new InvalidOperationException("パッケージのソースを見つけられませんでした。");
            string sourceRoot = sourcePath.Substring(0, sourcePath.LastIndexOf("/Runtime/", StringComparison.Ordinal));
            string resolvedRoot = sourceRoot.StartsWith("Packages/", StringComparison.Ordinal)
                ? UnityEditor.PackageManager.PackageInfo.FindForAssetPath(sourceRoot).resolvedPath : Path.GetFullPath(sourceRoot);
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string absolute in Directory.GetFiles(resolvedRoot, "*", SearchOption.AllDirectories))
            {
                string relative = absolute.Substring(resolvedRoot.Length + 1).Replace('\\', '/');
                if (relative.EndsWith(".meta", StringComparison.Ordinal) || relative.Contains("~")) continue;
                if (!File.Exists(absolute + ".meta")) continue;
                files["Assets/TextureFireWorks/Package/" + relative] = absolute;
            }
            // Export only runtime dependencies of the selected Prefab, excluding authoring fonts.
            if (!string.IsNullOrEmpty(selectedPrefab))
            {
                foreach (string dependency in AssetDatabase.GetDependencies(selectedPrefab, true))
                {
                    if (!dependency.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    if (dependency.EndsWith("Settings.asset", StringComparison.Ordinal)) continue;
                    if (dependency.StartsWith(sourceRoot + "/", StringComparison.Ordinal)) continue;
                    if (File.Exists(dependency + ".meta")) files[dependency] = Path.GetFullPath(dependency);
                }
            }
            using (var stream = new FileStream(output, FileMode.CreateNew))
            using (var gzip = new GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal))
            {
                foreach (var entry in files)
                {
                    string metadata = File.ReadAllText(entry.Value + ".meta");
                    string guid = ReadGuid(metadata);
                    WriteEntry(gzip, guid + "/asset", File.ReadAllBytes(entry.Value));
                    WriteEntry(gzip, guid + "/asset.meta", Encoding.UTF8.GetBytes(metadata));
                    WriteEntry(gzip, guid + "/pathname", Encoding.UTF8.GetBytes(entry.Key));
                }
                gzip.Write(new byte[1024], 0, 1024);
            }
        }

        private static string ReadGuid(string metadata)
        {
            foreach (string line in metadata.Split('\n'))
                if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            throw new InvalidDataException("GUIDのないファイルがあります。");
        }

        private static void WriteEntry(Stream stream, string name, byte[] bytes)
        {
            byte[] header = new byte[512];
            Put(header, 0, name);
            Put(header, 100, "0000644\0");
            Put(header, 108, "0000000\0");
            Put(header, 116, "0000000\0");
            Put(header, 124, Convert.ToString(bytes.LongLength, 8).PadLeft(11, '0') + "\0");
            Put(header, 136, "00000000000\0");
            for (int i = 148; i < 156; i++) header[i] = 32;
            header[156] = (byte)'0';
            Put(header, 257, "ustar\0");
            Put(header, 263, "00");
            int checksum = 0;
            foreach (byte b in header) checksum += b;
            Put(header, 148, Convert.ToString(checksum, 8).PadLeft(6, '0') + "\0 ");
            stream.Write(header, 0, header.Length);
            stream.Write(bytes, 0, bytes.Length);
            int padding = (512 - bytes.Length % 512) % 512;
            if (padding > 0) stream.Write(new byte[padding], 0, padding);
        }

        private static void Put(byte[] header, int offset, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, header, offset, bytes.Length);
        }
    }
}

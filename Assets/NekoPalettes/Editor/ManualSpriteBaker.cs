#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoPalettes.Editor
{
    public static class ManualSpriteBaker
    {
        [MenuItem("Tools/NekoPalettes/Bake Sprites in Selected Folder")]
        public static void BakeSelectedFolder()
        {
            Object selectedObject = Selection.activeObject;
            string folderPath = AssetDatabase.GetAssetPath(selectedObject);

            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog(
                    "NekoPalettes Baker",
                    "Please select a valid folder in the Project window before running this tool.",
                    "OK"
                );
                return;
            }

            // Select Base Palette Texture
            string basePalettePath = EditorUtility.OpenFilePanel("Select Base Palette Texture", "Assets", "png");
            if (string.IsNullOrEmpty(basePalettePath)) return;

            // Convert absolute system path to Unity relative path
            if (basePalettePath.StartsWith(Application.dataPath))
            {
                basePalettePath = "Assets" + basePalettePath.Substring(Application.dataPath.Length);
            }

            // Ensure Base Palette texture is readable
            BakeUtility.EnsureTextureIsReadable(basePalettePath);

            Texture2D basePaletteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePalettePath);
            if (basePaletteTex == null)
            {
                Debug.LogError("[NekoPalettes] Invalid Base Palette selected.");
                return;
            }

            bool deleteOriginals = EditorUtility.DisplayDialog(
                "Delete Original Sprites?",
                "Do you want to delete the original source PNG files after baking them to indexed textures?",
                "Delete Originals",
                "Keep Originals"
            );

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            int processedCount = 0;
            List<string> originalFilesToDelete = new List<string>();

            // Extract base colors array from palette
            Color[] baseColors = basePaletteTex.GetPixels();

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                    // Skip the base palette file itself and already indexed textures
                    if (assetPath == basePalettePath || assetPath.EndsWith("_Indexed.png")) continue;

                    EditorUtility.DisplayProgressBar(
                        "Baking Sprites",
                        $"Processing: {Path.GetFileName(assetPath)}",
                        (float)i / guids.Length
                    );

                    string dir = Path.GetDirectoryName(assetPath);
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string outputPath = Path.Combine(dir, $"{fileName}_Indexed.png");

                    BakeUtility.EnsureTextureIsReadable(assetPath);
                    BakeUtility.BakeTextureToIndexed(assetPath, baseColors, outputPath);
                    processedCount++;

                    if (deleteOriginals)
                    {
                        originalFilesToDelete.Add(assetPath);
                    }
                }

                if (deleteOriginals && originalFilesToDelete.Count > 0)
                {
                    foreach (string fileToDelete in originalFilesToDelete)
                    {
                        AssetDatabase.DeleteAsset(fileToDelete);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[NekoPalettes] Bake complete! Processed {processedCount} sprite sheet(s) against base palette '{basePaletteTex.name}'.");
        }
    }
}
#endif

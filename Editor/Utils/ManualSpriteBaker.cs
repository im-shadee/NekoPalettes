#if UNITY_EDITOR
using NekoPalettes.Internal;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoPalettes.Editor
{
    /// <summary>
    /// Editor utility class that allows developers to manually batch-bake 
    /// sprite textures within a selected project folder into indexed UV textures.
    /// </summary>
    public static class ManualSpriteBaker
    {
        /// <summary>
        /// MenuItem action that processes all valid Texture2D assets within a user-selected 
        /// Project folder, re-encoding them based on a target base palette texture.
        /// </summary>
        [MenuItem("Tools/NekoPalettes/Bake Sprites in Selected Folder")]
        public static void BakeSelectedFolder()
        {
            // Shade: Retrieve active selected item in Unity Project browser window
            Object selectedObject = Selection.activeObject;
            string folderPath = AssetDatabase.GetAssetPath(selectedObject);

            // Shade: Validate that the selection is a non-empty directory path on disk
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog(
                    "NekoPalettes Baker",
                    "Please select a valid folder in the Project window before running this tool.",
                    "OK"
                );
                return;
            }

            // Shade: Open file picker dialog so user can designate a base palette PNG file
            string basePalettePath = EditorUtility.OpenFilePanel("Select Base Palette Texture", "Assets", "png");
            if (string.IsNullOrEmpty(basePalettePath)) return;

            // Shade: Convert system OS absolute path into Unity project relative path ("Assets/...")
            if (basePalettePath.StartsWith(Application.dataPath))
            {
                basePalettePath = "Assets" + basePalettePath.Substring(Application.dataPath.Length);
            }

            // Shade: Guarantee CPU Read/Write settings on the selected base palette asset
            BakeUtility.EnsureTextureIsReadable(basePalettePath);

            // Shade: Load base palette Texture2D asset from AssetDatabase
            Texture2D basePaletteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePalettePath);
            if (basePaletteTex == null)
            {
                NekoPaletteDebug.LogError("Invalid Base Palette selected.");
                return;
            }

            // Shade: Prompt user regarding deletion of original unbaked sprite source files
            bool deleteOriginals = EditorUtility.DisplayDialog(
                "Delete Original Sprites?",
                "Do you want to delete the original source PNG files after baking them to indexed textures?",
                "Delete Originals",
                "Keep Originals"
            );

            // Shade: Search designated folder for all Texture2D GUIDs
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            int processedCount = 0;
            List<string> originalFilesToDelete = new List<string>();

            // Shade: Retrieve reference colors from the selected base palette texture
            Color[] baseColors = basePaletteTex.GetPixels();

            try
            {
                // Shade: Loop through all discovered texture asset GUIDs in selected folder
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                    // Shade: Skip base palette source file and assets that are already baked
                    if (assetPath == basePalettePath || assetPath.EndsWith("_Indexed.png")) continue;

                    // Shade: Render progress bar in Unity Editor GUI
                    EditorUtility.DisplayProgressBar(
                        "Baking Sprites",
                        $"Processing: {Path.GetFileName(assetPath)}",
                        (float)i / guids.Length
                    );

                    // Shade: Construct output target file path for indexed replacement asset
                    string dir = Path.GetDirectoryName(assetPath);
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    string outputPath = Path.Combine(dir, $"{fileName}_Indexed.png");

                    // Shade: Validate source texture readability and execute bake process
                    BakeUtility.EnsureTextureIsReadable(assetPath);
                    BakeUtility.BakeTextureToIndexed(assetPath, baseColors, outputPath);
                    processedCount++;

                    // Shade: Queue source path for deletion if user selected cleanup option
                    if (deleteOriginals)
                    {
                        originalFilesToDelete.Add(assetPath);
                    }
                }

                // Shade: Perform batch deletion of source original textures if requested
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
                // Shade: Clear active progress bar display and synchronize AssetDatabase state
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            // Shade: Log batch baking process summary results to debug console
            NekoPaletteDebug.Log($"Bake complete! Processed {processedCount} sprite sheet(s) against base palette '{basePaletteTex.name}'.");
        }
    }
}
#endif

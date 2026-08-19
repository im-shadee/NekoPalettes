#if UNITY_EDITOR
using NekoPalettes.Internal;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoPalettes.Editor
{
    public class PaletteGeneratorWindow : EditorWindow
    {
        // Shade: Reference to the assigned source sprite texture asset
        private Texture2D m_SourceTexture = null;

        // Shade: Target project folder path for saving generated palette assets
        private string m_SaveFolderPath = "Assets/NekoPalettes/Palettes/";

        // Shade: Base filename for generated palette textures
        private string m_PaletteName = "NewPalette";

        // Shade: Extracted original colors present in the source sprite
        private readonly List<Color> m_BasePaletteColors = new List<Color>();

        // Shade: Editable target colors mapped 1-to-1 with base palette indices
        private readonly List<Color> m_NewPaletteColors = new List<Color>();

        // Shade: Scroll position vector for the swatch list GUI
        private Vector2 m_ScrollPosition = Vector2.zero;

        // Shade: Runtime texture used to display real-time recoloring in the window
        private Texture2D m_PreviewTexture = null;

        /// <summary>
        /// Opens and initializes the Palette Generator EditorWindow instance.
        /// </summary>
        [MenuItem("Tools/NekoPalettes/Palette Generator and Editor")]
        public static void ShowWindow()
        {
            // Shade: Create our palette editor window of size 450x550 by default
            PaletteGeneratorWindow window = GetWindow<PaletteGeneratorWindow>("Palette Editor");
            window.minSize = new Vector2(450, 550);
        }

        private void OnEnable()
        {
            // Shade: Auto-select sprite if one is highlighted in the Project window when opening
            if (Selection.activeObject is Texture2D tex)
            {
                m_SourceTexture = tex;
                ExtractBasePalette();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("Interactive Palette Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Shade: Track changes to the assigned source texture field
            EditorGUI.BeginChangeCheck();
            m_SourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Source Sprite Texture",
                m_SourceTexture,
                typeof(Texture2D),
                allowSceneObjects: false
            );

            // Shade: Automatically extract unique colors as soon as a new sprite is dropped into the slot
            if (EditorGUI.EndChangeCheck() && m_SourceTexture != null)
            {
                ExtractBasePalette();
            }

            // Shade: Guard against null selection before drawing palette controls
            if (m_SourceTexture == null)
            {
                EditorGUILayout.HelpBox("Assign a source sprite texture above to extract its palette and begin editing.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10);
            m_PaletteName = EditorGUILayout.TextField("New Palette Name", m_PaletteName);
            m_SaveFolderPath = EditorGUILayout.TextField("Save Folder Path", m_SaveFolderPath);
            EditorGUILayout.Space(10);

            // Shade: Render editable color swatches if a palette has been extracted
            if (m_BasePaletteColors.Count > 0)
            {
                DrawPaletteSwatches();
            }

            EditorGUILayout.Space(10);

            // Shade: Display the recolored sprite preview frame
            DrawSpritePreview();

            EditorGUILayout.Space(15);

            // Shade: Export button for the 1xN palette asset with a hover tooltip
            GUIContent paletteButtonContent = new GUIContent(
                "Save New Palette Asset (.png)",
                "Generates and saves a 1xN palette texture asset containing your edited color swatches."
            );

            if (GUILayout.Button(paletteButtonContent, GUILayout.Height(35)))
            {
                SavePaletteAsPNG();
            }

            // Shade: Export button for the indexed sprite with a hover tooltip
            GUIContent bakeButtonContent = new GUIContent(
                "Bake Indexed Sprite Texture",
                "Creates a linear sprite texture encoding palette indices in the Red channel for O(1) shader lookups. This considerably reduces the sprite's size on disk."
            );

            if (GUILayout.Button(bakeButtonContent, GUILayout.Height(25)))
            {
                BakeIndexedSprite();
            }
        }

        /// <summary>
        /// Reads unique pixel colors from the source texture, sorts them by HSV, and builds the baseline color set.
        /// </summary>
        private void ExtractBasePalette()
        {
            // Shade: Force read/write access on the texture so GetPixels32 doesn't crash at runtime
            EnsureTextureIsReadable(m_SourceTexture);

            // Shade: Fetch raw 32-bit pixel data (0-255 range) to avoid float precision issues during color comparison
            Color32[] pixels = m_SourceTexture.GetPixels32();
            List<Color32> uniqueColors = new List<Color32>();

            // Shade: Iterate over every pixel in the texture to collect unique solid colors
            foreach (Color32 pixel in pixels)
            {
                // Shade: Completely ignore transparent pixels so invisible background space isn't treated as a palette slot
                if (pixel.a == 0) continue;

                // Shade: Only store unique color entries to form the base palette
                if (!uniqueColors.Contains(pixel))
                {
                    uniqueColors.Add(pixel);
                }
            }

            // Shade: Sort colors by Hue first, then Brightness, then Saturation
            uniqueColors.Sort((c1, c2) =>
            {
                Color color1 = c1;
                Color color2 = c2;
                Color.RGBToHSV(color1, out float h1, out float s1, out float v1);
                Color.RGBToHSV(color2, out float h2, out float s2, out float v2);

                int hueCompare = h1.CompareTo(h2);
                if (hueCompare != 0) return hueCompare;

                int valCompare = v1.CompareTo(v2);
                if (valCompare != 0) return valCompare;

                return s1.CompareTo(s2);
            });

            // Shade: Reset internal palette lists before populating with newly extracted colors
            m_BasePaletteColors.Clear();
            m_NewPaletteColors.Clear();

            // Shade: Copy extracted colors to both base and editable palette lists
            foreach (Color32 color in uniqueColors)
            {
                m_BasePaletteColors.Add(color);
                m_NewPaletteColors.Add(color);
            }

            // Shade: Auto-generate a default export filename based on the source texture name
            m_PaletteName = $"{m_SourceTexture.name}_AltPalette";

            // Shade: Immediately generate the initial preview using the extracted colors
            UpdatePreviewTexture();
        }

        /// <summary>
        /// Renders the GUI scroll view containing base colors and editable target color fields.
        /// </summary>
        private void DrawPaletteSwatches()
        {
            GUILayout.Label($"Palette Swatches ({m_BasePaletteColors.Count} Colors)", EditorStyles.boldLabel);

            // Shade: Restrict vertical height and enable scrolling so large palettes don't push controls off screen
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(150));

            EditorGUI.BeginChangeCheck();

            // Shade: Render each color index slot as a read-only base color and an editable target color
            for (int i = 0; i < m_BasePaletteColors.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // Shade: Display zero-padded slot index for clean alignment (e.g., Index 01, Index 02)
                EditorGUILayout.LabelField($"Index {i:D2}", GUILayout.Width(60));

                // Shade: Show original base color swatch (disabled input to prevent accidental editing)
                EditorGUILayout.ColorField(GUIContent.none, m_BasePaletteColors[i], false, false, false, GUILayout.Width(50));

                EditorGUILayout.LabelField("->", GUILayout.Width(20));

                // Shade: Active color field allowing real-time editing of the target palette slot
                m_NewPaletteColors[i] = EditorGUILayout.ColorField(GUIContent.none, m_NewPaletteColors[i], false, true, false);

                EditorGUILayout.EndHorizontal();
            }

            // Shade: Refresh preview texture only when an actual color change occurs in the UI
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewTexture();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Renders the recolored texture inside the Editor Window GUI.
        /// </summary>
        private void DrawSpritePreview()
        {
            if (m_PreviewTexture == null) return;

            GUILayout.Label("Live Recolored Preview", EditorStyles.boldLabel);

            // Shade: Allocate a flexible square region in the GUI layout to display the preview texture
            Rect previewRect = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));

            // Shade: Draw preview with ScaleToFit to preserve original aspect ratio regardless of window resizing
            GUI.DrawTexture(previewRect, m_PreviewTexture, ScaleMode.ScaleToFit);
        }

        /// <summary>
        /// Re-maps colors on the preview texture to provide live feedback during editing.
        /// </summary>
        private void UpdatePreviewTexture()
        {
            if (m_SourceTexture == null) return;

            // Shade: Ensure CPU read permissions before calling GetPixels
            EnsureTextureIsReadable(m_SourceTexture);

            Color[] pixels = m_SourceTexture.GetPixels();
            Color[] recoloredPixels = new Color[pixels.Length];

            // Shade: Loop over every pixel and remap its base color to the corresponding new palette color
            for (int i = 0; i < pixels.Length; i++)
            {
                // Shade: Preserve fully transparent pixels without attempting color matching
                if (pixels[i].a == 0)
                {
                    recoloredPixels[i] = Color.clear;
                    continue;
                }

                // Shade: Find nearest matching slot in base palette and swap with new palette color
                int matchIndex = FindClosestColorIndex(pixels[i], m_BasePaletteColors);
                if (matchIndex >= 0 && matchIndex < m_NewPaletteColors.Count)
                {
                    Color swapped = m_NewPaletteColors[matchIndex];

                    // Shade: Retain original pixel alpha to preserve semi-transparent edges/anti-aliasing
                    swapped.a = pixels[i].a;
                    recoloredPixels[i] = swapped;
                }
                else
                {
                    recoloredPixels[i] = pixels[i];
                }
            }

            // Shade: Instantiate or reallocate preview texture if dimensions don't match source texture
            if (m_PreviewTexture == null || m_PreviewTexture.width != m_SourceTexture.width || m_PreviewTexture.height != m_SourceTexture.height)
            {
                m_PreviewTexture = new Texture2D(m_SourceTexture.width, m_SourceTexture.height, TextureFormat.RGBA32, false)
                {
                    // Shade: Force Point filtering so pixel art previews remain crisp and unblurred
                    filterMode = FilterMode.Point
                };
            }

            // Shade: Upload updated pixel array to GPU texture memory
            m_PreviewTexture.SetPixels(recoloredPixels);
            m_PreviewTexture.Apply();

            // Shade: Force immediate repaint of EditorWindow to reflect changes instantly
            Repaint();
        }

        /// <summary>
        /// Saves the active color swatches as a 1xN PNG palette texture file.
        /// Generates a unique path if a duplicate file exists.
        /// </summary>
        private void SavePaletteAsPNG()
        {
            if (m_NewPaletteColors.Count == 0) return;

            // Shade: Create 1xN pixel texture where width equals color count and height is 1 pixel
            Texture2D paletteTex = new Texture2D(m_NewPaletteColors.Count, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Shade: Write each color swatch left-to-right into individual pixel coordinates
            for (int i = 0; i < m_NewPaletteColors.Count; i++)
            {
                paletteTex.SetPixel(i, 0, m_NewPaletteColors[i]);
            }

            paletteTex.Apply();

            // Shade: Generate unique project-relative asset path to avoid overwriting existing files
            string targetAssetPath = GetUniqueTargetPath(m_SaveFolderPath, m_PaletteName, ".png");

            // Shade: Encode raw texture pixels to PNG binary format and write directly to disk
            byte[] bytes = paletteTex.EncodeToPNG();
            File.WriteAllBytes(targetAssetPath, bytes);

            // Shade: Trigger Unity AssetDatabase to scan and discover the newly created PNG file
            AssetDatabase.Refresh();

            // Shade: Fetch TextureImporter instance to configure optimal import settings for 1D palette sampling
            TextureImporter paletteImporter = AssetImporter.GetAtPath(targetAssetPath) as TextureImporter;
            if (paletteImporter != null)
            {
                paletteImporter.textureType = TextureImporterType.Default;
                paletteImporter.filterMode = FilterMode.Point; // Shade: Prevent bilinear blending between adjacent palette pixels
                paletteImporter.mipmapEnabled = false;         // Shade: Disable mipmaps as palette texture is never scaled in 3D space
                paletteImporter.textureCompression = TextureImporterCompression.Uncompressed; // Shade: Lossless color data
                paletteImporter.wrapMode = TextureWrapMode.Clamp; // Shade: Prevent UV wrapping overflow at texture edges
                paletteImporter.npotScale = TextureImporterNPOTScale.None; // Shade: Set to None instead of ToNearest to avoid smudging
                paletteImporter.SaveAndReimport();
            }

            NekoPaletteDebug.Log($"Saved palette asset ({m_NewPaletteColors.Count} colors) to: {targetAssetPath}");
        }

        /// <summary>
        /// Bakes the current source sprite into an indexed format with unique output path handling.
        /// </summary>
        private void BakeIndexedSprite()
        {
            if (m_SourceTexture == null || m_BasePaletteColors.Count == 0) return;

            // Shade: Route through the shared utility so the encoding here is byte-for-byte identical
            // to what ManualSpriteBaker produces - same half-texel index formula, same importer settings.
            string assetPath = AssetDatabase.GetAssetPath(m_SourceTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                NekoPaletteDebug.LogError("Source texture has no valid project asset path.");
                return;
            }

            // Shade: Generate unique project-relative asset path for the baked indexed texture
            string targetFileName = $"{m_SourceTexture.name}_Indexed";
            string outputPath = GetUniqueTargetPath(m_SaveFolderPath, targetFileName, ".png");

            BakeUtility.EnsureTextureIsReadable(assetPath);
            BakeUtility.BakeTextureToIndexed(assetPath, m_BasePaletteColors.ToArray(), outputPath);

            NekoPaletteDebug.Log($"Baked indexed sprite to: {outputPath}");
        }

        /// <summary>
        /// Calculates a unique project-relative file path for new assets.
        /// Appends standard numeric suffixes (_1, _2, etc.) if collision is detected.
        /// </summary>
        /// <param name="folderPath">The relative target folder path in the project.</param>
        /// <param name="baseName">The requested base file name without extension.</param>
        /// <param name="extension">File extension (e.g., ".png").</param>
        /// <returns>A valid, non-colliding Unity asset path.</returns>
        private string GetUniqueTargetPath(string folderPath, string baseName, string extension)
        {
            // Shade: Ensure directory structure exists on local storage
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Shade: Standardize path separators to Unity style forward slashes
            string sanitizedFolder = folderPath.Replace("\\", "/");
            if (!sanitizedFolder.EndsWith("/"))
            {
                sanitizedFolder += "/";
            }

            // Shade: Combine components into asset relative path format
            string defaultPath = $"{sanitizedFolder}{baseName}{extension}";

            // Shade: GenerateUniqueAssetPath appends _1, _2, etc., if defaultPath already exists
            return AssetDatabase.GenerateUniqueAssetPath(defaultPath);
        }

        /// <summary>
        /// Evaluates vector distance between pixel color and reference palette colors to return the index of the closest match.
        /// </summary>
        /// <param name="color">The source pixel color.</param>
        /// <param name="palette">The reference color palette array.</param>
        /// <returns>Index of the closest color entry.</returns>
        private int FindClosestColorIndex(Color color, List<Color> palette)
        {
            int bestIndex = 0;
            float minDistance = float.MaxValue;

            // Shade: Compare current RGB pixel color against every palette color entry
            for (int i = 0; i < palette.Count; i++)
            {
                // Shade: Calculate 3D Euclidean distance between pixel RGB and palette entry RGB
                float distance = Vector3.Distance(
                    new Vector3(color.r, color.g, color.b),
                    new Vector3(palette[i].r, palette[i].g, palette[i].b)
                );

                // Shade: Track closest matching palette index with smallest color distance
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Helper wrapper ensuring that a target Texture2D is configured to be CPU readable.
        /// </summary>
        /// <param name="texture">Target texture instance to check.</param>
        private void EnsureTextureIsReadable(Texture2D texture)
        {
            // Shade: Thin convenience wrapper - resolves the asset path then delegates to the
            // single shared implementation so there's only one place that touches import settings.
            if (texture == null) return;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return;

            BakeUtility.EnsureTextureIsReadable(path);
        }
    }
}
#endif

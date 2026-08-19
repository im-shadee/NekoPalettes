#if UNITY_EDITOR
using NekoPalettes.Internal;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace NekoPalettes.Editor
{
    public static class BakeUtility
    {
        /// <summary>
        /// Validates and ensures that a target texture asset is readable on the CPU,
        /// uncompressed, uses point filtering, and disables non-power-of-two scaling.
        /// </summary>
        /// <param name="assetPath">The relative project path to the texture asset (e.g., "Assets/Sprites/MySprite.png").</param>
        public static void EnsureTextureIsReadable(string assetPath)
        {
            // Shade: Load the importer for the target asset path
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            // Shade: Reimport asset only if any required import flags were mutated
            if (CheckReimportState(importer))
            {
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Evaluates importer properties and enforces required settings for palette processing.
        /// </summary>
        /// <param name="importer">The TextureImporter instance to evaluate and update.</param>
        /// <returns>True if any property was modified and requires a reimport; otherwise, false.</returns>
        private static bool CheckReimportState(TextureImporter importer)
        {
            bool shouldReimport = false;

            // Shade: Enable CPU read/write permissions for GetPixels access
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                shouldReimport = true;
            }

            // Shade: Enforce Point filtering to preserve sharp pixel boundaries
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                shouldReimport = true;
            }

            // Shade: Force uncompressed texture format to prevent color compression artifacts
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                shouldReimport = true;
            }

            // Shade: Disable NPOT scaling to prevent dimensions from distorting during import
            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                shouldReimport = true;
            }

            return shouldReimport;
        }

        /// <summary>
        /// Calculates the nearest palette index for a given RGB color using human-perception-weighted distance math.
        /// </summary>
        /// <param name="pixel">The target pixel color to evaluate.</param>
        /// <param name="palette">Array of reference palette colors to match against.</param>
        /// <returns>The zero-based index of the closest matching palette color.</returns>
        private static int GetClosestPaletteIndex(Color pixel, Color[] palette)
        {
            int bestIndex = 0;
            float minDistance = float.MaxValue;

            for (int p = 0; p < palette.Length; p++)
            {
                float rDiff = pixel.r - palette[p].r;
                float gDiff = pixel.g - palette[p].g;
                float bDiff = pixel.b - palette[p].b;

                // Shade: Weighted Euclidean distance according to human perceptual vision sensitivity (R:30%, G:59%, B:11%)
                float dist = (rDiff * rDiff * 0.3f) + (gDiff * gDiff * 0.59f) + (bDiff * bDiff * 0.11f);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = p;
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// Converts a source sprite texture into an indexed texture encoding UV lookups in the Red channel.
        /// Saves the generated image file to disk and configures importer properties and sprite slicer rects.
        /// </summary>
        /// <param name="assetPath">Path to the source texture asset.</param>
        /// <param name="baseColors">The base palette array used for color quantization and indexing.</param>
        /// <param name="outputPath">Destination path on disk where the baked PNG should be generated.</param>
        public static void BakeTextureToIndexed(string assetPath, Color[] baseColors, string outputPath)
        {
            // Shade: Validate input color array before proceeding
            if (baseColors == null || baseColors.Length == 0)
            {
                NekoPaletteDebug.LogError("Cannot bake: baseColors array is empty or null.");
                return;
            }

            if (PackageConfig.ENABLE_LOGS)
            {
                // Shade: Output diagnostic mapping data for debugging
                LogBakeDiagnostics(assetPath, baseColors);
            }

            // Shade: Load source texture asset
            Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (sourceTex == null) return;

            // Shade: Extract existing sprite mode and rect slicing metadata from source importer
            TextureImporter sourceImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            SpriteImportMode originalMode = SpriteImportMode.Single;
            SpriteRect[] sourceSpriteRects = FetchSpriteRects(sourceImporter, out originalMode);

            // Shade: Encode raw pixel colors into normalized Red channel indices
            Color[] indexedPixels = EncodePixelsToRedIndex(sourceTex.GetPixels(), baseColors);

            // Shade: Generate, save, and import the target PNG asset file on disk
            WriteAndImportTexture(sourceTex.width, sourceTex.height, indexedPixels, outputPath);

            // Shade: Configure destination importer flags (Linear sRGB=false, Point Filter, Uncompressed)
            ConfigureDestinationImporter(outputPath, originalMode);

            // Shade: Restore slice rectangles if source texture was set to Multiple Sprite mode
            RestoreSpriteSlicingData(outputPath, originalMode, sourceSpriteRects);
        }

        /// <summary>
        /// Reads sprite import modes and extracts sliced sprite rectangles for multi-sprite sheets.
        /// </summary>
        private static SpriteRect[] FetchSpriteRects(TextureImporter sourceImporter, out SpriteImportMode importMode)
        {
            importMode = SpriteImportMode.Single;
            if (sourceImporter == null) return null;

            importMode = sourceImporter.spriteImportMode;
            if (importMode != SpriteImportMode.Multiple) return null;

            // Shade: Access the SpriteEditorDataProvider factory to safely fetch sliced rect data
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(sourceImporter);

            if (dataProvider != null)
            {
                dataProvider.InitSpriteEditorDataProvider();
                return dataProvider.GetSpriteRects();
            }

            return null;
        }

        /// <summary>
        /// Transforms raw RGB pixels into single-channel normalized index representations stored in the Red channel.
        /// </summary>
        private static Color[] EncodePixelsToRedIndex(Color[] sourcePixels, Color[] baseColors)
        {
            int paletteSize = baseColors.Length;
            Color[] indexedPixels = new Color[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                // Shade: Retain transparent pixels without index evaluation
                if (sourcePixels[i].a < 0.01f)
                {
                    indexedPixels[i] = Color.clear;
                    continue;
                }

                // Shade: Calculate half-texel offset center for accurate linear UV lookup sampling in shaders
                int bestIndex = GetClosestPaletteIndex(sourcePixels[i], baseColors);
                float normalizedIndex = (bestIndex + 0.5f) / paletteSize;

                // Shade: Bake normalized UV value strictly into the Red channel
                indexedPixels[i] = new Color(normalizedIndex, 0f, 0f, sourcePixels[i].a);
            }

            return indexedPixels;
        }

        /// <summary>
        /// Constructs a Texture2D object from pixel data, encodes it to PNG format, and writes it to disk.
        /// </summary>
        private static void WriteAndImportTexture(int width, int height, Color[] pixels, string outputPath)
        {
            Texture2D outputTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            outputTex.SetPixels(pixels);
            outputTex.Apply();

            // Shade: Save binary file to project folder and refresh AssetDatabase
            File.WriteAllBytes(outputPath, outputTex.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Applies required sprite importer properties to ensure optimal shader sampling on the baked texture.
        /// </summary>
        private static void ConfigureDestinationImporter(string outputPath, SpriteImportMode importMode)
        {
            TextureImporter destImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (destImporter == null) return;

            destImporter.textureType = TextureImporterType.Sprite;
            destImporter.spriteImportMode = importMode;
            destImporter.filterMode = FilterMode.Point;
            destImporter.wrapMode = TextureWrapMode.Clamp;
            destImporter.npotScale = TextureImporterNPOTScale.None; // Shade: Prevent scaling NPOT textures so UV index math stays exact
            destImporter.alphaIsTransparency = true;
            destImporter.sRGBTexture = false; // Shade: Force linear color space to prevent sRGB gamma curve corruption
            destImporter.mipmapEnabled = false;

            // Shade: Enforce uncompressed format on all default platforms
            TextureImporterPlatformSettings defaultSettings = destImporter.GetDefaultPlatformTextureSettings();
            defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
            destImporter.SetPlatformTextureSettings(defaultSettings);

            destImporter.SaveAndReimport();
        }

        /// <summary>
        /// Re-applies extracted multi-sprite sheet slice rects to the newly imported target asset.
        /// </summary>
        private static void RestoreSpriteSlicingData(string outputPath, SpriteImportMode importMode, SpriteRect[] spriteRects)
        {
            if (importMode != SpriteImportMode.Multiple || spriteRects == null) return;

            TextureImporter destImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (destImporter == null) return;

            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider destDataProvider = factory.GetSpriteEditorDataProviderFromObject(destImporter);

            if (destDataProvider != null)
            {
                destDataProvider.InitSpriteEditorDataProvider();
                destDataProvider.SetSpriteRects(spriteRects);
                destDataProvider.Apply();
            }

            destImporter.SaveAndReimport();
        }

        /// <summary>
        /// Logs diagnostic info to the console, showing sample pixel mappings and normalized index encodings.
        /// </summary>
        /// <param name="assetPath">The path of the texture asset being inspected.</param>
        /// <param name="baseColors">The active base palette color set.</param>
        private static void LogBakeDiagnostics(string assetPath, Color[] baseColors)
        {
            Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (sourceTex == null) return;

            // Shade: Print complete color palette breakdown
            NekoPaletteDebug.Log($"Palette size: {baseColors.Length}");
            for (int p = 0; p < baseColors.Length; p++)
            {
                NekoPaletteDebug.Log($"  Palette [{p}]: RGB({baseColors[p].r:F2}, {baseColors[p].g:F2}, {baseColors[p].b:F2})");
            }

            // Shade: Sample up to the first 10 non-transparent pixels to verify half-texel offset math
            Color[] pixels = sourceTex.GetPixels();
            int sampleCount = Mathf.Min(10, pixels.Length);

            for (int i = 0; i < sampleCount; i++)
            {
                if (pixels[i].a < 0.01f) continue;

                int bestIndex = GetClosestPaletteIndex(pixels[i], baseColors);
                float normalizedIndex = (bestIndex + 0.5f) / baseColors.Length;

                NekoPaletteDebug.Log($"[Pixel {i}] RGB: ({pixels[i].r:F2}, {pixels[i].g:F2}, {pixels[i].b:F2}) -> Best Index: {bestIndex}/{baseColors.Length} -> Normalized Red: {normalizedIndex:F4}");
            }
        }
    }
}
#endif

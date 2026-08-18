#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace NekoPalettes.Editor
{
    public static class BakeUtility
    {
        public static void EnsureTextureIsReadable(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            if (CheckReimportState(importer))
            {
                importer.SaveAndReimport();
            }
        }

        private static bool CheckReimportState(TextureImporter importer)
        {
            bool shouldReimport = false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                shouldReimport = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                shouldReimport = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                shouldReimport = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                shouldReimport = true;
            }

            return shouldReimport;
        }

        private static int GetClosestPaletteIndex(Color pixel, Color[] palette)
        {
            int bestIndex = 0;
            float minDistance = float.MaxValue;

            for (int p = 0; p < palette.Length; p++)
            {
                float rDiff = pixel.r - palette[p].r;
                float gDiff = pixel.g - palette[p].g;
                float bDiff = pixel.b - palette[p].b;

                // Shade: Weighted Euclidean distance for human vision sensitivity
                float dist = (rDiff * rDiff * 0.3f) + (gDiff * gDiff * 0.59f) + (bDiff * bDiff * 0.11f);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestIndex = p;
                }
            }
            return bestIndex;
        }

        public static void LogBakeDiagnostics(string assetPath, Color[] baseColors)
        {
            Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (sourceTex == null) return;

            Debug.Log($"[NekoBakeDiagnostics] Palette size: {baseColors.Length}");
            for (int p = 0; p < baseColors.Length; p++)
            {
                Debug.Log($"  Palette [{p}]: RGB({baseColors[p].r:F2}, {baseColors[p].g:F2}, {baseColors[p].b:F2})");
            }

            Color[] pixels = sourceTex.GetPixels();
            int sampleCount = Mathf.Min(10, pixels.Length);

            for (int i = 0; i < sampleCount; i++)
            {
                if (pixels[i].a < 0.01f) continue;

                int bestIndex = GetClosestPaletteIndex(pixels[i], baseColors);
                float normalizedIndex = (bestIndex + 0.5f) / baseColors.Length;

                Debug.Log($"[Pixel {i}] RGB: ({pixels[i].r:F2}, {pixels[i].g:F2}, {pixels[i].b:F2}) -> Best Index: {bestIndex}/{baseColors.Length} -> Normalized Red: {normalizedIndex:F4}");
            }
        }

        public static void BakeTextureToIndexed(string assetPath, Color[] baseColors, string outputPath)
        {
            if (baseColors == null || baseColors.Length == 0)
            {
                Debug.LogError("[NekoPalettes] Cannot bake: baseColors array is empty or null.");
                return;
            }

            LogBakeDiagnostics(assetPath, baseColors);

            Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (sourceTex == null) return;

            int paletteSize = baseColors.Length;

            TextureImporter sourceImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            SpriteImportMode originalMode = SpriteImportMode.Single;
            SpriteRect[] sourceSpriteRects = null;

            if (sourceImporter != null)
            {
                originalMode = sourceImporter.spriteImportMode;
                if (originalMode == SpriteImportMode.Multiple)
                {
                    SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
                    factory.Init();
                    ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(sourceImporter);
                    if (dataProvider != null)
                    {
                        dataProvider.InitSpriteEditorDataProvider();
                        sourceSpriteRects = dataProvider.GetSpriteRects();
                    }
                }
            }

            Color[] pixels = sourceTex.GetPixels();
            Color[] indexedPixels = new Color[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 0.01f)
                {
                    indexedPixels[i] = Color.clear;
                    continue;
                }

                int bestIndex = GetClosestPaletteIndex(pixels[i], baseColors);
                float normalizedIndex = (bestIndex + 0.5f) / paletteSize;
                indexedPixels[i] = new Color(normalizedIndex, 0f, 0f, pixels[i].a);
            }

            Texture2D outputTex = new Texture2D(sourceTex.width, sourceTex.height, TextureFormat.RGBA32, false);
            outputTex.SetPixels(indexedPixels);
            outputTex.Apply();

            File.WriteAllBytes(outputPath, outputTex.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            TextureImporter destImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;

            if (destImporter == null) return;

            destImporter.textureType = TextureImporterType.Sprite;
            destImporter.spriteImportMode = originalMode;
            destImporter.filterMode = FilterMode.Point;
            destImporter.wrapMode = TextureWrapMode.Clamp;
            destImporter.alphaIsTransparency = true;
            destImporter.sRGBTexture = false;
            destImporter.mipmapEnabled = false;

            TextureImporterPlatformSettings defaultSettings = destImporter.GetDefaultPlatformTextureSettings();
            defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
            destImporter.SetPlatformTextureSettings(defaultSettings);

            destImporter.SaveAndReimport();

            if (originalMode == SpriteImportMode.Multiple && sourceSpriteRects != null)
            {
                SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
                factory.Init();
                ISpriteEditorDataProvider destDataProvider = factory.GetSpriteEditorDataProviderFromObject(destImporter);

                if (destDataProvider != null)
                {
                    destDataProvider.InitSpriteEditorDataProvider();
                    destDataProvider.SetSpriteRects(sourceSpriteRects);
                    destDataProvider.Apply();
                }

                destImporter.SaveAndReimport();
            }
        }
    }
}
#endif

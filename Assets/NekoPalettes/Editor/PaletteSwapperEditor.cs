#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NekoPalettes.Runtime;

namespace NekoPalettes.Editor
{
    [CustomEditor(typeof(PaletteSwapper))]
    public class PaletteSwapperEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Shade: Draw standard fields (Base Palette, Alt Palettes, Material, Index)
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            PaletteSwapper swapper = (PaletteSwapper)target;

            // Shade: Manual trigger button to apply palette updates on demand
            if (GUILayout.Button("Apply Palette Changes", GUILayout.Height(30)))
            {
                swapper.ApplyPalette();
                EditorUtility.SetDirty(swapper);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NekoPalettes.Runtime;

namespace NekoPalettes.Editor
{
    [CustomEditor(typeof(ColorRemover))]
    public class NekoColorRemoverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Shade: Draw default serialized properties (Key Color, Tolerance, Material)
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            ColorRemover remover = (ColorRemover)target;

            // Shade: Manual trigger button matching PaletteSwapperEditor's design
            if (GUILayout.Button("Apply Color Removal Changes", GUILayout.Height(30)))
            {
                remover.ApplyChromaKey();
                EditorUtility.SetDirty(remover);
            }
        }
    }
}
#endif

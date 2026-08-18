using UnityEngine;

namespace NekoPalettes.Runtime
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ColorRemover : MonoBehaviour
    {
        // Shade: Cached shader property IDs to avoid string hashing overhead
        private static readonly int m_KeyColorProp = Shader.PropertyToID("_KeyColor");
        private static readonly int m_ToleranceProp = Shader.PropertyToID("_Tolerance");

        private SpriteRenderer m_SpriteRenderer = null;
        private MaterialPropertyBlock m_PropertyBlock = null;

        [Header("Chroma Key Settings")]
        [SerializeField, Tooltip("The solid background color to key out and make transparent.")]
        private Color m_KeyColor = Color.magenta;

        [SerializeField, Range(0f, 1f), Tooltip("Tolerance threshold for color matching. Higher values key out near-matches.")]
        private float m_Tolerance = 0.05f;

        [Header("Shader References")]
        [SerializeField, Tooltip("Material assigned to this renderer (Neko/ChromaKey or Neko/PaletteSwap_ChromaKey).")]
        private Material m_ChromaMaterial = null;

        public Color KeyColor
        {
            get => m_KeyColor;
            set { m_KeyColor = value; ApplyChromaKey(); }
        }

        public float Tolerance
        {
            get => m_Tolerance;
            set { m_Tolerance = Mathf.Clamp01(value); ApplyChromaKey(); }
        }

        private void Awake()
        {
            m_SpriteRenderer = GetComponent<SpriteRenderer>();
            m_PropertyBlock = new MaterialPropertyBlock();

            // Shade: Assign material if specified
            if (m_ChromaMaterial != null)
            {
                m_SpriteRenderer.sharedMaterial = m_ChromaMaterial;
            }

            ApplyChromaKey();
        }

        /// <summary>
        /// Applies the key color and tolerance to the SpriteRenderer via MaterialPropertyBlock.
        /// Preserves existing MaterialPropertyBlock data to work side-by-side with PaletteSwapper.
        /// </summary>
        public void ApplyChromaKey()
        {
            if (m_SpriteRenderer == null) m_SpriteRenderer = GetComponent<SpriteRenderer>();
            m_PropertyBlock ??= new MaterialPropertyBlock();

            // Shade: Get existing block to preserve properties added by other scripts (e.g. PaletteSwapper)
            m_SpriteRenderer.GetPropertyBlock(m_PropertyBlock);

            m_PropertyBlock.SetColor(m_KeyColorProp, m_KeyColor);
            m_PropertyBlock.SetFloat(m_ToleranceProp, m_Tolerance);

            // Shade: Commit updated block back to the renderer
            m_SpriteRenderer.SetPropertyBlock(m_PropertyBlock);
        }
    }
}

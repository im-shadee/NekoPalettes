using UnityEngine;

namespace NekoPalettes.Runtime
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PaletteSwapper : MonoBehaviour
    {
        // Shade: Cached property IDs for performance (avoiding string hash lookups every frame)
        private static readonly int m_ActivePaletteProp = Shader.PropertyToID("_PaletteTex");
        private static readonly int m_PaletteSizeProp = Shader.PropertyToID("_PaletteSize");

        // Shade: Centralized base palette index for maintainability
        private const int m_BasePaletteIndex = 0;

        private SpriteRenderer m_SpriteRenderer = null;
        private MaterialPropertyBlock m_PropertyBlock = null;

        [Header("Palette Configuration")]
        [SerializeField, Tooltip("The base palette representing the original colors of the sprite (1xN texture).")]
        private Texture2D m_BasePalette = null;

        [SerializeField, Tooltip("List of alternative palettes to swap to (1xN textures).")]
        private Texture2D[] m_AltPalettes = null;

        [SerializeField, Min(m_BasePaletteIndex), Tooltip("The active palette index. 0 selects the Base Palette, 1+ selects an Alternative Palette. 0 by default.")]
        private int m_PaletteIndex = m_BasePaletteIndex;

        [Header("Shader References")]
        [SerializeField, Tooltip("Material using the PaletteSwap shader graph.")] 
        private Material m_PaletteMaterial = null;

        private void Awake()
        {
            m_SpriteRenderer = GetComponent<SpriteRenderer>();
            m_PropertyBlock = new MaterialPropertyBlock();

            // Shade: Assign material if set, otherwise instantiate instance
            if (m_PaletteMaterial != null)
            {
                m_SpriteRenderer.sharedMaterial = m_PaletteMaterial;
            }

            ApplyPalette();
        }

        private void OnValidate()
        {
            // Shade: Update live in the Editor when changing values in the Inspector
            ApplyPalette();
        }

        /// <summary>
        /// Sets the current palette index and updates the shader properties.
        /// <para>
        /// Index 0 = Base Palette, Index 1+ = Alternative Palettes array element (index - 1).
        /// </para>
        /// </summary>
        public void SetPaletteIndex(int index)
        {
            int maxIndex = (m_AltPalettes != null) ? m_AltPalettes.Length : m_BasePaletteIndex;

            // Shade: Clamp the index between the base palette index (0) and the length of alternative textures to avoid exceptions
            m_PaletteIndex = Mathf.Clamp(index, m_BasePaletteIndex, maxIndex);

            ApplyPalette();
        }

        /// <summary>
        /// Sets the current sprite's palette back to its default palette.
        /// </summary>
        public void ResetToBasePalette() => SetPaletteIndex(m_BasePaletteIndex);

        /// <summary>
        /// Applies the chosen palette to the SpriteRenderer via MaterialPropertyBlock.
        /// Uses MaterialPropertyBlock to prevent creating runtime material instances/leaks.
        /// </summary>
        public void ApplyPalette()
        {
            if (m_SpriteRenderer == null) m_SpriteRenderer = GetComponent<SpriteRenderer>();
            m_PropertyBlock ??= new MaterialPropertyBlock();

            // Shade: Get existing property block settings to preserve sprite renderer batches
            m_SpriteRenderer.GetPropertyBlock(m_PropertyBlock);

            // Shade: Pass palette width to shader
            if (m_BasePalette != null)
            {
                m_PropertyBlock.SetFloat(m_PaletteSizeProp, m_BasePalette.width);
            }

            // Shade: Determine active palette texture based on index AC
            Texture2D activePalette = GetActivePaletteTexture();
            if (activePalette != null)
            {
                m_PropertyBlock.SetTexture(m_ActivePaletteProp, activePalette);
            }

            // Shade: Apply block to renderer
            m_SpriteRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        private Texture2D GetActivePaletteTexture()
        {
            if (m_PaletteIndex <= m_BasePaletteIndex || m_AltPalettes == null || m_AltPalettes.Length == 0)
            {
                return m_BasePalette;
            }

            int targetArrayIndex = m_PaletteIndex - 1;
            if (targetArrayIndex >= m_BasePaletteIndex && targetArrayIndex < m_AltPalettes.Length)
            {
                return m_AltPalettes[targetArrayIndex];
            }

            // Shade: Fallback to base palette if out of bounds
            return m_BasePalette;
        }
    }
}

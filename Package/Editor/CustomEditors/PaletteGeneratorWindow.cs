#if UNITY_EDITOR
using NekoPalettes.Internal;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NekoPalettes.Editor
{
    public class PaletteGeneratorWindow : EditorWindow
    {
        // Shade: Reference to the assigned source sprite texture asset
        [SerializeField]
        private Texture2D m_SourceTexture = null;

        // Shade: Default project folder path for saving generated palette assets
        [SerializeField]
        private string m_SaveFolderPath = "Assets/NekoPalettes/Palettes/";

        // Shade: Base filename for generated palette textures
        [SerializeField]
        private string m_PaletteName = "NewPalette";

        // Shade: Extracted original colors present in the source sprite
        [SerializeField]
        private List<Color> m_BasePaletteColors = new List<Color>();

        // Shade: Editable target colors mapped 1-to-1 with base palette indices
        [SerializeField]
        private List<Color> m_NewPaletteColors = new List<Color>();

        // Shade: Runtime texture used to display real-time recoloring in the window
        [SerializeField]
        private Texture2D m_PreviewTexture = null;

        // Shade: Tracks whether each color swatch was manually added (true) or extracted (false)
        [SerializeField]
        private List<bool> m_IsCustomColor = new List<bool>();

        // Shade: Preview window interactive zoom variables
        [SerializeField]
        private float m_ZoomScale = 1.0f;

        [SerializeField]
        private Vector2 m_ZoomCenter = new Vector2(0.5f, 0.5f);

        // State variables for tracking multi-item drag-and-drop operations
        private bool m_IsDraggingMultiple = false;
        private bool m_DragArmed = false;
        private int m_PressedRowIndex = -1;
        private Vector2 m_DragStartPosition;
        private List<int> m_DraggedSourceIndices = new List<int>();
        private VisualElement m_DragProxyElement = null;

        // UI Toolkit Visual Elements
        private VisualElement m_ControlsContainer;
        private HelpBox m_HelpBox;
        private ListView m_SwatchListView;
        private Button m_RemoveSwatchButton;
        private VisualElement m_PreviewViewport;
        private Image m_PreviewImage;
        private Button m_ResetZoomButton;

        /// <summary>
        /// Mutable index holder attached to each recycled swatch row, so a single long-lived callback
        /// per field can always read the row's *current* logical index instead of one captured at bind time.
        /// </summary>
        private class SwatchRowState
        {
            public int Index = -1;
        }

        /// <summary>
        /// Opens and initializes the Palette Generator EditorWindow instance.
        /// </summary>
        [MenuItem("Tools/NekoPalettes/Palette Generator and Editor")]
        public static void ShowWindow()
        {
            // Shade: Create our palette editor window of size 450x650 by default
            PaletteGeneratorWindow window = GetWindow<PaletteGeneratorWindow>("Palette Editor");
            window.minSize = new Vector2(450, 650);
        }

        private void OnEnable()
        {
            // Shade: Auto-select sprite if one is highlighted in the Project window when opening
            if (Selection.activeObject is Texture2D tex && m_SourceTexture == null)
            {
                m_SourceTexture = tex;
                ExtractBasePalette();
            }

            // Register for global undo/redo callbacks
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            // Clean up callback to avoid memory leaks
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            // Refresh the ListView data binding and UI state
            if (m_SwatchListView != null)
            {
                m_SwatchListView.itemsSource = m_NewPaletteColors;
                m_SwatchListView.Rebuild();
                UpdateRemoveButtonState();
            }

            // Recalculate the texture preview with the undone/redone color states
            UpdatePreviewTexture();
        }

        /// <summary>
        /// Unity UI Toolkit lifecycle method for constructing the EditorWindow user interface.
        /// </summary>
        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            // Title Label
            Label titleLabel = new Label("Interactive Palette Generator");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 5;
            root.Add(titleLabel);

            // Source Texture Selector
            // Shade: Track changes to the assigned source texture field
            ObjectField sourceTextureField = new ObjectField("Source Sprite Texture")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = m_SourceTexture
            };
            sourceTextureField.RegisterValueChangedCallback(evt =>
            {
                m_SourceTexture = evt.newValue as Texture2D;

                // Shade: Automatically extract unique colors as soon as a new sprite is dropped into the slot
                if (m_SourceTexture != null)
                {
                    ExtractBasePalette();
                }

                RefreshUIState();
            });
            root.Add(sourceTextureField);

            // Shade: Guard against null selection before drawing palette controls
            m_HelpBox = new HelpBox("Assign a source sprite texture above to extract its palette and begin editing.", HelpBoxMessageType.Info);
            root.Add(m_HelpBox);

            // Main Editor Controls Container
            m_ControlsContainer = new VisualElement();
            m_ControlsContainer.style.marginTop = 10;
            m_ControlsContainer.style.flexGrow = 1;
            root.Add(m_ControlsContainer);

            // Palette Name & Save Path TextFields
            TextField paletteNameField = new TextField("New Palette Name") { value = m_PaletteName };
            paletteNameField.RegisterValueChangedCallback(evt => m_PaletteName = evt.newValue);
            m_ControlsContainer.Add(paletteNameField);

            TextField saveFolderField = new TextField("Save Folder Path") { value = m_SaveFolderPath };
            saveFolderField.RegisterValueChangedCallback(evt => m_SaveFolderPath = evt.newValue);
            saveFolderField.style.marginBottom = 10;
            m_ControlsContainer.Add(saveFolderField);

            // Shade: Vertical Splitter with native view data persistence enabled via viewDataKey
            TwoPaneSplitView splitView = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Vertical)
            {
                viewDataKey = "PaletteGeneratorWindow_SplitView"
            };
            splitView.style.flexGrow = 1;

            // --- Upper Pane: Swatch List & Actions ---
            VisualElement upperPane = new VisualElement();
            BuildSwatchListView();
            upperPane.Add(m_SwatchListView);

            // Shade: Swatch Action Toolbar (Add/Remove on left, Reset/Sort grouped on right)
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.SpaceBetween;
            toolbar.style.marginTop = 4;
            toolbar.style.marginBottom = 10;

            VisualElement addRemoveGroup = new VisualElement();
            addRemoveGroup.style.flexDirection = FlexDirection.Row;

            Button addSwatchButton = new Button(AddColorSwatch) { text = "+" };
            addSwatchButton.style.width = 30;
            addSwatchButton.style.minHeight = 22;
            addSwatchButton.style.height = 22;
            addRemoveGroup.Add(addSwatchButton);

            m_RemoveSwatchButton = new Button(RemoveSelectedColorSwatch) { text = "-" };
            m_RemoveSwatchButton.style.width = 30;
            m_RemoveSwatchButton.style.minHeight = 22;
            m_RemoveSwatchButton.style.height = 22;
            m_RemoveSwatchButton.SetEnabled(false);
            addRemoveGroup.Add(m_RemoveSwatchButton);

            toolbar.Add(addRemoveGroup);

            // Shade: Container for Reset and Sort buttons to keep them side-by-side on the right
            VisualElement rightButtonGroup = new VisualElement();
            rightButtonGroup.style.flexDirection = FlexDirection.Row;

            Button resetButton = new Button(ResetPaletteToDefault) { text = "Reset Colors to Original" };
            resetButton.style.width = 160;
            resetButton.style.minHeight = 22;
            resetButton.style.height = 22;
            resetButton.style.marginRight = 4;
            rightButtonGroup.Add(resetButton);

            Button sortColorsButton = new Button(SortColorsUsingPalette)
            {
                text = "Sort Colors Using Palette",
                tooltip = "Imports an external 1xN palette texture to sort existing colors and add any new ones."
            };
            sortColorsButton.style.height = 22;
            sortColorsButton.style.minHeight = 22;
            sortColorsButton.style.width = 160;
            rightButtonGroup.Add(sortColorsButton);

            toolbar.Add(rightButtonGroup);
            upperPane.Add(toolbar);

            splitView.Add(upperPane);
            upperPane.style.marginBottom = 10;

            // --- Lower Pane: Preview Viewport ---
            VisualElement lowerPane = new VisualElement();
            lowerPane.style.flexGrow = 1;
            lowerPane.style.marginTop = 10;
            lowerPane.style.minHeight = 200;

            Label previewHeaderLabel = new Label("Live Recolored Preview (Scroll to Zoom)");
            previewHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewHeaderLabel.style.marginBottom = 5;
            lowerPane.Add(previewHeaderLabel);

            BuildSpritePreviewUI();
            m_PreviewViewport.style.flexGrow = 1;
            lowerPane.Add(m_PreviewViewport);

            Button exportPaletteButton = new Button(SavePaletteAsPNG)
            {
                text = "Save New Palette Asset (.png)",
                tooltip = "Generates and saves a 1xN palette texture asset containing your edited color swatches."
            };
            exportPaletteButton.style.height = 35;
            exportPaletteButton.style.marginTop = 15;
            lowerPane.Add(exportPaletteButton);

            Button bakeIndexedButton = new Button(BakeIndexedSprite)
            {
                text = "Bake Indexed Sprite Texture",
                tooltip = "Creates a linear sprite texture encoding palette indices in the Red channel for O(1) shader lookups. This considerably reduces the sprite's size on disk."
            };
            bakeIndexedButton.style.height = 25;
            bakeIndexedButton.style.marginTop = 5;
            bakeIndexedButton.style.marginBottom = 5;
            lowerPane.Add(bakeIndexedButton);

            splitView.Add(lowerPane);
            m_ControlsContainer.Add(splitView);

            RefreshUIState();
            if (m_SourceTexture != null)
            {
                UpdatePreviewTexture();
            }
        }

        /// <summary>
        /// Updates element visibility based on texture selection state.
        /// </summary>
        private void RefreshUIState()
        {
            bool hasTexture = m_SourceTexture != null;
            m_HelpBox.style.display = hasTexture ? DisplayStyle.None : DisplayStyle.Flex;
            m_ControlsContainer.style.display = hasTexture ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Builds and initializes the UI Toolkit ListView for palette swatches with native selection and smooth visual drag proxy support.
        /// </summary>
        private void BuildSwatchListView()
        {
            m_SwatchListView = new ListView(m_NewPaletteColors, 24, MakeSwatchRow, BindSwatchRow)
            {
                reorderable = false,
                headerTitle = $"Palette Swatches ({m_BasePaletteColors.Count} Colors)",
                selectionType = SelectionType.Multiple,
                showAddRemoveFooter = false
            };
            m_SwatchListView.style.flexGrow = 1;
            m_SwatchListView.style.flexShrink = 1;

            m_SwatchListView.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                m_DragStartPosition = evt.position;
                m_IsDraggingMultiple = false;
                m_DraggedSourceIndices.Clear();

                // Shade: Only arm the reorder gesture when the press started on the row itself, not on
                // an interactive control like a color swatch field - otherwise opening a color picker
                // (which involves its own pointer movement) gets misread as "start dragging to reorder".
                m_DragArmed = !IsColorFieldOrDescendant(evt.target as VisualElement);
                m_PressedRowIndex = GetRowIndexAtPosition(evt.position);
            });

            m_SwatchListView.RegisterCallback<PointerMoveEvent>(evt =>
            {
                // Shade: If we haven't started dragging yet, check if we've passed the threshold
                if (!m_IsDraggingMultiple && m_DragArmed && Vector2.Distance(evt.position, m_DragStartPosition) > 6f)
                {
                    // Shade: Resolve the dragged set from the row that was actually pressed, not from
                    // whatever the selection happened to be beforehand. Only sweep the whole multi-selection
                    // along if the pressed row is part of it.
                    List<int> currentSelection = new List<int>(m_SwatchListView.selectedIndices);
                    m_DraggedSourceIndices = (m_PressedRowIndex >= 0 && currentSelection.Contains(m_PressedRowIndex))
                        ? currentSelection
                        : new List<int> { m_PressedRowIndex };
                    m_DraggedSourceIndices.Sort();

                    if (m_PressedRowIndex >= 0 && m_DraggedSourceIndices.Count > 0)
                    {
                        m_IsDraggingMultiple = true;
                        CreateDragProxy(evt.position);
                    }
                }

                if (m_IsDraggingMultiple && m_DragProxyElement != null)
                {
                    m_DragProxyElement.style.left = evt.position.x + 15;
                    m_DragProxyElement.style.top = evt.position.y + 15;
                }
            });

            m_SwatchListView.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!m_IsDraggingMultiple)
                {
                    m_DragArmed = false;
                    return;
                }

                RemoveDragProxy();

                int targetIndex = GetRowIndexAtPosition(evt.position);

                if (targetIndex >= 0 && m_DraggedSourceIndices.Count > 0)
                {
                    if (targetIndex > m_DraggedSourceIndices[m_DraggedSourceIndices.Count - 1])
                    {
                        targetIndex++;
                    }

                    MoveMultipleSwatches(m_DraggedSourceIndices, Mathf.Clamp(targetIndex, 0, m_BasePaletteColors.Count));
                }

                m_IsDraggingMultiple = false;
                m_DragArmed = false;
                m_DraggedSourceIndices.Clear();
            }, TrickleDown.TrickleDown);

            m_SwatchListView.selectionChanged += objects =>
            {
                UpdateRemoveButtonState();
            };
        }

        /// <summary>
        /// Walks up the visual tree from the given element to determine if it is (or is inside) a ColorField.
        /// </summary>
        private static bool IsColorFieldOrDescendant(VisualElement element)
        {
            VisualElement current = element;
            while (current != null)
            {
                if (current is ColorField) return true;
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Converts a panel-space pointer position into a swatch row index, accounting for list scroll offset.
        /// </summary>
        private int GetRowIndexAtPosition(Vector2 panelPosition)
        {
            Vector2 localPos = m_SwatchListView.WorldToLocal(panelPosition);
            ScrollView scrollView = m_SwatchListView.Q<ScrollView>();
            float scrollY = scrollView != null ? scrollView.scrollOffset.y : 0f;
            float totalY = localPos.y + scrollY;
            return Mathf.FloorToInt(totalY / 24f);
        }

        private void CreateDragProxy(Vector2 panelPos)
        {
            if (m_DragProxyElement != null) return;

            m_DragProxyElement = new VisualElement();
            m_DragProxyElement.style.position = Position.Absolute;
            m_DragProxyElement.style.flexDirection = FlexDirection.Row;
            m_DragProxyElement.style.alignItems = Align.Center;

            // Shade: Give explicit dimensions so it renders immediately without waiting for a layout pass
            m_DragProxyElement.style.minWidth = 140;
            m_DragProxyElement.style.minHeight = 26;

            m_DragProxyElement.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
            m_DragProxyElement.style.borderTopWidth = 1;
            m_DragProxyElement.style.borderBottomWidth = 1;
            m_DragProxyElement.style.borderLeftWidth = 1;
            m_DragProxyElement.style.borderRightWidth = 1;
            m_DragProxyElement.style.paddingLeft = 8;
            m_DragProxyElement.style.paddingRight = 8;
            m_DragProxyElement.style.borderTopColor = Color.gray;
            m_DragProxyElement.style.borderBottomColor = Color.gray;
            m_DragProxyElement.style.borderLeftColor = Color.gray;
            m_DragProxyElement.style.borderRightColor = Color.gray;
            m_DragProxyElement.pickingMode = PickingMode.Ignore;

            Label proxyLabel = new Label($"Moving {m_DraggedSourceIndices.Count} swatches");
            proxyLabel.style.color = Color.white;
            proxyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            proxyLabel.pickingMode = PickingMode.Ignore;
            m_DragProxyElement.Add(proxyLabel);

            m_DragProxyElement.style.left = panelPos.x + 15;
            m_DragProxyElement.style.top = panelPos.y + 15;

            rootVisualElement.Add(m_DragProxyElement);
        }

        private void RemoveDragProxy()
        {
            if (m_DragProxyElement != null)
            {
                m_DragProxyElement.RemoveFromHierarchy();
                m_DragProxyElement = null;
            }
        }

        /// <summary>
        /// Reorders multiple selected swatches to a target index position and restores selection state.
        /// </summary>
        private void MoveMultipleSwatches(List<int> sourceIndices, int targetIndex)
        {
            Undo.RecordObject(this, "Reorder Multiple Palette Swatches");

            List<Color> movedBase = new List<Color>();
            List<Color> movedNew = new List<Color>();
            List<bool> movedCustom = new List<bool>();

            sourceIndices.Sort();

            for (int i = sourceIndices.Count - 1; i >= 0; i--)
            {
                int idx = sourceIndices[i];
                movedBase.Add(m_BasePaletteColors[idx]);
                movedNew.Add(m_NewPaletteColors[idx]);
                movedCustom.Add(m_IsCustomColor[idx]);

                m_BasePaletteColors.RemoveAt(idx);
                m_NewPaletteColors.RemoveAt(idx);
                m_IsCustomColor.RemoveAt(idx);
            }

            movedBase.Reverse();
            movedNew.Reverse();
            movedCustom.Reverse();

            // Adjust target index if elements were removed from above the target insertion point
            int adjustedTarget = targetIndex;
            foreach (int idx in sourceIndices)
            {
                if (idx < targetIndex)
                {
                    adjustedTarget -= 1;
                }
            }
            adjustedTarget = Mathf.Clamp(adjustedTarget, 0, m_BasePaletteColors.Count);

            m_BasePaletteColors.InsertRange(adjustedTarget, movedBase);
            m_NewPaletteColors.InsertRange(adjustedTarget, movedNew);
            m_IsCustomColor.InsertRange(adjustedTarget, movedCustom);

            m_SwatchListView.RefreshItems();
            UpdatePreviewTexture();

            // Restore selection to the moved items at their new positions
            List<int> newSelection = new List<int>();
            for (int i = 0; i < sourceIndices.Count; i++)
            {
                newSelection.Add(adjustedTarget + i);
            }
            m_SwatchListView.SetSelection(newSelection);
        }

        /// <summary>
        /// Prompts the user to select an external 1xN palette texture asset, extracts its unique colors, 
        /// and sorts the current sprite's palette matching the imported sequence. 
        /// Adds any missing colors found in the imported palette as custom entries, 
        /// and logs an error if none of the default sprite colors match the imported palette.
        /// </summary>
        private void SortColorsUsingPalette()
        {
            string path = EditorUtility.OpenFilePanel("Select Palette Texture", "Assets", "png");
            if (string.IsNullOrEmpty(path)) return;

            // Shade: Convert absolute path to project-relative path for AssetDatabase loading
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }
            else
            {
                NekoPaletteDebug.LogError("Selected palette texture must be inside the project's Assets folder.");
                return;
            }

            Texture2D paletteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (paletteTex == null)
            {
                NekoPaletteDebug.LogError("Failed to load texture at path: " + path);
                return;
            }

            // Shade: Ensure the texture is readable
            string assetPath = AssetDatabase.GetAssetPath(paletteTex);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            bool originalReadable = true;
            if (importer != null && !importer.isReadable)
            {
                originalReadable = false;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Color[] palettePixels = paletteTex.GetPixels();

            if (!originalReadable && importer != null)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            if (palettePixels == null || palettePixels.Length == 0)
            {
                NekoPaletteDebug.LogError("The selected palette texture contains no pixel data.");
                return;
            }

            // Shade: Extract unique colors in order from the imported palette
            List<Color> importedColors = new List<Color>();
            foreach (Color c in palettePixels)
            {
                // Shade: Skip fully transparent or duplicates if it's a 1xN strip, but keep sequence order
                if (!importedColors.Contains(c))
                {
                    importedColors.Add(c);
                }
            }

            // Shade: Check if none of the default base colors are present in the imported palette
            int matchingCount = 0;
            foreach (Color baseCol in m_BasePaletteColors)
            {
                if (importedColors.Contains(baseCol))
                {
                    matchingCount++;
                }
            }

            if (matchingCount == 0)
            {
                NekoPaletteDebug.LogError("Imported palette error: None of the default sprite's colors are present in the selected palette.");
                return;
            }

            // Shade: Map current base-to-new color relationships so we don't lose user edits for existing colors
            Dictionary<Color, Color> colorMapping = new Dictionary<Color, Color>();
            for (int i = 0; i < m_BasePaletteColors.Count; i++)
            {
                if (!colorMapping.ContainsKey(m_BasePaletteColors[i]))
                {
                    colorMapping[m_BasePaletteColors[i]] = m_NewPaletteColors[i];
                }
            }

            List<Color> newBaseColors = new List<Color>();
            List<Color> newEditedColors = new List<Color>();
            List<bool> newIsCustomFlags = new List<bool>();

            // Shade: Reconstruct lists based on the imported palette's order and added positions
            foreach (Color importedCol in importedColors)
            {
                if (m_BasePaletteColors.Contains(importedCol))
                {
                    // Shade: Existing base color found in imported palette
                    int baseIndex = m_BasePaletteColors.IndexOf(importedCol);
                    newBaseColors.Add(importedCol);
                    newEditedColors.Add(m_NewPaletteColors[baseIndex]);
                    newIsCustomFlags.Add(m_IsCustomColor[baseIndex]);
                }
                else
                {
                    // Shade: Additional new color from the imported palette
                    newBaseColors.Add(importedCol); // Shade: Treat imported color as its own base reference fallback
                    newEditedColors.Add(importedCol); // Shade: Default edited color matches the imported color
                    newIsCustomFlags.Add(true);       // Shade: Marked as custom/added
                }
            }

            // Shade: Update state with Undo support
            Undo.RecordObject(this, "Sort Colors Using Palette");
            m_BasePaletteColors = newBaseColors;
            m_NewPaletteColors = newEditedColors;
            m_IsCustomColor = newIsCustomFlags;

            if (m_SwatchListView != null)
            {
                m_SwatchListView.itemsSource = m_NewPaletteColors;
                m_SwatchListView.Rebuild();
                UpdateRemoveButtonState();
            }

            UpdatePreviewTexture();
            EditorUtility.SetDirty(this);
            NekoPaletteDebug.Log("Successfully sorted and updated palette using: " + paletteTex.name);
        }

        /// <summary>
        /// Creates the visual element template for a single swatch row in the ListView.
        /// </summary>
        private VisualElement MakeSwatchRow()
        {
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;

            // Shade: Each recycled row gets one mutable index holder for its lifetime. The field
            // callbacks below are registered exactly once and always read the *current* value here,
            // so rebinding a recycled row (scrolling, RefreshItems, add/remove/move) never stacks up
            // extra callbacks pointing at stale indices - which is what was causing edits to one
            // swatch to bleed into whatever swatch a recycled row previously represented.
            SwatchRowState rowState = new SwatchRowState();
            container.userData = rowState;

            Label indexLabel = new Label();
            indexLabel.name = "index-label";
            indexLabel.style.width = 60;
            indexLabel.pickingMode = PickingMode.Ignore;
            container.Add(indexLabel);

            ColorField baseColorField = new ColorField();
            baseColorField.name = "base-color-field";
            baseColorField.style.width = 45;
            baseColorField.showEyeDropper = true;
            baseColorField.showAlpha = false;
            baseColorField.RegisterValueChangedCallback(evt =>
            {
                int index = rowState.Index;
                if (index < 0 || index >= m_BasePaletteColors.Count || !m_IsCustomColor[index])
                {
                    return;
                }

                // Shade: Two swatches can't share the same original color - it would make
                // pixel-to-swatch matching ambiguous. Reject and revert the field instead.
                if (IsColorAlreadyInBasePalette(evt.newValue, index))
                {
                    baseColorField.SetValueWithoutNotify(m_BasePaletteColors[index]);
                    NekoPaletteDebug.LogWarning($"Color {evt.newValue} is already used as another swatch's original color and can't be reused.");
                    return;
                }

                Undo.RecordObject(this, "Change Base Palette Color");
                m_BasePaletteColors[index] = evt.newValue;
                UpdatePreviewTexture();
            });
            container.Add(baseColorField);

            Label arrowLabel = new Label("->");
            arrowLabel.style.width = 20;
            arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrowLabel.pickingMode = PickingMode.Ignore;
            container.Add(arrowLabel);

            ColorField targetColorField = new ColorField();
            targetColorField.name = "target-color-field";
            targetColorField.style.flexGrow = 1;
            targetColorField.showEyeDropper = true;
            targetColorField.showAlpha = true;
            targetColorField.RegisterValueChangedCallback(evt =>
            {
                int index = rowState.Index;
                if (index < 0 || index >= m_NewPaletteColors.Count)
                {
                    return;
                }

                Undo.RecordObject(this, "Change Target Palette Color");
                m_NewPaletteColors[index] = evt.newValue;
                UpdatePreviewTexture();
            });
            container.Add(targetColorField);

            return container;
        }

        /// <summary>
        /// Binds data values to a swatch row element at the specified index.
        /// </summary>
        private void BindSwatchRow(VisualElement element, int index)
        {
            if (index < 0 || index >= m_BasePaletteColors.Count || index >= m_NewPaletteColors.Count) return;

            ((SwatchRowState)element.userData).Index = index;

            Label indexLabel = element.Q<Label>("index-label");
            indexLabel.text = $"Index {index:D2}";

            bool isCustom = m_IsCustomColor[index];

            ColorField baseColorField = element.Q<ColorField>("base-color-field");
            baseColorField.SetValueWithoutNotify(m_BasePaletteColors[index]);
            baseColorField.SetEnabled(isCustom);

            ColorField targetColorField = element.Q<ColorField>("target-color-field");
            targetColorField.SetValueWithoutNotify(m_NewPaletteColors[index]);
        }

        /// <summary>
        /// Checks whether a color is already assigned as another swatch's original/base color.
        /// </summary>
        private bool IsColorAlreadyInBasePalette(Color color, int excludingIndex)
        {
            for (int i = 0; i < m_BasePaletteColors.Count; i++)
            {
                if (i == excludingIndex) continue;
                if (m_BasePaletteColors[i] == color) return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a new custom color swatch to the active palette lists.
        /// </summary>
        private void AddColorSwatch()
        {
            Color newColor = Color.white;
            float hue = 0f;
            while (m_NewPaletteColors.Contains(newColor) && hue < 1.0f)
            {
                newColor = Color.HSVToRGB(hue, 0.8f, 0.9f);
                hue += 0.05f;
            }

            Undo.RecordObject(this, "Add Palette Color");

            m_BasePaletteColors.Add(newColor);
            m_NewPaletteColors.Add(newColor);
            m_IsCustomColor.Add(true);

            m_SwatchListView.RefreshItems();
            m_SwatchListView.SetSelection(m_NewPaletteColors.Count - 1);

            UpdatePreviewTexture();
            UpdateRemoveButtonState();
        }

        /// <summary>
        /// Custom removal callback equivalent for deleting selected custom palette colors.
        /// </summary>
        private void RemoveSelectedColorSwatch()
        {
            List<int> selectedIndices = new List<int>(m_SwatchListView.selectedIndices);
            if (selectedIndices.Count == 0) return;

            Undo.RecordObject(this, "Remove Palette Colors");

            bool removedAny = false;
            foreach (int index in selectedIndices)
            {
                if (index >= 0 && index < m_IsCustomColor.Count && m_IsCustomColor[index])
                {
                    m_BasePaletteColors.RemoveAt(index);
                    m_NewPaletteColors.RemoveAt(index);
                    m_IsCustomColor.RemoveAt(index);
                    removedAny = true;
                }
            }

            if (!removedAny) return;

            m_SwatchListView.ClearSelection();
            m_SwatchListView.RefreshItems();
            UpdatePreviewTexture();
            UpdateRemoveButtonState();
        }

        /// <summary>
        /// Evaluates selected swatch removable condition and toggles Remove (-) button state.
        /// </summary>
        private void UpdateRemoveButtonState()
        {
            // Shade: Greys out and disables the Remove (-) button if the selected color is an extracted color
            bool canRemove = false;

            foreach (int index in m_SwatchListView.selectedIndices)
            {
                if (index >= 0 && index < m_IsCustomColor.Count && m_IsCustomColor[index])
                {
                    canRemove = true;
                    break;
                }
            }

            m_RemoveSwatchButton.SetEnabled(canRemove);
        }

        /// <summary>
        /// Builds interactive sprite preview viewport element and connects scroll events.
        /// </summary>
        private void BuildSpritePreviewUI()
        {
            // Shade: Allocate a flexible square region in the GUI layout to display the preview texture
            // Shade: Expanded viewport region for easier detail inspection (v1.0.0)
            m_PreviewViewport = new VisualElement();
            m_PreviewViewport.style.height = 280;
            m_PreviewViewport.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            m_PreviewViewport.style.borderTopWidth = 1;
            m_PreviewViewport.style.borderBottomWidth = 1;
            m_PreviewViewport.style.borderLeftWidth = 1;
            m_PreviewViewport.style.borderRightWidth = 1;
            m_PreviewViewport.SetBorderColor(Color.gray);
            m_PreviewViewport.style.overflow = Overflow.Hidden;

            m_PreviewImage = new Image();
            m_PreviewImage.scaleMode = ScaleMode.StretchToFill;
            m_PreviewImage.style.position = Position.Absolute;
            m_PreviewViewport.Add(m_PreviewImage);

            // Shade: Handle mouse wheel zoom interactions relative to mouse location inside preview frame
            m_PreviewViewport.RegisterCallback<WheelEvent>(OnPreviewWheelScroll);

            // Shade: Re-centers texture dynamically whenever the window or viewport dimensions change
            m_PreviewViewport.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                UpdatePreviewLayout();
            });

            // Shade: Reset Zoom button overlay
            m_ResetZoomButton = new Button(() =>
            {
                m_ZoomScale = 1.0f;
                m_ZoomCenter = new Vector2(0.5f, 0.5f);
                UpdatePreviewLayout();
            })
            { text = "Reset Zoom" };

            m_ResetZoomButton.style.position = Position.Absolute;
            m_ResetZoomButton.style.right = 5;
            m_ResetZoomButton.style.bottom = 5;
            m_ResetZoomButton.style.display = DisplayStyle.None;
            m_PreviewViewport.Add(m_ResetZoomButton);
        }

        /// <summary>
        /// Handles scroll wheel zoom math relative to cursor coordinates over the preview element.
        /// </summary>
        /// <param name="evt">The registered UI Toolkit WheelEvent.</param>
        private void OnPreviewWheelScroll(WheelEvent evt)
        {
            if (m_PreviewTexture == null) return;

            Rect previewRect = m_PreviewViewport.contentRect;
            float zoomDelta = -evt.delta.y * 0.05f;
            float previousZoom = m_ZoomScale;
            float newZoomScale = Mathf.Clamp(m_ZoomScale + zoomDelta, 1.0f, 10.0f);

            if (!Mathf.Approximately(newZoomScale, previousZoom))
            {
                // Shade: Calculate base aspect-fitted dimensions inside the preview frame
                float texAspect = (float)m_PreviewTexture.width / m_PreviewTexture.height;
                float rectAspect = previewRect.width / previewRect.height;

                float baseWidth, baseHeight;
                if (rectAspect > texAspect)
                {
                    baseHeight = previewRect.height;
                    baseWidth = baseHeight * texAspect;
                }
                else
                {
                    baseWidth = previewRect.width;
                    baseHeight = baseWidth / texAspect;
                }

                Vector2 localMouse = evt.localMousePosition;

                float currentDrawW = baseWidth * previousZoom;
                float currentDrawH = baseHeight * previousZoom;

                float cx = previewRect.width * 0.5f;
                float cy = previewRect.height * 0.5f;

                float currentX = cx - m_ZoomCenter.x * currentDrawW;
                float currentY = cy - m_ZoomCenter.y * currentDrawH;

                float u = (localMouse.x - currentX) / currentDrawW;
                float v = (localMouse.y - currentY) / currentDrawH;

                float newDrawW = baseWidth * newZoomScale;
                float newDrawH = baseHeight * newZoomScale;

                float targetX = localMouse.x - u * newDrawW;
                float targetY = localMouse.y - v * newDrawH;

                if (newDrawW > previewRect.width)
                    targetX = Mathf.Clamp(targetX, previewRect.width - newDrawW, 0f);
                else
                    targetX = (previewRect.width - newDrawW) * 0.5f;

                if (newDrawH > previewRect.height)
                    targetY = Mathf.Clamp(targetY, previewRect.height - newDrawH, 0f);
                else
                    targetY = (previewRect.height - newDrawH) * 0.5f;

                m_ZoomScale = newZoomScale;
                m_ZoomCenter = new Vector2((cx - targetX) / newDrawW, (cy - targetY) / newDrawH);

                UpdatePreviewLayout();
            }

            evt.StopPropagation();
        }

        /// <summary>
        /// Updates coordinates, dimensions, and transform rules of preview visual elements.
        /// </summary>
        private void UpdatePreviewLayout()
        {
            if (m_PreviewTexture == null) return;

            Rect previewRect = m_PreviewViewport.contentRect;
            if (previewRect.width <= 0 || previewRect.height <= 0) return;

            // Shade: Calculate base aspect-fitted dimensions inside the preview frame
            float texAspect = (float)m_PreviewTexture.width / m_PreviewTexture.height;
            float rectAspect = previewRect.width / previewRect.height;

            float baseWidth, baseHeight;
            if (rectAspect > texAspect)
            {
                baseHeight = previewRect.height;
                baseWidth = baseHeight * texAspect;
            }
            else
            {
                baseWidth = previewRect.width;
                baseHeight = baseWidth / texAspect;
            }

            float drawWidth = baseWidth * m_ZoomScale;
            float drawHeight = baseHeight * m_ZoomScale;

            if (Mathf.Approximately(m_ZoomScale, 1.0f))
            {
                m_ZoomCenter = new Vector2(0.5f, 0.5f);
            }

            // Shade: Calculate final texture draw coordinates
            float centerX = previewRect.width * 0.5f;
            float centerY = previewRect.height * 0.5f;

            float texX = centerX - m_ZoomCenter.x * drawWidth;
            float texY = centerY - m_ZoomCenter.y * drawHeight;

            if (drawWidth > previewRect.width)
                texX = Mathf.Clamp(texX, previewRect.width - drawWidth, 0f);
            else
                texX = (previewRect.width - drawWidth) * 0.5f;

            if (drawHeight > previewRect.height)
                texY = Mathf.Clamp(texY, previewRect.height - drawHeight, 0f);
            else
                texY = (previewRect.height - drawHeight) * 0.5f;

            m_PreviewImage.style.left = texX;
            m_PreviewImage.style.top = texY;
            m_PreviewImage.style.width = drawWidth;
            m_PreviewImage.style.height = drawHeight;

            // Shade: Reset Zoom button overlay visibility
            m_ResetZoomButton.style.display = m_ZoomScale > 1.0f ? DisplayStyle.Flex : DisplayStyle.None;
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
                return valCompare != 0 ? valCompare : s1.CompareTo(s2);
            });

            // Shade: Reset internal palette lists before populating with newly extracted colors
            m_BasePaletteColors.Clear();
            m_NewPaletteColors.Clear();
            m_IsCustomColor.Clear();

            // Shade: Copy extracted colors to both base and editable palette lists
            foreach (Color32 color in uniqueColors)
            {
                m_BasePaletteColors.Add(color);
                m_NewPaletteColors.Add(color);
                m_IsCustomColor.Add(false); // Shade: Extracted colors marked as not custom
            }

            // Shade: Auto-generate a default export filename based on the source texture name
            m_PaletteName = $"{m_SourceTexture.name}_AltPalette";

            // Shade: Reset zoom transform state when populating new palette
            m_ZoomScale = 1.0f;
            m_ZoomCenter = new Vector2(0.5f, 0.5f);

            if (m_SwatchListView != null)
            {
                m_SwatchListView.headerTitle = $"Palette Swatches ({m_BasePaletteColors.Count} Colors)";
                m_SwatchListView.RefreshItems();
            }

            // Shade: Immediately generate the initial preview using the extracted colors
            UpdatePreviewTexture();
        }

        /// <summary>
        /// Resets target palette colors back to their extracted baseline colors with full Undo support.
        /// </summary>
        private void ResetPaletteToDefault()
        {
            Undo.RegisterCompleteObjectUndo(this, "Reset Palette Colors");

            m_NewPaletteColors.Clear();
            m_NewPaletteColors.AddRange(m_BasePaletteColors);

            m_SwatchListView?.RefreshItems();
            UpdatePreviewTexture();
        }

        /// <summary>
        /// Re-maps colors on the preview texture to provide live feedback during editing.
        /// </summary>
        private void UpdatePreviewTexture()
        {
            if (m_SourceTexture == null) return;

            // Shade: Ensure CPU read permissions before calling GetPixels32
            EnsureTextureIsReadable(m_SourceTexture);

            // Shade: Work in Color32 (the same representation ExtractBasePalette used) so a pixel either
            // exactly matches a base swatch color or it doesn't - no float round-trip drift, no "nearest
            // neighbor" guesswork that can pull an unrelated pixel toward the wrong swatch.
            Color32[] pixels = m_SourceTexture.GetPixels32();
            Color32[] recoloredPixels = new Color32[pixels.Length];

            Dictionary<Color32, int> baseColorIndexMap = BuildBaseColorIndexMap();

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 sourcePixel = pixels[i];

                // Shade: Preserve fully transparent pixels without attempting color matching
                if (sourcePixel.a == 0)
                {
                    recoloredPixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Shade: Only remap on an exact match to a base palette color; anything else (e.g. a
                // stray color with no matching swatch) is left untouched rather than snapped to a
                // "closest" swatch it doesn't actually belong to.
                if (baseColorIndexMap.TryGetValue(sourcePixel, out int matchIndex) && matchIndex < m_NewPaletteColors.Count)
                {
                    Color32 swapped = m_NewPaletteColors[matchIndex];

                    // Shade: Retain original pixel alpha to preserve semi-transparent edges/anti-aliasing
                    swapped.a = sourcePixel.a;
                    recoloredPixels[i] = swapped;
                }
                else
                {
                    recoloredPixels[i] = sourcePixel;
                }
            }

            // Shade: Instantiate or reallocate preview texture if dimensions don't match source texture
            if (m_PreviewTexture == null || m_PreviewTexture.width != m_SourceTexture.width || m_PreviewTexture.height != m_SourceTexture.height)
            {
                m_PreviewTexture = new Texture2D(m_SourceTexture.width, m_SourceTexture.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp // Shade: Prevents border wrapping/tiling artifacts when zoomed
                };
            }
            else
            {
                // Shade: Ensures existing preview texture instances remain clamped
                m_PreviewTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // Shade: Upload updated pixel array to GPU texture memory
            m_PreviewTexture.SetPixels32(recoloredPixels);
            m_PreviewTexture.Apply();

            if (m_PreviewImage != null)
            {
                m_PreviewImage.image = m_PreviewTexture;
                UpdatePreviewLayout();
            }
        }

        /// <summary>
        /// Builds a lookup from each base palette color to its swatch index, for exact-match pixel recoloring.
        /// If two swatches somehow share a base color, the later swatch wins the lookup slot.
        /// </summary>
        private Dictionary<Color32, int> BuildBaseColorIndexMap()
        {
            Dictionary<Color32, int> map = new Dictionary<Color32, int>(m_BasePaletteColors.Count);
            for (int i = 0; i < m_BasePaletteColors.Count; i++)
            {
                map[(Color32)m_BasePaletteColors[i]] = i;
            }

            return map;
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

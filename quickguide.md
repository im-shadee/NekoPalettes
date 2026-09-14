# NekoPalettes Quickguide
## Getting started 
> Here's a small tutorial I wrote to quickly setup your first sprite! You can follow it step-by-step if you're unsure on how to use the editor and getting your sprite ready.

### Summary
[NekoPalettes Quickguide](#nekopalettes-quickguide)
- [Getting Started](#getting-started)
  * [Summary](#summary)
  * [Dependencies](#dependencies)
  * [Bases - Importing a sprite in Unity](#bases---importing-a-sprite-in-unity)
- [Using the Palette Generator tool](#using-the-palette-generator-tool)
  * [Top Pane](#top-pane)
  * [Bottom Pane](#bottom-pane)
- [Baking your sprites](#baking-your-sprites)
- [Getting your sprite ready in the scene](#getting-your-sprite-ready-in-the-scene)
- [Closing Thoughts](#closing-thoughts)


### Dependencies
You will need to install the following package(s): 
- 2D Sprite - Unity

Click on `Window > Package Management > Package Manager`. In the 'Unity Registery' tab, search for '2D Sprite' and click on 'Install'.

If NekoPalettes throws debug errors, remove the package from Unity and install it back. It should work out of the box!


### Bases - Importing a sprite in Unity
You can follow the official [Unity Documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html) for more information on the Sprite texture type import settings.

I also wrote a guide (coming soon!) to import a sprite into Unity 6+ (and some tips and tricks to optimize asset size) if this is still unclear to you.

While the Baking Utility/Palette Editor should be able to do it for you, I wouldn't rely on it solely.

---
## Using the Palette Generator tool
Once your sprite is imported into Unity, click on it and find the 'Tools' tab on your editor's toolbar. You can find the editor under `Tools > NekoPalettes > Palette Generator`.

Here's a view of the editor:

<img width="450" height="620" alt="image" src="https://github.com/user-attachments/assets/61407e6f-67d7-45b4-ac3f-15e05856e63f" />

---
The editor should automatically setup everything if you previously clicked on a sprite. Else, you can just drag and drop your sprite asset into the 'Texture' field at the top of the editor.

<img width="637" height="117" alt="Capture d&#39;écran 2026-09-14 111234" src="https://github.com/user-attachments/assets/c02a7931-964e-4d51-bcf5-775be6c40476" />


It is split into two panes:

### Top Pane
  
The top pane is where you create the palette. By default, the list will be filled with colors *from your sprite*. 
**These colors cannot be removed since they are part of your sprite's default palette.**

You can:
- Switch the indexes of the colors by dragging them across the list. You can hold `Shift` to select multiple swatches and drag them all at once.
- Add/remove colors by pressing the +/- buttons
- Rearrange and add colors from a previously generated palette 
- Reset all of your color modifications on the current palette

### Bottom Pane
  
The bottom pane contains the live preview of your palette modifications as well as the export buttons.

The live preview allows you to see the new colors that will be applied to your sprite at runtime. You can zoom in in case your sprite sheets are too large to see changes from closer.

You can also bake your sprite once you're done (optional) and export the new palette using the 'Export palette as png' button. 
**Make sure to click the 'Reset Colors' button on the top pane to export the default color palette!**"


## Baking your sprites
This step is **required** and fundamental for the shader to work. Make sure to follow it with particular attention.

First, you'll want to click on the folder that contains all of your character sprites and click on `Tools > NekoPalettes > Bake in selected folder`. 
Then, select the base palette you exported from the [previous section](#using-the-palette-generator-tool).

Then, choose whether to delete or keep your original sprites. If you're not sure of whether your palette will work, I would advise keeping them. 
**Once you're 100% sure your palettes work, you can (and should!) delete them entirely.**

By default, generated palettes/baked sprites go into `Assets > NekoPalettes > Palettes`. Go inside this folder. You are now ready to move on to the next section.


## Getting your sprite ready in the scene
Right click on the 'Hierarchy' tab in your Unity project. Click on 'Empty GameObject' then click on your new object.

In the inspector window, click on 'Add Component' and search for 'SpriteRenderer'. 

Click on the 'Material' field, search for 'PaletteSwap' (or go into `Your Project > Packages > NekoPalettes (or com.shade.nekopalettes) > Runtime > Shaders > PaletteSwap`) and assign it. 
Use the 'PaletteSwapWithChromaKey' if you want background removal.

<img width="350" height="218" alt="Capture d&#39;écran 2026-09-14 111442" src="https://github.com/user-attachments/assets/6ceb5277-256a-4622-9ab4-001a71620d3d" />


---
Finally, click on 'Add Component' and search for the `PaletteSwap` component (and `ChromaKey` if you want background removal). Fill in the info accordingly:
1. Drag and drop the default palette into the 'Default Palette' field.
2. For any additional palette, click '+' on the additional palettes list and drag/drop each palette.
3. Select the default palette to use on scene load (0 is the default palette, indexes from 1-N for additional palettes) *Note: you can change this index at runtime by calling `SetPaletteIndex()` from the `PaletteSwap` component's API.*
4. Assign the material you assigned on the sprite renderer (either 'PaletteSwap' or 'PaletteSwapWithChromaKey'). The sprite should automatically update. If not, you can press the 'Apply Palette Changes' button.

Here's an example of the complete setup for a sprite.

<img width="354" height="613" alt="Capture d&#39;écran 2026-09-14 111539" src="https://github.com/user-attachments/assets/045d297a-8f56-4720-84e4-1542de26b0ba" />


## Closing Thoughts

Hopefully this guide answered any questions you might have had about using the NekoPalettes workflow.
Enjoy NekoPalettes!

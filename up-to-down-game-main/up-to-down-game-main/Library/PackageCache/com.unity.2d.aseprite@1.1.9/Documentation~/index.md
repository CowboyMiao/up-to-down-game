# Introduction to Aseprite Importer

The Aseprite Importer is an Asset importer that imports [Aseprite's](https://www.aseprite.org/) .ase and .aseprite files into Unity.

See [Aseprite Features](AsepriteFeatures) to read more about which features from Aseprite are supported by the importer. See [Importer Features](ImporterFeatures) to read more about which features the Aseprite Importer provides.

## Package versions

The different versions of the Aseprite Importer package are supported by the following versions of Unity respectively:

Package version  | Unity Editor version
--|--
1.2.x |  6.1
1.1.x  |  6.0, 2022.3 and 2021.3
1.0.x  |  6.0, 2022.3 and 2021.3

## Getting Started
If you are using any of the following Unity versions or newer, Aseprite Importer package comes preinstalled when creating a new 2D or 2D URP project.

- Unity 2023.2.0a19
- Unity 2023.1.0f1
- Unity 2022.3.2f1
- Unity 2021.3.28f1

You can also manually add the Aseprite Importer package by:
1. Open [Package Manager](https://docs.unity3d.com/Manual/upm-ui.html).
2. Search for **Aseprite Importer**.
3. Select **2D Aseprite Importer** and press the **install** button.
    - If you see a Burst popup saying that a new version of Burst has been installed, restart the editor.
4. You are now ready to import `.ase` and `.aseprite` files into your project.

**Note:** You need to use **Unity 2021.3.15f1** or newer.

## Modifying generated Animator Controllers

The Aseprite Importer generates an Animator Controller if the Aseprite file contains more than one frame, the [Import Mode](ImporterFeatures#general) is set to **Animated Sprite**, and the **Animation Clip** checkbox is checked in the importer. This Animator Controller is Read-Only, meaning that it cannot be changed. To modify the Animator Controller, please see [the Importer FAQ](ImporterFAQ#how-to-make-changes-to-an-animator-controller).
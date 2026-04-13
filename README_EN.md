# Prefab Annotator

[![Unity](https://img.shields.io/badge/Unity-2019.4%2B-blue)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE.md)

**[English](README_EN.md)**

An editor extension that adds annotation/description functionality to
GameObjects in Unity Prefabs.\
It helps AI better understand prefab structures, improving the accuracy
of generated business logic code.

## Features

-   Add description annotations to any GameObject in Prefab editing mode
-   Supports inheritance and overriding of annotations in nested Prefabs
-   Displays annotation icons and tooltips in the Hierarchy window
-   Supports ignoring nodes and their child nodes
-   Export Prefab structure as a tree text (copy to clipboard)

## Export Example

    main (Canvas)
    ├─ background (Image) - Description: Background image, dynamically switches based on season
    ├─ title - Description: Title root node, displayed when the event is active
    │  └─ desc (TextMeshProUGUI) - Description: Title text, format: xxx event
    ├─ content
    │  └─ Scroll View (Image, ScrollRect)
    │     ├─ Viewport (Image, Mask)
    │     │  └─ Content
    │     └─ Scrollbar Vertical (Image, Scrollbar)
    │        └─ Sliding Area
    │           └─ Handle (Image)
    └─ btnClose (Button, Image) - Description: Close button, closes the UI when clicked
       └─ desc (TextMeshProUGUI)

## Installation

### Method 1: Install via Git URL (Recommended)

1.  Open Unity Editor\
2.  Go to `Window > Package Manager`\
3.  Click the `+` button in the top-left corner\
4.  Select `Add package from git URL...`\
5.  Enter the following URL:

```{=html}
<!-- -->
```
    https://github.com/wuchunpeng777/prefab-annotator.git

6.  Click `Add`

### Method 2: Add to manifest.json manually

Open `Packages/manifest.json` and add the following under
`dependencies`:

``` json
{
  "dependencies": {
    "com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git",
    ...
  }
}
```

To specify a version, use a tag:

``` json
"com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git#v1.0.0"
```

### Method 3: Local installation

1.  Download or clone this repository\
2.  In Package Manager, select `Add package from disk...`\
3.  Choose the `package.json` file

## Requirements

-   Unity 2019.4 LTS or higher\
-   **Dependency**: Newtonsoft.Json (install manually)

> 1.  Open `Window > Package Manager`\
> 2.  Click `+` \> `Add package by name`\
> 3.  Enter: `com.unity.nuget.newtonsoft-json`

## Usage

### Enable / Disable

Menu: `Tools > Prefab Annotator > Enable/Disable`

### Add Description

1.  Double-click a Prefab to enter edit mode\
2.  Select any GameObject\
3.  Enter the description in the Inspector (bottom section)\
4.  Click Save or press `Ctrl+Enter`

### Ignore Node

Check "Ignore this node and its children" to exclude it during export.

### Export Structure

-   **In Prefab editing mode**: Select the root node and click "Export
    Prefab Description" in the Inspector\
-   Provide the exported tree structure to AI, which can analyze node
    roles via natural language descriptions and generate highly relevant
    business logic code

### Switch Tool Language

Menu: `Tools > Prefab Annotator > Language > Chinese/English`

## Data Storage

Description data is stored under `Assets/Editor/Descriptions/`, named
using the Prefab GUID:

-   Format: `{GUID}.desc.json`\
-   Moving or renaming a Prefab will not lose description data

## License

MIT License

# Prefab Annotator

[![Unity](https://img.shields.io/badge/Unity-2019.4%2B-blue)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE.md)

**[中文文档](README_CN.md)**

A Unity Editor extension for adding descriptions/annotations to GameObjects in Prefabs.

## Features

- Add description annotations to any GameObject in Prefab editing mode
- Support nested Prefab annotation inheritance and override
- Display annotation icons and tooltips in Hierarchy window
- Support ignoring nodes and their children
- Export Prefab structure as tree-formatted text (copy to clipboard)
- Chinese/English language switching

## Installation

### Method 1: Install via Git URL (Recommended)

1. Open Unity Editor
2. Open `Window > Package Manager`
3. Click the `+` button in the top-left corner
4. Select `Add package from git URL...`
5. Enter the following URL:

```
https://github.com/wuchunpeng777/prefab-annotator.git
```

6. Click `Add` button

### Method 2: Add to manifest.json

Open your project's `Packages/manifest.json` file and add to `dependencies`:

```json
{
  "dependencies": {
    "com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git",
    ...
  }
}
```

To specify a version, use a tag:

```json
"com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git#v1.0.0"
```

### Method 3: Local Installation

1. Download or clone this repository
2. In Package Manager, select `Add package from disk...`
3. Select the `package.json` file

## Requirements

- Unity 2019.4 LTS or higher
- **Dependency**: Newtonsoft.Json (manual-installed)
> 1. Open `Window > Package Manager`
> 2. Click `+` > `Add package by name`
> 3. Enter: `com.unity.nuget.newtonsoft-json`

## Usage

### Enable/Disable

Menu: `Tools > Prefab Annotator > Enable/Disable`

### Add Description

1. Double-click a Prefab to enter editing mode
2. Select any GameObject
3. Enter content in the description area at the bottom of Inspector
4. Click Save or press `Ctrl+Enter`

### Ignore Nodes

Check "Ignore this node and children" to skip the node during export.

### Export Structure

- **In Prefab editing mode**: Right-click GameObject > `Export Prefab Descriptions`
- **In Project window**: Right-click Prefab asset > `Export Prefab Descriptions`

### Switch Language

Menu: `Tools > Prefab Annotator > Language > Chinese/English`

## Data Storage

Description data is stored in `Assets/Editor/Descriptions/` directory, named by Prefab GUID:
- Format: `{GUID}.desc.json`
- Moving or renaming Prefab will not lose description data

## License

MIT License

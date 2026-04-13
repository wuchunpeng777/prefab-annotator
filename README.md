# Prefab Annotator

[![Unity](https://img.shields.io/badge/Unity-2019.4%2B-blue)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE.md)

**[English](README_EN.md)**

为 Unity Prefab 中的 GameObject 添加注释/描述功能的编辑器扩展。
可以让AI更好的理解prefab结构以提高生成业务代码的精准度。

## 功能特性

- 在 Prefab 编辑模式下为任意 GameObject 添加描述注释
- 支持嵌套 Prefab 的注释继承和覆盖
- Hierarchy 窗口显示注释图标和 Tooltip
- 支持忽略节点及其子节点
- 导出 Prefab 结构为树形文本（复制到剪贴板）

## 导出示例

~~~
main (Canvas)
├─ background (Image) - 描述：背景图，根据季节动态切换
├─ title - 描述：标题根节点，当活动开启时显示这个节点
│  └─ desc (TextMeshProUGUI) - 描述：标题文字，格式为：xxx活动
├─ content
│  └─ Scroll View (Image, ScrollRect)
│     ├─ Viewport (Image, Mask)
│     │  └─ Content
│     └─ Scrollbar Vertical (Image, Scrollbar)
│        └─ Sliding Area
│           └─ Handle (Image)
└─ btnClose (Button, Image) - 描述：关闭按钮，点击关闭界面
   └─ desc (TextMeshProUGUI)
~~~



## 安装方法

### 方法 1：通过 Git URL 安装（推荐）

1. 打开 Unity 编辑器
2. 打开 `Window > Package Manager`
3. 点击左上角 `+` 按钮
4. 选择 `Add package from git URL...`
5. 输入以下 URL：

```
https://github.com/wuchunpeng777/prefab-annotator.git
```

6. 点击 `Add` 按钮

### 方法 2：手动添加到 manifest.json

打开项目的 `Packages/manifest.json` 文件，在 `dependencies` 中添加：

```json
{
  "dependencies": {
    "com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git",
    ...
  }
}
```

如需指定版本，可以使用 tag：

```json
"com.firebox.prefab-annotator": "https://github.com/wuchunpeng777/prefab-annotator.git#v1.0.0"
```

### 方法 3：本地安装

1. 下载或克隆此仓库
2. 在 Package Manager 中选择 `Add package from disk...`
3. 选择 `package.json` 文件

## 系统要求

- Unity 2019.4 LTS 或更高版本
- **依赖**: Newtonsoft.Json（手动安装）
> 1. 打开 `Window > Package Manager`
> 2. 点击 `+` > `Add package by name`
> 3. 输入：`com.unity.nuget.newtonsoft-json`

## 使用方法

### 开启/关闭功能

菜单：`Tools > Prefab Annotator > Enable/Disable`

### 添加描述

1. 双击 Prefab 进入编辑模式
2. 选中任意 GameObject
3. 在 Inspector 底部的描述区域输入内容
4. 点击保存或按 `Ctrl+Enter`

### 忽略节点

勾选 "忽略此节点及子节点" 可以在导出时跳过该节点。

### 导出结构

- **Prefab 编辑模式中**: 选中顶部节点，点击Inspector中的导出Prefab描述
- 将导出的树形prefab结构交给AI，AI即可根据节点的自然语言描述分析节点作用，生成高度符合业务需求的代码

### 切换工具菜单语言

菜单：`Tools > Prefab Annotator > Language > Chinese/English`

## 数据存储

描述数据存储在 `Assets/Editor/Descriptions/` 目录下，以 Prefab 的 GUID 命名：
- 格式：`{GUID}.desc.json`
- 移动或重命名 Prefab 不会丢失描述数据

## 许可证

MIT License

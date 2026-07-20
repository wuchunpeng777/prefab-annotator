# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.6] - 2026-07-20

### Added

- 支持拖动调整 Inspector 中描述文本输入框的高度
- 每个 Prefab 节点独立保存输入框高度，并在切换节点或重启 Unity 后恢复

## [1.0.5] - 2026-04-15

### Added

- 支持在 Project 视图中 Ctrl+D（Mac: Cmd+D）复制 Prefab 时自动复制对应的注释文件并应用给新的prefab

## [1.0.4] - 2026-04-13

### Fixed

- 修复 Prefab 编辑模式下复制已注释节点后，新节点显示原节点注释且编辑会覆盖原节点数据的问题
- 新增 GlobalObjectId 碰撞检测，复制节点未保存时在 Inspector 显示警告并禁止编辑注释
- 新增 Hierarchy 窗口黄色警告图标，提示用户保存 Prefab 后再编辑

## [1.0.3] - 2026-04-13

### Modify

- 功能默认开启

## [1.0.0] - 2026-01-30

### Added

- 初始版本发布
- Prefab 编辑模式下的 GameObject 描述功能
- 支持嵌套 Prefab 的注释继承和覆盖
- Hierarchy 窗口图标和 Tooltip 显示
- 节点忽略功能
- 导出 Prefab 结构为树形文本
- 工具菜单中英文语言切换

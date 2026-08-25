# Errors

## [ERR-20260825-001] tuanjie-batch-project-lock

**Logged**: 2026-08-25
**Priority**: low
**Status**: resolved
**Area**: config

### Summary
尝试用团结引擎 batchmode 自动保存 ShopPanelUI 的 prefab 引用时，项目已被编辑器实例占用而启动失败。

### Error
```text
Multiple Tuanjie instances cannot open the same project.
Project: E:/LightMiniGame
```

### Context
已改为在 ShopPanelUI.OnValidate 中自动加载 CardItem、RelicItem、DeleteCard 三个预制体引用，并通过延迟 SaveAssets 在当前编辑器实例中保存。

### Suggested Fix
项目已打开时，不要另起 batchmode；把需要保存的编辑器逻辑放在当前实例的 OnValidate、菜单命令或自定义 Inspector 中。

### Metadata
- Reproducible: yes
- Related Files: Assets/Scripts/UI/ShopPanelUI.cs

---


## [ERR-20260824-001] git-path-verification

**Logged**: 2026-08-24
**Priority**: low
**Status**: pending
**Area**: config

### Summary
用于检查改动格式的 Git 命令使用了错误的工作区路径，未执行成功。

### Error
```text
fatal: cannot change to '/e/LightMiniGame': No such file or directory
```

### Context
代码修改本身已完成；后续仅需在正确的仓库根目录执行 `git diff --check`。

### Suggested Fix
使用当前项目实际仓库根目录，避免假设 `/e/LightMiniGame` 一定存在。

### Metadata
- Reproducible: unknown
- Related Files: Scripts/UI/ShopPanelUI.cs
---

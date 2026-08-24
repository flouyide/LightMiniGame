# Errors

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

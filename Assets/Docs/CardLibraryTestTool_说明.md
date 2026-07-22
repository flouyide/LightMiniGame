# CardLibraryTestTool.cs 说明文档

> 路径：`Assets/Scripts/Card/CardLibraryTestTool.cs`
> 命名空间：`LightMiniGame.Card`
> 类型：`MonoBehaviour`（运行时调试 / QA 工具）

---

## 1. 它是什么

`CardLibraryTestTool` 是一个**运行时牌库测试工具**，用于：
- 手动给某个角色的 `CharacterCardLibrary` **增 / 删卡**；
- 在 Console 打印该角色的**总牌数**与**完整 cards 列表**。

特点：
- 只用 `UnityEngine` 自带 API（GUI + `Debug.Log`），**不含 `UnityEditor` 依赖**，可安全打进正式包。
- 提供 GUI 面板、`~` 快捷键开关、`ContextMenu` 三种触发方式。

> 它操作的牌库来自 `GlobalCardLibrary`（通过 `ResolveLibrary` 取 `CharacterCardLibrary`），不直接持有数据。

---

## 2. Inspector 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `targetCharacter` | `CharacterData` | 要操作的角色；**留空则自动取 `GlobalCardLibrary` 中第一个已注册角色**。 |
| `cardPresets` | `CardData[]` | 可添加的卡模板列表，运行时用 ◀ ▶ 切换选择。 |
| `showPanel` | `bool` | 是否显示 GUI 面板（默认 `true`）。 |
| `toggleKey` | `KeyCode` | 开关面板的快捷键，默认 `KeyCode.BackQuote`（数字 1 左边的 `~` 键）。 |

---

## 3. 工作原理

```csharp
private void Start() => ResolveLibrary();           // 进入 Play 时解析目标牌库
private void Update() { if (Input.GetKeyDown(toggleKey)) showPanel = !showPanel; }
private void OnGUI()  { /* 绘制调试面板 */ }
```

`ResolveLibrary()` 的逻辑：
1. 取 `GlobalCardLibrary.Instance`，为空则 `EnsureInstance()`。
2. 若 `targetCharacter` 已指定 → 取该角色牌库；
3. 否则取 `AllLibraries[0]`（第一个已注册角色）；
4. 都没有 → `_lib = null` 并打警告（提示先注册角色）。

> 每次执行增删操作前都会重新 `ResolveLibrary()`，所以 Play 中途切换角色 / 注册新角色也能正确生效。

---

## 4. GUI 面板功能

面板位于屏幕左上（10,10 起，宽 380）。包含：

- 显示：目标角色名 + 当前总牌数。
- **◀ / ▶**：在 `cardPresets` 里切换「要添加的卡」。
- **➕ 添加选中卡** (`AddSelected`)：把当前选中的 `CardData` 加入牌库。
- 文本框 + **🗑 按ID删除** (`RemoveById`)：填 `instanceId` 精确删除。
- **删除最后一张** (`RemoveLast`)：删最后一张。
- **清空牌库** (`ClearLib`)：清空该角色全部卡。
- **🖨 打印牌库到 Console** (`PrintLibrary`)：打印总牌数 + 完整列表。
- 下方滚动区：实时预览当前牌库（序号 / 名称 / 类型 / 费用 / 短 ID）。

---

## 5. ContextMenu 方法（Inspector 右键本组件）

| 菜单项 | 方法 | 说明 |
| --- | --- | --- |
| 添加选中卡 | `AddSelected()` | 添加 `cardPresets` 中当前选中的卡。 |
| 删除最后一张 | `RemoveLast()` | 删除牌库最后一张。 |
| 清空牌库 | `ClearLib()` | 清空牌库。 |
| 打印牌库到 Console | `PrintLibrary()` | 打印总牌数 + 列表。 |

> `RemoveById(string id)` 没有 ContextMenu，仅 GUI / 代码调用。

---

## 6. 打印格式（Console）

`PrintLibrary()` 输出示例：

```
════════ 牌库内容 ════════
角色 : 战士
总牌数: 3
────────────────────────────
[0] id=a1b2c3d4 | 名=打击 | 类型=攻击 | 费=1 | 模板=Strike
[1] id=e5f6a7b8 | 名=格挡 | 类型=护甲 | 费=1 | 模板=Block
[2] id=c9d0e1f2 | 名=战吼 | 类型=增益 | 费=1 | 模板=BattleCry
═══════════════════════════
```

- 类型名通过 `CardData.GetCardTypeName(template.cardType)` 取中文（攻击/护甲/增益）。
- 模板名：`CardData.name`；若模板缺失显示 `∅`（孤儿卡）。
- ID 仅显示前 8 位（`ShortId`）。

---

## 7. 使用步骤

1. 把 `CardLibraryTestTool` 挂到场景任意 GameObject（建议挂在 `GlobalCardLibrary` 或 `GameManager` 上）。
2. 在 Inspector 指定 `targetCharacter`（留空自动取第一个角色）。
3. 在 `cardPresets` 数组里拖入若干 `CardData` 作为「可添加的卡」。
4. 进入 Play 模式：
   - 按 `~` 开关面板；
   - 或用 ◀ ▶ 选卡后点「添加」；
   - 点「打印牌库到 Console」查看总牌数与列表；
   - 或用 Inspector 右键 → ContextMenu 直接触发各操作。

---

## 8. 注意事项

- 依赖 `GlobalCardLibrary` 已存在且目标角色已注册（如通过 `BuildFromStartingLibrary` 或 `RegisterCharacter`）。否则 `_lib` 为 `null`，操作会被跳过并打印警告。
- 仅用于调试 / QA，正式发布前可按需保留（无 Editor 依赖，不影响打包）或移除组件。
- `cardPresets` 为空时「添加选中卡」会警告「未选择要添加的卡」。

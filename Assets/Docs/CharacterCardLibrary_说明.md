# CharacterCardLibrary.cs 说明文档

> 路径：`Assets/Scripts/Card/CharacterCardLibrary.cs`
> 命名空间：`LightMiniGame.Card`
> 类型：`[Serializable]` 普通类（运行时 `new`，不挂物体，也不依赖可被编辑器误改的 SO 资产）

---

## 1. 它是什么

`CharacterCardLibrary` 表示**单个角色拥有的牌库**：一个**平铺的 `CardInstance` 列表**。每个角色对应一个实例，互不影响。

- 它只管「一个角色范围内」的事：增、删、改、查、清空。
- 跨角色的管理由 `GlobalCardLibrary` 负责（见 `GlobalCardLibrary_说明.md`）。
- 设计为**可序列化普通类**（非 `MonoBehaviour` / 非 `ScriptableObject`），运行时直接 `new`，因此既能随 `GlobalCardLibrary._libraries` 一起被 Unity 序列化，又不会被当成可编辑资产误改。

---

## 2. 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `owner` | `CharacterData` | 归属角色（只读标识，作为 key）。 |
| `cards` | `List<CardInstance>` | 该角色拥有的全部卡（平铺）。 |
| `OnChanged` | `UnityEvent` | 本角色牌库**增/删/改后广播**，供未来 UI 监听刷新。 |

```csharp
public CharacterCardLibrary(CharacterData owner) { this.owner = owner; }
public int Count => cards.Count;
```

---

## 3. API 速查

| 方法 | 说明 |
| --- | --- |
| `Add(CardData template)` | 添加一张卡。**每次都新建独立 `CardInstance`**（即便模板与已有卡完全相同），拥有自己的 `instanceId` 与覆盖层。受 `owner.maxLibrarySize` 限制。 |
| `Remove(string instanceId)` | 按 `instanceId` **精确**删除一张，不影响其它同名卡 / 模板。 |
| `ApplyOverride(string instanceId, CardOverride o)` | 按 ID 写覆盖层（改该卡属性，不影响模板与其它副本）。 |
| `Get(string instanceId)` | 按 ID 取一张 `CardInstance`。 |
| `All` | 全部卡的只读列表（`IReadOnlyList<CardInstance>`）。 |
| `Clear()` | 清空全部卡（保留 `owner`）。 |

> 所有会改变内容的操作成功后都会调用 `OnChanged?.Invoke()`（删除时仅当确有元素被移除才触发）。

---

## 4. 设计要点

### 4.1 「同名多张，各自独立」
`Add` 每次 `new CardInstance(template)` 都会生成新的 `Guid` 作为 `instanceId`。因此：
- 同一模板可以存在多张，每一张都是独立实体。
- 对其中一张 `ApplyOverride` 只影响那一张，不会污染模板、也不影响其它副本。

### 4.2 牌库容量上限
`Add` 会检查 `owner.maxLibrarySize`：
- `> 0` 且 `cards.Count >= maxLibrarySize` 时，添加被忽略并打印 `Debug.LogWarning`。
- `<= 0` 表示不限制（默认 `CharacterData.maxLibrarySize = 100`）。

### 4.3 实例层 vs 模板层
- `CardInstance.template` 指向 `CardData`（模板，不变）。
- 玩家改过的字段存在 `CardInstance.overrideData`（`CardOverride`）。
- 取「真实生效值」用 `CardInstance.Effective*`（模板值优先被覆盖层接管，见 `CardInstance` 定义）。

---

## 5. 典型用法

```csharp
// 由 GlobalCardLibrary 路由调用（推荐）
GlobalCardLibrary.Instance.AddCard(warrior, strikeTemplate);
GlobalCardLibrary.Instance.RemoveCard(warrior, id);

// 或直接拿牌库操作
var lib = GlobalCardLibrary.Instance.GetLibrary(mage);
lib.Add(fireballTemplate);
int n = lib.Count;
var first = lib.Get(lib.cards[0].instanceId);
lib.Clear();
```

UI 监听示例（未来扩展）：

```csharp
var lib = GlobalCardLibrary.Instance.GetLibrary(warrior);
lib.OnChanged.AddListener(RefreshDeckUI);
```

---

## 6. 关联类型

- `GlobalCardLibrary`（全局管理，含 `RegisterCharacter` / `AddCard` 等路由方法）
- `CharacterData`（owner，`characterId` 为路由 key，`maxLibrarySize` 控制容量）
- `CardInstance`（牌库里实际存的「一张卡」：模板 + 覆盖层 + 唯一 ID）
- `CardOverride`（实例覆盖层，只存玩家手动改过的字段）

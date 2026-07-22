# GlobalCardLibrary.cs 说明文档

> 路径：`Assets/Scripts/Card/GlobalCardLibrary.cs`
> 命名空间：`LightMiniGame.Card`
> 类型：`MonoBehaviour`（带单例）

---

## 1. 它是什么

`GlobalCardLibrary` 是**全局牌库的总入口 / 单一管理者**。它持有游戏中**所有角色**的独立牌库（每个角色一个 `CharacterCardLibrary`），并保证从 API 层面做到「**按角色隔离**」——任何增删改操作都必须先指定目标角色，再由它路由到对应角色的牌库。

- 全局只有一个实例（单例），通过 `GlobalCardLibrary.Instance` 访问。
- 自身不存任何「卡牌」，只持有 `CharacterCardLibrary` 列表与索引。
- 真正存卡的是 `CharacterCardLibrary`（见 `CharacterCardLibrary_说明.md`）。

---

## 2. 生命周期与单例

| 成员 | 行为 |
| --- | --- |
| `static GlobalCardLibrary Instance` | 全局唯一实例。`Awake` 时赋值；已存在则销毁重复物体。 |
| `Awake()` | 处理单例冲突（重复物体 `Destroy`）+ `DontDestroyOnLoad`（跨场景常驻）+ 用已序列化的 `_libraries` 重建 `_index` 字典。 |
| `OnDestroy()` | 若销毁的是当前实例，将 `Instance` 置空。 |
| `static EnsureInstance()` | 若 `Instance` 为空，则**运行时动态创建**一个常驻 GameObject 并挂本组件，返回实例。在启动最早期调用一次即可。 |

> **注意 `DontDestroyOnLoad`**：默认本组件会在场景切换时保留。若不想跨场景，删掉 `Awake` 中那一行即可。

> **为什么重建 `_index`**：`Dictionary` 不被 Unity 序列化。场景重载后会丢失，而 `_libraries`（`List`）是序列化的，所以 `Awake` 里从 `_libraries` 重新建索引，避免 `GetLibrary` 失效。

---

## 3. 核心数据结构

```csharp
private readonly List<CharacterCardLibrary> _libraries;          // 全部角色牌库（可序列化）
private readonly Dictionary<string, CharacterCardLibrary> _index;// characterId -> 牌库（运行时重建）
```

- `_libraries` 序列化在 Inspector / 场景里，跨重载不丢。
- `_index` 仅是查找加速用，每次 `Awake` 从 `_libraries` 重建。

---

## 4. API 速查

### 4.1 角色牌库注册 / 注销（支持多角色同时存在）

| 方法 | 说明 |
| --- | --- |
| `RegisterCharacter(CharacterData)` | 为某角色建独立牌库；**重复注册返回已有牌库，不覆盖**。返回 `CharacterCardLibrary`。 |
| `UnregisterCharacter(CharacterData)` | 移除某角色的牌库（角色退场 / 存档卸载时）。 |
| `IsRegistered(CharacterData)` | 该角色是否已注册牌库。 |
| `GetLibrary(CharacterData)` | 取该角色的牌库；未注册返回 `null`。 |
| `AllLibraries` | 所有角色牌库的只读列表。 |

### 4.2 全局 CRUD（必须指定角色，路由到对应牌库）

| 方法 | 说明 |
| --- | --- |
| `AddCard(CharacterData, CardData)` | 给某角色加一张卡（委托 `CharacterCardLibrary.Add`）。 |
| `RemoveCard(CharacterData, string instanceId)` | 按实例 ID 删一张。 |
| `UpdateCard(CharacterData, string id, CardOverride)` | 改一张卡（写覆盖层）。 |
| `GetCardCount(CharacterData)` | 该角色牌库张数。 |
| `GetCards(CharacterData)` | 该角色所有卡（`IReadOnlyList<CardInstance>`）。 |
| `GetCard(CharacterData, string id)` | 按 ID 取一张卡。 |

### 4.3 从初始配置构建

| 方法 | 说明 |
| --- | --- |
| `BuildFromStartingLibrary(CharacterStartingLibrary start)` | 用 `CharacterStartingLibrary` 资产一次性生成起始卡组；角色须先 `RegisterCharacter`（内部会自动注册）。 |

### 4.4 持久化（可选）

- `Save(IReadOnlyDictionary<string, CardData> registry)`  
  按角色序列化到 `Application.persistentDataPath/saveFileName`（默认 `cardLibrary.json`）。模板以 `CardData.name` 作 key。
- `Load(IReadOnlyDictionary<string, CardData> registry)`  
  从存档清空并重建所有角色牌库（**会覆盖当前内存状态**）。`registry` 必须能解析模板 name。

辅助（静态）：
- `RegisterCharacterIdentity(CharacterData)`：把已知角色登记到静态 `_characterRegistry`，供 `Load` 时按 `characterId` 还原角色。
- 内部 `FindCharacterById(string)`：从 `_characterRegistry` 查角色。

---

## 5. 存档机制的限制（重要）

- 运行时 `ScriptableObject` 不暴露 GUID，所以存档里模板只记 `CardData.name`。
- `Load` 成功的前提是调用方提供 `registry`（`CardData.name -> CardData`），例如启动时 `Resources.LoadAll<CardData>()` 构建。
- 若某张卡找不到模板，重建后的 `CardInstance.template` 为 `null`，运行时表现为「孤儿卡」（仍保留 instanceId 与覆盖层）。

---

## 6. 典型用法

```csharp
// 启动早期确保实例（GameManager.Start 里常见）
GlobalCardLibrary.EnsureInstance();

// 用初始配置构建两个角色的起始卡组
library.BuildFromStartingLibrary(warriorStartLib);
library.BuildFromStartingLibrary(mageStartLib);

// 运行时增删改都要带角色
library.AddCard(warrior, strikeTemplate);
library.RemoveCard(mage, someInstanceId);
library.UpdateCard(warrior, id, myOverride);

int n = library.GetCardCount(warrior);
```

也可把 `GlobalCardLibrary` 组件直接挂在场景常驻物体上（或 `GameManager`），不用 `EnsureInstance` 也行。

---

## 7. 关联类型

- `CharacterData`（ScriptableObject，角色标识 + 牌库 key，`characterId` 为路由键）
- `CharacterCardLibrary`（单角色牌库，真正存 `CardInstance` 列表）
- `CharacterStartingLibrary`（SO，初始卡组配置）
- `CardInstance` / `CardOverride` / `CardData`（卡牌实例 / 覆盖层 / 模板）

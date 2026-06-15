---
name: magic-gardener
description: Apply the project execution rules for the Magic Gardener / 倒水小游戏 Godot project. Use when Codex works in this repository on game code, Godot scenes, assets, smoke tests, project documentation, implementation planning, verification, or any change that must follow the four authoritative project documents, module boundaries, minimal-change policy, and required validation/reporting workflow.
---

# 魔法园丁 / 倒水小游戏项目执行 Skill

## 1. Skill 目标

这是《倒水小游戏 / 魔法园丁 Magic Gardener》项目专用 AI / Codex 执行规范。

目标是让后续 AI / Codex 在本项目中执行任何修改任务时，默认先确认项目权威资料、当前实现状态和工作区已有改动，再做最精确、最小范围修改，避免跑偏、乱改、漏验证、漏记录。

本 skill 是项目级补充规范。若用户本次任务、四份权威文档和本 skill 存在冲突，必须先明确指出冲突，再按用户确认后的方向执行，并同步更新对应权威文档。

## 2. 每次任务的强制流程

每次执行任务都必须按以下顺序推进：

1. 先运行 `git status --short`，确认当前工作区状态，识别用户或其他 agent 已有未提交改动，避免覆盖。
2. 先阅读四份权威文档：
   - `docs/01_需求文档.md`
   - `docs/02_执行文档.md`
   - `docs/03_实现文档.md`
   - `docs/04_架构文档.md`
3. 再阅读与本次任务直接相关的代码、场景、资源、配置和 smoke 测试。
4. 不确定时必须明确说“不确定”，并说明缺少哪部分代码、场景、截图、报错、资源或真实窗口确认。
5. 只做最小必要修改，优先保持既有结构、命名、模块边界和已验证流程。
6. 不修改无关文件，不做无关重构，不移动或删除历史遗留文件，除非用户明确要求。
7. 修改后按任务类型运行验证。
8. 最后输出执行记录，并在需要时同步 `docs/03_实现文档.md`。

执行过程中如果发现工作区已有大量改动，必须把这些改动视为用户或其他 agent 的既有工作，不得回滚、覆盖或顺手整理。

## 3. 四份权威文档阅读规则

四份权威文档是当前执行依据，`docs/archive/` 只作为历史参考，不作为当前执行依据。

- `docs/01_需求文档.md`：判断应该做什么、验收什么。用于确认产品需求、玩法规则、阶段目标和验收口径。
- `docs/02_执行文档.md`：判断怎么执行、怎么验证。用于确认工程边界、任务类型、验证要求、素材接入规则和文档同步规则。
- `docs/03_实现文档.md`：判断当前到底实现到哪里。必须优先阅读顶部当前状态快照、第三阶段当前状态快照 / 待实现清单、跨文档追踪矩阵和最近执行记录。
- `docs/04_架构文档.md`：判断模块边界、状态归属、场景关系、数据流和禁止越界职责。

不得只根据历史执行记录或旧文档推断当前事实。历史记录只供追溯，当前状态以 `docs/03_实现文档.md` 顶部快照和最新执行记录为准。

## 4. 修改边界

后续修改必须遵守以下边界：

- View 只负责显示和上报输入。
- System 负责业务规则、流程和状态变更。
- Model 保存真实状态。
- `GameScene` 不直接写主页、仓库或存档。
- `HomeGardenView` 不直接改库存、不动态加载主页花位贴图、不动态创建主页花图节点、不动态设置主页花图 Texture / position / scale / anchors / z_index。
- `PourSystem` 不处理选花、奖励、仓库、种植、存档、UI。
- `SaveSystem` 不判断倒水规则，不操作表现层节点。
- `BottleView` 不判断倒水规则，不保存唯一真实水层状态。
- `FlowerSelectView` 不直接进入 `GameScene`，只上报目标花选择意图。
- `LevelSelectView` 不生成倒水关卡数据，不修改关卡进度。
- `WarehousePageView` 只读显示库存 snapshot，不扣减或返还库存。
- `SettingsPageView` 只显示设置 snapshot 并上报意图，不直接保存、不直接重置、不直接操作运行期状态。
- 不为了“顺手整理”做无关重构、移动文件、重命名历史债务或删除历史遗留文件。

涉及 `Bag*`、`RewardFlower*`、`PendingPlanting` 等历史命名或兼容字段时，先阅读四份权威文档确认当前语义；除非任务明确要求迁移，否则不要主动重命名或删除。

## 5. 任务类型与验证规则

按任务类型选择验证方式，并在最终回复中说明验证结果。

- 纯文档修改：只需运行 `git status --short` 和 diff 检查；不运行 `dotnet build` / Godot，并说明原因是未修改代码、场景、UI、流程或资源。
- C# 代码修改：必须运行 `dotnet build`。
- UI / 场景 / 交互 / 流程修改：必须运行 Godot headless 或真实窗口验证；真实窗口未确认时必须标 `[UNVERIFIED]`。
- 资源接入：必须复制到 `res://assets/...` 或正式项目目录，不得引用 `素材/` 或 Windows 绝对 staging 路径；需要检查场景 / 脚本 / 导入文件中的运行路径。
- 架构 / 流程 / 需求变化：必须同步更新四份权威文档中对应职责部分。产品需求更新 `01`，执行规则更新 `02`，实现状态和执行记录更新 `03`，模块边界或状态归属更新 `04`。
- 可解关卡、倒水核心、存档、种植、仓库、设置、音频等任务必须补充或更新对应 smoke 测试，并运行相应验证。

常见验证入口以 `docs/02_执行文档.md` 当前规则为准。若本 skill 与执行文档的验证要求不一致，优先按更严格、更新的项目文档执行，并记录原因。

## 6. 输出格式

每次任务最终回复必须包含以下内容：

- 修改文件
- 新增文件
- 完成内容
- 验证结果
- 未确认项
- 未修改范围
- 是否同步文档
- 如果需要继续执行，给出下一步建议

最终回复必须明确说明本次是否修改代码、场景、资源，是否运行 `dotnet build`，是否运行 Godot，以及未运行的原因。

## 7. Codex 执行任务时的固定提示模板

后续可直接复制以下模板给 Codex：

```text
请按《魔法园丁 / 倒水小游戏》项目 skill 执行本任务。

本次任务：
【在这里写具体需求】

强制要求：
1. 先运行 git status --short。
2. 先阅读 docs/01_需求文档.md、docs/02_执行文档.md、docs/03_实现文档.md、docs/04_架构文档.md。
3. 再阅读相关代码、场景、资源和 smoke 测试。
4. 只做最小必要修改。
5. 不修改无关文件，不做无关重构。
6. 按任务类型运行验证。
7. 在 docs/03_实现文档.md 记录执行结果、验证结果和未确认项。
8. 最终回复必须列出修改文件、新增文件、完成内容、验证结果、未确认项和未修改范围。
```

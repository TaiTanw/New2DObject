# New2DObject — 2D 运动与接触框架

Unity 2022.3.54f1c1 项目。自研逻辑负责动作、环境效果与规则时序；实体接触和碰撞响应交给 Unity 原生 2D 物理，每个物理步统一模拟一次。

## 从哪里开始

- **做什么、还有什么问题：** [待办安排](Docs/待办安排.md)
- **分层与实现边界：** [工程原则](Docs/工程原则.md)
- **为什么改成现在这样：** [历史回顾](Docs/历史回顾.md)
- **文档维护规则：** [文档索引](Docs/README.md)

自研解算器已退出，改为 Unity 原生单次模拟加六相位时序；同时接入水体、水下/水面游泳与环境槽接口（参数、计算点、两套槽、几何开关）。自动回归通过，手感与场景人工验收待核；多箱上坡仍在后续评估。

当前推箱抖动据用户验证，已通过**角色和箱子的 Rigidbody2D 均启用 Interpolate**暂时解决；后续运动回归以此配置为基线。该结论不代表接触算法或多箱上坡已修复。

## 代码阅读入口

| 模块 | 从这里读 |
|---|---|
| 公共调度 | [Main](Assets/Script/Framworker/Main.cs)、[MonoPublicMgr（六相位与统一模拟）](Assets/Script/Framworker/AOTSystem/Mgr/Scheduling/MonoPublicMgr.cs) |
| 输入与角色编排 | [Player](Assets/Script/Framworker/Function/Player/Player.cs)、[InputControlMgr](Assets/Script/Framworker/Function/Player/Mgr/InputControlMgr.cs) |
| 行为策略 | [PlayerStateMachine](Assets/Script/Framworker/Function/Player/FSM/PlayerStateMachine.cs)、[状态类](Assets/Script/Framworker/Function/Player/FSM/BehavioralState.cs)、[水下游泳](Assets/Script/Framworker/Function/Player/FSM/FourWayMovementState.cs)、[水面游泳](Assets/Script/Framworker/Function/Player/FSM/SurfaceSwimmingState.cs) |
| 运动执行 | [BasicEntity](Assets/Script/Framworker/Function/Physics/Component/PlayerOBJ/base/BasicEntity.cs)、[EntityEffectState](Assets/Script/Framworker/Function/Physics/Data/EntityEffectState.cs)、[CharacterPhysics](Assets/Script/Framworker/Function/Physics/Component/PlayerOBJ/CharacterPhysics.cs)、[PhysicalBox](Assets/Script/Framworker/Function/Physics/Component/SceneObjects/PhysicalBox.cs)、[物理世界访问](Assets/Script/Framworker/Function/Physics/Interface/IPhysicsWorldAccess2D.cs) |
| 环境来源与关系 | [能力接口](Assets/Script/Framworker/Function/Physics/Interface/IEnvironmentCapabilities.cs)、[EntityEnvironmentContext](Assets/Script/Framworker/Function/Physics/Base/EntityEnvironmentContext.cs)、[EnvironmentCapabilities](Assets/Script/Framworker/Function/Physics/Data/EntityEnvironmentFrame.cs)、[WaterRegion](Assets/Script/Framworker/Function/Physics/Component/SceneObjects/WaterRegion.cs) |
| 环境槽与制作工具 | [BasicPhysicalObject](Assets/Script/Framworker/Function/Physics/Component/SceneObjects/Base/BasicPhysicalObject.cs)、[EnvironmentParameter](Assets/Script/Framworker/Function/Physics/Data/EnvironmentParameter.cs)、[参数面板](Assets/Script/Framworker/Editor/Tool/EnvironmentParameterEditor.cs) |
| 数据与表现 | [Struct](Assets/Script/Framworker/Function/Physics/Data/Struct.cs)、[PresentationLayer](Assets/Script/Framworker/Function/Performance/Component/PresentationLayer.cs) |
| Edit Mode 回归 | [EnvironmentIntegrationChecks](Assets/Script/Framworker/Editor/Tool/PhyDataTest/EnvironmentIntegrationChecks.cs) |
| F8 观测与 Play Mode 自动回归 | [维护说明](Docs/待办具体事务文档集中/F_8-物理编辑器观测拆分.md)、[PhysicsEditorObservationChecks](Assets/Script/Framworker/Editor/Tool/PhyDataTest/PhysicsEditorObservationChecks.cs) |

## 当前物理帧

| 相位 | 职责 |
|---|---|
| 0 | 平台运动产生 Delta、水更新范围与水位，同步几何 |
| 1 | 读取上一步原生接触；探针、单向平台与区域查询 |
| 2 | 环境来源核对、脚下／水事实、墙能力解析 |
| 3 | 合成本步速度，按静墙、可爬坡面和头顶收一次后写入刚体 |
| 4 | 驱动者先写第一只侧面箱子的水平速度，再推进一次 Unity 物理模拟 |
| 5 | 回读实际位置与速度，供下一步和表现读取 |

`Physics2D.simulationMode` 为 Script，只在相位 4 后模拟一次；刚体实际速度是唯一跨帧运动基准。跳跃、墙滑／墙跳仍归规则时序，不由碰撞回调决定。任意箱链与多箱上坡未建模。

## 运行与验证

- 使用项目指定的 Unity 版本及 New Input System。
- 角色/箱体物理根节点按现有配置冻结旋转，使用 Dynamic 刚体；玩家刚体质量为 1，过小会推不动箱子。
- Edit Mode 菜单：`自定义工具/测试工具/验证/EPhy 验证环境接入（Edit Mode）`；批处理入口：`EnvironmentIntegrationChecks.RunForBatch`、`PhysicsEditorObservationChecks.RunForBatch`、`SlopePushSceneChecks.RunForBatch`（实际场景推箱）。
- 最近验收：编译通过，环境接入 161 项、编辑器观测与实际场景推箱回归通过；不替代 Play Mode 场景与手感验收。

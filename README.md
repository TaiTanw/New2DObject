# New2DObject — 2D 运动与接触框架

Unity 2022.3.54f1c1 项目。自研逻辑负责动作、环境效果、预测接触与请求位移；当前仍保留 Unity Dynamic 接触响应。

## 从哪里开始

- **做什么、还有什么问题：** [待办安排](Docs/待办安排.md)
- **分层与实现边界：** [工程原则](Docs/工程原则.md)
- **为什么改成现在这样：** [历史回顾](Docs/历史回顾.md)
- **文档维护规则：** [文档索引](Docs/README.md)

F5.1–F5.8 已完成：运动快照与环境入口已收敛，最终 Edit Mode 回归 118 项通过。F6 已进入统一二维运动模型与多箱上坡的首轮评估，尚未修改实现。

当前推箱抖动据用户验证，已通过**角色和箱子的 Rigidbody2D 均启用 Interpolate**暂时解决；后续运动回归以此配置为基线。该结论不代表接触算法或多箱上坡已修复。

## 代码阅读入口

| 模块 | 从这里读 |
|---|---|
| 公共调度 | [Main](Assets/Script/Framworker/Main.cs)、[物理解算器](Assets/Script/Framworker/Function/Physics/Mgr/PhysicsSolverMgr.cs) |
| 输入与角色编排 | [Player](Assets/Script/Framworker/Function/Player/Player.cs)、[InputControlMgr](Assets/Script/Framworker/Function/Player/Mgr/InputControlMgr.cs) |
| 行为策略 | [PlayerStateMachine](Assets/Script/Framworker/Function/Player/FSM/PlayerStateMachine.cs)、[状态类](Assets/Script/Framworker/Function/Player/FSM/BehavioralState.cs) |
| 运动与提交 | [BasicEntity](Assets/Script/Framworker/Function/Physics/Base/BasicEntity.cs)、[CharacterPhysics](Assets/Script/Framworker/Function/Physics/Component/CharacterPhysics.cs)、[PhysicalBox](Assets/Script/Framworker/Function/Physics/Component/SceneObjects/PhysicalBox.cs) |
| 环境来源与关系 | [能力接口](Assets/Script/Framworker/Function/Physics/Interface/IEnvironmentCapabilities.cs)、[EntityEnvironmentContext](Assets/Script/Framworker/Function/Physics/Base/EntityEnvironmentContext.cs)、[EnvironmentCapabilities](Assets/Script/Framworker/Function/Physics/Data/EntityEnvironmentFrame.cs) |
| 数据与表现 | [Struct](Assets/Script/Framworker/Function/Physics/Data/Struct.cs)、[PresentationLayer](Assets/Script/Framworker/Function/Performance/Component/PresentationLayer.cs) |
| Edit Mode 回归 | [EnvironmentIntegrationChecks](Assets/Script/Framworker/Editor/Tool/PhyDataTest/EnvironmentIntegrationChecks.cs) |
| F8 观测与 Play Mode 自动回归 | [维护说明](Docs/待办具体事务文档集中/F_8-物理编辑器观测拆分.md)、[PhysicsEditorObservationChecks](Assets/Script/Framworker/Editor/Tool/PhyDataTest/PhysicsEditorObservationChecks.cs) |

## 当前物理帧

| 相位 | 职责 |
|---|---|
| 0 | 平台自运动，产生平台 Delta |
| 1 | 实际几何接触与区域重叠查询 |
| 2 | 环境来源核对、脚下采样、角色左右墙能力解析 |
| 3 | 速度计算，构建 EntityMotionFrame，预测 AABB |
| 4 | PhysicsSolverMgr 处理实体接触；生成本帧偏置与后续动态力 |
| 5 | 基础位移 + 接触偏置，经静墙裁剪后提交一次 MovePosition |

预测和提交共用 `plannedWorldDelta`，但接触偏置、静墙裁剪与引擎修正仍可能改变最终落点。当前接触速度传递主要沿 X 轴，箱链仍是 N=1 基线；不能把 F5 完成理解为多箱解算已完成。

## 运行与验证

- 使用项目指定的 Unity 版本及 New Input System。
- 角色/箱体物理根节点按现有配置冻结旋转，自研重力与 Rigidbody2D 设置配套使用。
- Edit Mode 菜单：`自定义工具/EPhy 验证环境接入（Edit Mode）`；批处理入口：`EnvironmentIntegrationChecks.RunForBatch`。
- 最近验收：运行时、编辑器、无编辑器宏运行时编译通过，Edit Mode 118 项通过；不替代 Play Mode 场景与手感验收。

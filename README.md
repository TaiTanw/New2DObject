# New2DObject · Unity 2D 运动框架

Unity 2022.3.54f1c1。当前框架只需先理解两点：

1. **物理改走 Unity。** 实体接触、碰撞响应和最终运动结果由 Unity 2D 物理负责；旧自研接触解算器已退出。
2. **框架时序继续保留。** `MonoPublicMgr` 组织动作、环境效果和物理读写，在相位 4 回调结束后统一模拟一次，相位 5 回读实际结果。

## 这次变了什么

| 关注点 | 旧方案 | 当前方案 |
|---|---|---|
| 实体接触 | 预测 AABB、自研接触纠偏与接触力传播 | Unity 原生接触与碰撞响应；相位 4 保留侧推预处理 |
| 运动提交 | 生成请求位移，再由 `MovePosition` 提交 | 相位 3/4 写刚体速度 → 单次 `Physics2D.Simulate` → 相位 5 回读 |
| 跨帧依据 | 自研运动快照与接触累计状态 | 刚体实际速度；环境效果参数仍由框架管理 |
| 规则归属 | 框架管理动作、环境与分层调度 | 继续保留；跳跃、松跳、墙滑/墙跳和游泳仍由框架规则控制 |

![当前架构与职责变化](Docs/图/当前架构.png)

## 阅读入口

| 需要了解什么 | 入口 |
|---|---|
| 当前结构、完整固定步时序、代码入口与维护边界 | [当前框架](Docs/当前框架.md) |
| 实体基类的数据链路、字段分类与阶段语义 | [实体基类数据链路](Docs/实体基类数据链路.md) |
| 不新增功能的人工语义核对、整理及修复结论 | [整体语义调整](Docs/整体语义调整.md) |
| 问题发现、人工询问、策略确认与成果记录 | [开发日志](Docs/开发日志.md) |
| 迁移后仍有价值的待验收内容 | [待办安排](Docs/待办安排.md) |
| 原 P/V/K 待办、完成记录与旧方案退出原因 | [历史待办](Docs/历史待办.md) |
| 编辑新图 | [当前框架.drawio（架构、时序两页）](Docs/图/当前框架.drawio) |

旧专题、旧图和逐步实施记录已从当前文档中移除，完整内容可从 Git 历史查阅。维护时以现码为准，同步更新图源与预览；只为实际开工且需要独立说明的工作新增专题。

## 运行与验证

- 使用项目指定的 Unity 版本及 New Input System。常规角色/箱体沿用 Dynamic、冻结旋转配置；玩家质量沿用当前值 1。
- 角色和箱体均启用 Rigidbody2D Interpolate，作为已确认的显示基线；场景结果仍需单独验收。
- 当前人物预制体已挂接 [PlayerFourWayTesk](Assets/Script/Tesk/PlayerFourWayTesk.cs)：WASD 移动，F 或测试按钮切换穿墙四向飞行；松手悬停，障碍物内部拒绝恢复实心。水下/水面游泳按水体事实自动切换。
- 本体人物动画已替换为来源工程的切片，水下与水面动画已接入；飞行复用本体待机，不依赖精神体动画。
- Edit Mode 菜单：`自定义工具/测试工具/验证/EPhy 验证环境接入（Edit Mode）`。
- 批处理入口：`EnvironmentIntegrationChecks.RunForBatch`、`PhysicsEditorObservationChecks.RunForBatch`、`SlopePushSceneChecks.RunForBatch`。代码位置见[当前框架](Docs/当前框架.md)。
- 四向移动与人物动画验证：`BasicMovementMigrationChecks.RunForBatch`，测试代码位于 `Assets/Script/Tesk/Editor/`。
- 本次迁移已通过 Unity Edit Mode 真实模拟与资源验证：环境接入 161 项、迁移专项 73 项。人工场景与手感验收范围见[待办安排](Docs/待办安排.md)。

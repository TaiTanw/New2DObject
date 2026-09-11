# 角色动画表现层说明

## 范围

本次只实现角色的待机、地面移动、上升、下落和贴墙动画。推箱动画不在当前范围内，也不会读取尚未完成的推箱接触事实。

Animator Controller 不需要建立 Transition 或参数连线。`PresentationLayer` 通过 `Animator.Play` 直接选择已经关联好切片的 Base Layer 状态：

| 表现语义 | Controller 状态 | 切片类型 |
|---|---|---|
| 待机 | `On` | 循环 |
| 地面移动 | `move` | 循环 |
| 起跳/重新起跳 | `Jump_Up` | 触发动作，单次 |
| 持续上升 | `OnAir` | 循环 |
| 持续下落 | `OnAir_Down` | 循环 |
| 落地收势 | `Jump_Down` | 触发动作，单次 |
| 贴墙下滑 | `Wall` | 循环 |

其中 `On` 与 `OnAir` 是当前 Controller 已有的状态名，分别承载 `idle` 与 `OnAir_Up` 切片。

## 数据流

```text
PlayerStateMachine
  └─ ReadOnly_ActionData
       ├─ NowState ───────────────► 贴墙行为状态
       └─ onMove ─────────────────► 地面待机/移动、左右朝向

CharacterPhysics / BasicEntity
  ├─ ReadOnly_GeometryPhysicsData
  │    └─ isGrounded ─────────────► 地面/空中边界
  └─ ReadOnly_PlayerPhysicsData
       ├─ verticalSpeed ──────────► 主动跳跃冲量检测
       └─ verticalSpeed+phyVSpeed ► 实际上升/下落阶段

PresentationLayer
  └─ Animator.Play / SpriteRenderer.flipX
       （只消费，不回写逻辑或物理）
```

表现层在 `Player.Update` 之后执行，以读取同一渲染帧内已经更新的逻辑输出；物理数据则始终读取最近一次完成的 FixedUpdate 快照。

## 触发动作与持续动作

- 触发动作只在状态边沿开始：物理层确认一次正向主动跳跃冲量时播放 `Jump_Up`，空中/贴墙转为着地时播放 `Jump_Down`。
- 持续动作表达角色当前一直成立的状态：地面待机/移动、持续上升、持续下落和贴墙下滑。
- `Jump_Up` 是独立触发动作；完成后若仍在上升则交给 `OnAir`，开始下落或贴墙会提前中断它并立即切换持续状态。
- `Jump_Down` 只在着地后播放，完成后交给 `On`；再次离地、贴墙、跳跃或出现移动意图都会中断落地动作。
- 着地时物理层会把负的主动竖直速度重置为零；代码额外要求跳跃冲量的结果必须为正，避免把该归零误判成跳跃。

## 切换规则

1. 物理几何确认着地时，按逻辑层的水平移动意图选择 `On` 或 `move`；首个物理快照生成前以 FSM 的初始地面状态兜底。
2. FSM 确认贴墙时，播放 `Wall`。
3. 主动跳跃冲量成立时播放一次 `Jump_Up`；起跳动作结束且仍上升时进入 `OnAir`，开始下降时可提前打断。
4. 总竖直速度转为向下时直接进入持续状态 `OnAir_Down`，不再错误播放落地动作。
5. 从空中或贴墙状态进入地面时播放一次 `Jump_Down`；移动或再次跳跃可提前打断，随后进入相应持续状态。
6. 空中跳或墙跳会使主动竖直速度出现正向离散增量，据此从头重播 `Jump_Up`。
7. 顶点附近使用小速度死区并保持上一升降阶段，避免在零速附近反复切换。
8. 墙跳输入与物理结算分属 Update/FixedUpdate；离墙后会等待主动竖直速度产生下一次物理变化，再决定上升或下落，避免闪过错误动画。

## 运行时校验

初始化时会检查上述七个 Animator 状态。任一状态缺失时，组件会输出明确错误并停用自身，避免状态名变更后静默播放错误动画。

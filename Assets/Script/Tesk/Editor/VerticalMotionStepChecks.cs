using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PhyData;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>通过正式相位 3 / 5 验证候选速度与公开输出分离，并保持真实角色动作、重力与附加量的原有顺序。</summary>
public static class VerticalMotionStepChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Object> owned = new List<Object>();
    private static readonly List<string> passed = new List<string>();

    [MenuItem("自定义工具/测试工具/验证/本步竖直候选与输出边界")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请在 Edit Mode 执行。");
        passed.Clear();
        FieldInfo singleton = typeof(BaseAutoMonoMgr<MonoPublicMgr>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousManager = singleton.GetValue(null);
        SimulationMode2D previousMode = Physics2D.simulationMode;
        try
        {
            singleton.SetValue(null, NewObject("vertical candidate scheduler", Vector3.zero).AddComponent<MonoPublicMgr>());
            Physics2D.simulationMode = SimulationMode2D.Script;
            CheckOutputAndHooks();
            CheckGroundAndCeiling();
            CheckRealJumpEvents();
            CheckWallJumpTiming();
            CheckSwimmingAndSurface();
            Debug.Log($"Vertical motion step: {passed.Count} checks passed.");
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            singleton.SetValue(null, previousManager);
            Physics2D.simulationMode = previousMode;
            Directory.CreateDirectory(".utmp/VerticalMotionStep");
            File.WriteAllLines(".utmp/VerticalMotionStep/checks.txt", passed);
        }
    }

    public static void RunForBatch()
    {
        try
        {
            EnvironmentIntegrationChecks.Run();
            BasicMovementMigrationChecks.Run();
            PassiveBrakingChecks.Run();
            HorizontalDriveTargetChecks.Run();
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckOutputAndHooks()
    {
        HorizontalDriveCheckBody body = Probe(22000f);
        body.Output.verticalSpeed = -700f;
        body.GetComponent<Rigidbody2D>().velocity = Vector2.up * 3f;
        body.gravity = -10f;
        Calculate(body);
        float expected = 3f - 10f * Time.fixedDeltaTime;
        Near(body.GetPlanarVelocity().y, expected, "竖直候选从刚体实际速度出发，不读取旧公开输出");
        Near(body.OutputVerticalAtEvent, -700f, "动作事件读取公开数据时仍是上次发布值");
        Near(body.VerticalAtRule, expected, "竖直规则通过参数收到重力后的候选值");
        Near(body.OutputVerticalAtRule, -700f, "重力和竖直规则不覆盖公开输出");
        Near(body.Output.verticalSpeed, -700f, "相位 3 结束仍未发布竖直候选值");
        Complete(body);
        Near(body.Output.verticalSpeed, expected, "相位 5 沿用原有自身竖直分量输出");

        body.Output.verticalSpeed = 900f;
        body.EventVerticalSpeed = 8f;
        body.RuleVerticalSpeed = 5f;
        Calculate(body);
        Near(body.VerticalAtRule, 8f - 10f * Time.fixedDeltaTime, "动作事件改候选后再施加重力");
        Near(body.GetPlanarVelocity().y, 5f, "竖直规则通过同一候选值覆盖重力结果");
        Near(body.Output.verticalSpeed, 900f, "多个钩子修改同一候选，不借公开对象传递");
        Check(body.EventCalls == 2, "每个物理步恰好消费一次动作事件");
        Complete(body);
    }

    private static void CheckGroundAndCeiling()
    {
        HorizontalDriveCheckBody body = Probe(22200f);
        body.SetGroundNormal(Vector2.up);
        body.Output.verticalSpeed = 123f;
        body.GetComponent<Rigidbody2D>().velocity = Vector2.down * 4f;
        Calculate(body);
        Near(body.GetPlanarVelocity().y, 0f, "着地的负自身候选速度仍归零");
        Complete(body);
        body.GetComponent<Rigidbody2D>().velocity = Vector2.up * 2f;
        Calculate(body);
        Near(body.GetPlanarVelocity().y, 0f, "无上升命令时着地正 Y 残余仍丢弃");
        Complete(body);
        body.EventVerticalSpeed = 6f;
        body.gravity = -10f;
        Calculate(body);
        Near(body.GetPlanarVelocity().y, 6f, "着地后本步跳跃候选不会被重力或残余清理抹掉");
        Complete(body);
        Near(body.Output.verticalSpeed, 6f, "相位 5 保留有明确上升命令的正 Y");

        HorizontalDriveCheckBody jumping = Probe(22400f);
        jumping.Output.verticalSpeed = -99f;
        jumping.EventVerticalSpeed = 100f;
        GameObject ceiling = NewObject("candidate ceiling", jumping.transform.position + Vector3.up * 1.5f);
        ceiling.AddComponent<BoxCollider2D>().size = new Vector2(4f, 1f);
        Physics2D.SyncTransforms();
        Calculate(jumping);
        Check(jumping.GetPlanarVelocity().y < 100f, "头顶裁剪仍限制最终提交速度");
        Near(jumping.Output.verticalSpeed, -99f, "地形处理也不提前覆盖公开竖直输出");
        Near((float)Field("commandedVerticalSpeed").GetValue(jumping), jumping.GetPlanarVelocity().y,
            "地形裁剪后仍降低自身竖直命令记录");
        Complete(jumping);
        jumping.EventVerticalSpeed = null;
        float actual = jumping.GetPlanarVelocity().y;
        Calculate(jumping);
        Check(jumping.GetPlanarVelocity().y <= Mathf.Max(0f, actual) + .001f,
            "下一步不从旧输出或上一候选恢复被截掉的跳速");
        Complete(jumping);
    }

    private static void CheckRealJumpEvents()
    {
        CharacterPhysics body = Character(22600f, out var action, out var events);
        PlayerPhysicsData output = Output(body);
        output.verticalSpeed = -400f;
        events.EventTigger(PlayerStateMachine.E_playEvent.jump);
        events.EventTigger(PlayerStateMachine.E_playEvent.jumpRelease);
        Calculate(body);
        float expected = body.upSpeed * .6f - 10f * Time.fixedDeltaTime;
        Near(body.GetPlanarVelocity().y, expected, "真实角色同一步跳跃、松跳、重力顺序保持");
        Check(output.jumpSequence == 1, "跳跃事件仍只递增一次动作序号");
        Near(output.verticalSpeed, -400f, "真实角色事件不改公开竖直结果");
        Complete(body);
        Near(output.verticalSpeed, expected, "真实角色相位 5 发布实际跳速");
        output.verticalSpeed = 900f;
        Calculate(body);
        Near(body.GetPlanarVelocity().y, expected - 10f * Time.fixedDeltaTime, "下一步从实际速度续算，不重复消费跳跃或松跳");
        Check(output.jumpSequence == 1, "无新动作时序号保持");
        Complete(body);
        body.GetComponent<Rigidbody2D>().velocity = Vector2.down * 4f;
        events.EventTigger(PlayerStateMachine.E_playEvent.jumpRelease);
        Calculate(body);
        Near(body.GetPlanarVelocity().y, -4f - 10f * Time.fixedDeltaTime, "下降时松跳仍不缩放负竖直速度");
        Complete(body);
    }

    private static void CheckWallJumpTiming()
    {
        CharacterPhysics body = Character(22800f, out var action, out var events);
        action.onMove = 1f;
        events.EventTigger(PlayerStateMachine.E_playEvent.wallJump);
        Calculate(body);
        Near(body.GetPlanarVelocity().y, body.upSpeed - 10f * Time.fixedDeltaTime, "真实墙跳仍先设置自身竖直候选再处理重力");
        Near(Output(body).phyHSpeed, 0f, "墙跳限时速度注册仍晚于本步效果汇总");
        Complete(body);
        Calculate(body);
        Near(Output(body).phyHSpeed, -2f * body.speed, "墙跳限时水平命令仍于下一物理步汇总");
        float active = Mathf.Min(body.speed, 600f * Time.fixedDeltaTime);
        Near(body.GetPlanarVelocity().x, active - 2f * body.speed, "水平目标与墙跳附加量仍按原顺序合成");
        Complete(body);
    }

    private static void CheckSwimmingAndSurface()
    {
        CharacterPhysics body = Character(23000f, out var action, out _);
        action.nowState = PlayerStateMachine.E_playerState.swimming;
        action.verticalInput = 1f;
        var source = NewObject("swim state source", new Vector3(23000f, 19900f)).AddComponent<EnvironmentCheckContact>();
        body.AddSpeedStatus(source.EnvironmentRegistration, Vector2.up * 2f);
        Output(body).verticalSpeed = -900f;
        Calculate(body);
        Near(body.GetPlanarVelocity().y, 8f, "真实游泳候选覆盖重力，水流仍单独附加一次");
        Near(Output(body).verticalSpeed, -900f, "游泳规则不借公开竖直结果传递");
        Complete(body);
        Near(Output(body).verticalSpeed, 6f, "游泳回读后自身与附加竖直分量保持原语义");
        Near(Output(body).phyVSpeed, 2f, "回读保留实际接受的水流分量");

        action.nowState = PlayerStateMachine.E_playerState.surfaceSwimming;
        action.verticalInput = 0f;
        GeometryPhysicsData geometry = (GeometryPhysicsData)Field("nowGemetry").GetValue(body);
        geometry.isGrounded = true;
        geometry.groundNormal = Vector2.up;
        float center = body.BodyBounds.center.y;
        float surface = center + .02f;
        float carriedY = .005f;
        typeof(EntityEnvironmentContext).GetField("<Frame>k__BackingField", Hidden).SetValue(body.EnvironmentContext,
            new EntityEnvironmentFrame(1f, 0f, Vector2.up * carriedY));
        typeof(EntityEnvironmentContext).GetField("<WaterFrame>k__BackingField", Hidden).SetValue(body.EnvironmentContext,
            new EntityWaterFrame(false, true, true, true, surface));
        float previousOutput = Output(body).verticalSpeed;
        Calculate(body);
        float targetSpeed = (surface - center) / Time.fixedDeltaTime;
        Near(body.GetPlanarVelocity().y, targetSpeed, "水面悬浮对水流与平台的补偿仍得到原目标总速度");
        Near(Output(body).verticalSpeed, previousOutput, "水面悬浮不提前发布自身候选值");
        Complete(body);
        Near(Output(body).phyVSpeed, 2f + carriedY / Time.fixedDeltaTime, "平台与状态速度仍只并入附加命令一次");
    }

    private static HorizontalDriveCheckBody Probe(float x)
    {
        GameObject instance = NewObject("vertical candidate probe", new Vector3(x, 20000f));
        var rigidbody = instance.AddComponent<Rigidbody2D>();
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
        instance.AddComponent<BoxCollider2D>();
        var body = instance.AddComponent<HorizontalDriveCheckBody>();
        var config = ScriptableObject.CreateInstance<SO_CPhysics>();
        config.hideFlags = HideFlags.DontSave;
        config.groundLayer = config.wallLayer = 1;
        owned.Add(config);
        body.Initialize(config);
        return body;
    }

    private static CharacterPhysics Character(float x, out MovementData action,
        out LocalEventSystem<PlayerStateMachine.E_playEvent> events)
    {
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MyObjAssets/gameObject/player20.prefab"));
        owned.Add(instance);
        instance.hideFlags = HideFlags.DontSave;
        instance.transform.position = new Vector3(x, 20000f);
        var body = instance.GetComponent<CharacterPhysics>();
        typeof(BasicEntity).GetMethod("Awake", Hidden).Invoke(body, null);
        action = new MovementData { nowState = PlayerStateMachine.E_playerState.inAir };
        events = new LocalEventSystem<PlayerStateMachine.E_playEvent>();
        body.Init(new ReadOnly_ActionData(action), events);
        body.gravity = -10f;
        return body;
    }
    private static GameObject NewObject(string name, Vector3 position)
    {
        var instance = new GameObject(name) { hideFlags = HideFlags.DontSave };
        instance.transform.position = position;
        owned.Add(instance);
        return instance;
    }
    private static FieldInfo Field(string name) => typeof(BasicEntity).GetField(name, Hidden);
    private static PlayerPhysicsData Output(BasicEntity body) => (PlayerPhysicsData)Field("playerPhysicsData").GetValue(body);
    private static void Calculate(BasicEntity body) => typeof(BasicEntity).GetMethod("SpeedCalculation", Hidden).Invoke(body, null);
    private static void Complete(BasicEntity body)
    {
        Physics2D.SyncTransforms();
        Physics2D.Simulate(Time.fixedDeltaTime);
        typeof(BasicEntity).GetMethod("DisplacementCorrection", Hidden).Invoke(body, null);
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("FAILED: " + message);
        passed.Add(message);
    }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f,
        message + $" [{actual} vs {expected}]");
}

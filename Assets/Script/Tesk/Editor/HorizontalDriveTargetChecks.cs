using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PhyData;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>验证水平目标返回值、零目标与无驱动的区别，以及公开水平结果不再参与相位 3 计算。</summary>
public static class HorizontalDriveTargetChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Object> owned = new List<Object>();
    private static readonly List<string> passed = new List<string>();

    [MenuItem("自定义工具/测试工具/验证/水平驱动目标与输出边界")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请在 Edit Mode 执行。");
        passed.Clear();
        FieldInfo singleton = typeof(BaseAutoMonoMgr<MonoPublicMgr>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousManager = singleton.GetValue(null);
        SimulationMode2D previousMode = Physics2D.simulationMode;
        try
        {
            singleton.SetValue(null, NewObject("horizontal target scheduler", Vector3.zero).AddComponent<MonoPublicMgr>());
            Physics2D.simulationMode = SimulationMode2D.Script;
            CheckOutputAndEventOrder();
            CheckZeroTargetAndNoDrive();
            CheckInertiaAndReversal();
            CheckModifierAndSlope();
            CheckWallStop();
            Debug.Log($"Horizontal target: {passed.Count} checks passed.");
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            singleton.SetValue(null, previousManager);
            Physics2D.simulationMode = previousMode;
            Directory.CreateDirectory(".utmp/HorizontalDriveTarget");
            File.WriteAllLines(".utmp/HorizontalDriveTarget/checks.txt", passed);
        }
    }

    public static void RunForBatch()
    {
        try
        {
            EnvironmentIntegrationChecks.Run();
            BasicMovementMigrationChecks.Run();
            PassiveBrakingChecks.Run();
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckOutputAndEventOrder()
    {
        HorizontalDriveCheckBody body = Body(18000f);
        body.Target = 10f;
        body.Output.horizontalSpeed = 123f;
        body.EventVerticalSpeed = 8f;
        body.gravity = -10f;
        Calculate(body);
        Near(body.GetPlanarVelocity().x, Mathf.Min(10f, 300f * Time.fixedDeltaTime), "目标返回值驱动实际提交，不读取旧水平输出");
        Near(body.Output.horizontalSpeed, 123f, "相位 3 不覆盖普通运动的公开水平结果");
        Near(body.OutputAtEvent, 123f, "动作事件不再借水平输出接收目标");
        Check(body.EventCalls == 1, "目标计算位置仍恰好消费一次动作事件");
        Near(body.VerticalAtRule, 8f - 10f * Time.fixedDeltaTime, "动作事件、重力、竖直规则仍按原顺序执行");
        Complete(body);
        Near(body.Output.horizontalSpeed, body.GetPlanarVelocity().x, "相位 5 仍发布实际接受的主动水平分量");
        Near(body.Output.phyHSpeed, 0f, "纯主动移动回读后不产生被动水平余量");
    }

    private static void CheckZeroTargetAndNoDrive()
    {
        HorizontalDriveCheckBody driven = Body(18200f);
        driven.Target = 0f;
        Seed(driven, 4f, 4f);
        Calculate(driven);
        float expected = Mathf.MoveTowards(4f, 0f, 300f * Time.fixedDeltaTime);
        Near(driven.GetPlanarVelocity().x, expected, "有驱动但目标为零时仍制动主动分量");
        Complete(driven);
        Near(driven.Output.horizontalSpeed, expected, "零目标不撤销相位 5 的主动归属");

        HorizontalDriveCheckBody passive = Body(18400f);
        passive.DriveEnabled = false;
        passive.Target = 20f;
        passive.Output.horizontalSpeed = -99f;
        Seed(passive, 4f, 0f);
        Calculate(passive);
        Near(passive.GetPlanarVelocity().x, 4f, "无驱动时忽略目标，零阻力惯性保持");
        Near(passive.Output.horizontalSpeed, -99f, "无驱动的相位 3 同样不改公开水平结果");
        Complete(passive);
        Near(passive.Output.horizontalSpeed, 4f, "无驱动时保持原相位 5 自身水平输出语义");
    }

    private static void CheckInertiaAndReversal()
    {
        HorizontalDriveCheckBody body = Body(18600f);
        body.Target = -4f;
        Seed(body, 6f, 4f);
        Calculate(body);
        float active = Mathf.MoveTowards(4f, -4f, 300f * Time.fixedDeltaTime);
        Near(body.GetPlanarVelocity().x, 2f + active, "反向只控制主动分量，保留已有被动惯性");
        Complete(body);
        Near(body.Output.horizontalSpeed, active, "反向后公开结果仍是接受的主动分量");
        Near(body.Output.phyHSpeed, 2f, "反向后剩余惯性继续进入被动水平输出");
    }

    private static void CheckModifierAndSlope()
    {
        HorizontalDriveCheckBody body = Body(18800f);
        body.Target = 20f;
        body.SetGroundNormal(new Vector2(.6f, .8f));
        typeof(BasicEntity).GetField("horizontalAcceleration", Hidden).SetValue(body, 1000f);
        GameObject sourceObject = NewObject("target movement modifier", new Vector3(18800f, 17900f));
        var source = sourceObject.AddComponent<EnvironmentCheckContact>();
        body.StatePowerRegistration(source.EnvironmentRegistration, -.5f);
        Calculate(body);
        Near(body.GetPlanarVelocity().x, 8f, "移速修饰与坡面水平投影各应用一次");
        Near(body.GetPlanarVelocity().y, -6f, "下坡仍按完整本步水平运动贴合");
    }

    private static void CheckWallStop()
    {
        HorizontalDriveCheckBody body = Body(19000f);
        body.Target = 10f;
        GameObject wall = NewObject("horizontal target wall", body.transform.position + Vector3.right * 1.5f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 4f);
        Physics2D.SyncTransforms();
        for (int i = 0; i < 20; i++) { Calculate(body); Complete(body); }
        Near(body.GetPlanarVelocity().x, 0f, "静墙限制与原生碰撞仍停止水平移动");
        Near(body.Output.horizontalSpeed, 0f, "堵停后的主动水平输出同步归零");
        Check(body.BodyBounds.max.x <= wall.GetComponent<Collider2D>().bounds.min.x + .01f, "水平目标不绕过墙面约束");
        Object.DestroyImmediate(wall);
        body.Target = 0f;
        Calculate(body); Complete(body);
        Near(body.GetPlanarVelocity().x, 0f, "移开墙并松手后不恢复旧目标或输出速度");
    }

    private static HorizontalDriveCheckBody Body(float x)
    {
        GameObject instance = NewObject("horizontal target body", new Vector3(x, 18000f));
        var rigidbody = instance.AddComponent<Rigidbody2D>();
        rigidbody.gravityScale = 0f;
        rigidbody.freezeRotation = true;
        instance.AddComponent<BoxCollider2D>();
        var body = instance.AddComponent<HorizontalDriveCheckBody>();
        var config = ScriptableObject.CreateInstance<SO_CPhysics>();
        config.hideFlags = HideFlags.DontSave;
        config.groundLayer = config.wallLayer = 1;
        config.maxClimbAngle = 45f;
        owned.Add(config);
        body.Initialize(config);
        return body;
    }

    private static GameObject NewObject(string name, Vector3 position)
    {
        var instance = new GameObject(name) { hideFlags = HideFlags.DontSave };
        instance.transform.position = position;
        owned.Add(instance);
        return instance;
    }

    private static void Seed(HorizontalDriveCheckBody body, float actual, float active)
    {
        body.GetComponent<Rigidbody2D>().velocity = Vector2.right * actual;
        typeof(BasicEntity).GetField("activeHorizontalVelocity", Hidden).SetValue(body, active);
    }
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

public sealed class HorizontalDriveCheckBody : BasePhysicsEntity
{
    public bool DriveEnabled = true;
    public float Target;
    public float OutputAtEvent;
    public float VerticalAtRule;
    public float? EventVerticalSpeed;
    public int EventCalls;
    public PlayerPhysicsData Output => playerPhysicsData;
    protected override bool HasHorizontalDrive => DriveEnabled;
    protected override void Awake() { }
    protected override void OnEnable() { }
    protected override void GeometricQuery() { }
    public void Initialize(SO_CPhysics config)
    {
        BindEffectReceiver();
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        cPhysics = config;
        nowGemetry = new GeometryPhysicsData();
        playerPhysicsData = new PlayerPhysicsData();
        self_resistanceCoefficient = gravity = 0f;
        Init();
    }
    public void SetGroundNormal(Vector2 normal) { nowGemetry.isGrounded = true; nowGemetry.groundNormal = normal; }
    protected override float HorizontalSpeedCalculation() => Target;
    protected override void PhyEventUpdate()
    {
        EventCalls++;
        OutputAtEvent = playerPhysicsData.horizontalSpeed;
        if (EventVerticalSpeed.HasValue) playerPhysicsData.verticalSpeed = EventVerticalSpeed.Value;
    }
    protected override void VerticalTransmission() => VerticalAtRule = playerPhysicsData.verticalSpeed;
}

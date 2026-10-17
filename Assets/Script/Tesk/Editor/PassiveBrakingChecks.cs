using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PhyData;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>通过实体正式速度入口验证统一制动；使用原生碰撞检查惯性不会从旧计算结果恢复。</summary>
public static class PassiveBrakingChecks
{
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static readonly List<string> passed = new List<string>();
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("自定义工具/测试工具/验证/被动水平速度与统一制动")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请在 Edit Mode 执行。");
        passed.Clear();
        FieldInfo singleton = typeof(BaseAutoMonoMgr<MonoPublicMgr>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousManager = singleton.GetValue(null);
        SimulationMode2D previousMode = Physics2D.simulationMode;
        try
        {
            singleton.SetValue(null, NewObject("braking scheduler", Vector3.zero).AddComponent<MonoPublicMgr>());
            Physics2D.simulationMode = SimulationMode2D.Script;
            CheckVelocityRules();
            CheckIceExit();
            CheckCollisionFeedback();
            Debug.Log($"Passive braking: {passed.Count} checks passed.");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            singleton.SetValue(null, previousManager);
            Physics2D.simulationMode = previousMode;
            Directory.CreateDirectory(".utmp/PassiveBraking");
            File.WriteAllLines(".utmp/PassiveBraking/checks.txt", passed);
        }
    }

    public static void RunForBatch()
    {
        try
        {
            EnvironmentIntegrationChecks.Run();
            BasicMovementMigrationChecks.Run();
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckVelocityRules()
    {
        EnvironmentCheckBody body = Body(new Vector3(14000f, 14000f));
        float dt = Time.fixedDeltaTime;
        var source = new FixedSource();
        Speed(body, 6f, 20f, 1f, 6f - 20f * dt, "无来源保留原地面制动量");
        Speed(body, -6f, 20f, 1f, -6f + 20f * dt, "负向惯性对称制动");
        Speed(body, .01f, 20f, 1f, 0f, "小正速度减至零不反向");
        Speed(body, -.01f, 20f, 1f, 0f, "小负速度减至零不反向");
        Speed(body, 0f, 20f, 1f, 0f, "静止时自身阻力不产生速度");

        body.AddForce(source, Force(0f, 0f, 100f));
        Speed(body, 6f, 20f, 1f, 6f - 20f * dt, "零施力来源存在仍执行自身制动");
        body.ChangeForce(source, 10f);
        Speed(body, 0f, 5f, 1f, 5f * dt, "施加后统一制动且只执行一次");
        Speed(body, 0f, 20f, 1f, 0f, "弱施力被随后自身制动抵消");
        var secondSource = new FixedSource();
        body.AddForce(secondSource, Force(5f, 0f, 100f));
        Speed(body, 0f, 5f, 1f, 10f * dt, "多个来源处理完后自身制动仍只执行一次");
        body.RemoveForce(secondSource);
        body.AddForce(source, Force(10f, 0f, 6f));
        Speed(body, 6f, 20f, 1f, 6f - 20f * dt, "施加阶段上限之后允许速度被制动降低");
        Speed(body, 8f, 20f, 1f, 8f - 20f * dt, "已有超限惯性不会被来源上限强裁");
        body.AddForce(source, Force(-10f, 0f, 6f));
        Speed(body, -6f, 20f, 1f, -6f + 20f * dt, "负向施加上限之后同样制动");

        body.AddForce(source, Force(0f, 5f, 0f, E_PhyForceType.controlRecovery));
        Speed(body, 6f, 20f, 1f, 6f - 25f * dt, "受控复原与自身阻力叠加");
        Speed(body, -6f, 20f, 1f, -6f + 25f * dt, "负向复原和自身阻力叠加");
        Speed(body, .01f, 20f, 1f, 0f, "两次趋零计算不会越过零");
        Speed(body, 6f, 10f, .5f, 6f - 15f * .5f * dt, "已修饰自身阻力和环境影响系数各计算一次");
        Speed(body, 6f, 20f, 0f, 6f, "零环境影响时不额外制动");
        body.RemoveForce(source);
        Speed(body, 6f, -20f, 1f, 6f, "负阻力不成为反向加速");

        var data = (PlayerPhysicsData)typeof(BasicEntity).GetField("playerPhysicsData", Hidden).GetValue(body);
        data.horizontalSpeed = 1000f;
        Speed(body, 6f, 20f, 1f, 6f - 20f * dt, "被动计算从实际刚体速度开始，不读取旧物理输出");
    }

    private static void CheckIceExit()
    {
        EnvironmentCheckBody body = Body(new Vector3(14500f, 14000f));
        body.Direction = 0f;
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MyObjAssets/gameObject/ICE.prefab"));
        objects.Add(instance);
        instance.hideFlags = HideFlags.DontSave;
        instance.transform.position = new Vector3(14500f, 13990f);
        var ice = instance.GetComponent<IceGround>();
        body.RefreshSupport(instance.GetComponent<Collider2D>());
        Check(body.EnvironmentContext.DebugSourceCount == 1, "冰面仍通过原入口登记效果");
        Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
        rigidbody.velocity = Vector2.right * 6f;
        body.RunForces();
        float expected = Mathf.Max(0f, 6f - (ice.Odex + 20f * ice.SlowingEffect) * Time.fixedDeltaTime)
            + ice.StateVelocity.x;
        Near(rigidbody.velocity.x, expected, "真实冰面复原与已采样地面阻力共同生效");
        float leavingVelocity = rigidbody.velocity.x;
        body.RefreshSupport(null);
        Near(rigidbody.velocity.x, leavingVelocity, "离开冰面只撤销效果，不抹除实际惯性");
        Check(body.EnvironmentContext.DebugSourceCount == 0, "离开后没有消退来源残留");
        body.RunForces();
        Near(rigidbody.velocity.x, Mathf.MoveTowards(leavingVelocity, 0f, Time.fixedDeltaTime),
            "离开后使用空气自身阻力，不继续冰面受控复原");
    }

    private static void CheckCollisionFeedback()
    {
        EnvironmentCheckBody body = Body(new Vector3(15000f, 14000f));
        Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
        SetResistance(body, 0f);
        var wall = NewObject("native braking wall", body.transform.position + Vector3.right * 1.5f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 4f);
        Physics2D.SyncTransforms();
        rigidbody.velocity = Vector2.right * 6f;
        for (int i = 0; i < 20; i++)
        {
            body.RunForces();
            Physics2D.Simulate(Time.fixedDeltaTime);
        }
        Near(rigidbody.velocity.x, 0f, "Unity 实际碰撞消除箱体水平惯性");
        Vector2 stopped = rigidbody.position;
        wall.SetActive(false);
        Physics2D.SyncTransforms();
        for (int i = 0; i < 10; i++)
        {
            body.RunForces();
            Physics2D.Simulate(Time.fixedDeltaTime);
        }
        Near(rigidbody.velocity.x, 0f, "移开墙后不从旧速度输出恢复惯性");
        Near(Vector2.Distance(stopped, rigidbody.position), 0f, "移开墙后没有隐藏位移释放");
    }

    private static EnvironmentCheckBody Body(Vector3 position)
    {
        GameObject item = NewObject("passive velocity receiver", position);
        item.AddComponent<Rigidbody2D>().gravityScale = 0f;
        item.AddComponent<BoxCollider2D>();
        var body = item.AddComponent<EnvironmentCheckBody>();
        body.InitializeForChecks();
        body.gravity = 0f;
        body.envImpact = 1f;
        return body;
    }

    private static GameObject NewObject(string name, Vector3 position)
    {
        var item = new GameObject(name) { hideFlags = HideFlags.DontSave };
        item.transform.position = position;
        objects.Add(item);
        return item;
    }

    private static ForceData Force(float acceleration, float recovery, float limit, E_PhyForceType type = E_PhyForceType.apply)
    {
        var data = new ForceData();
        data.Init(acceleration, recovery, limit);
        data.type = type;
        return data;
    }

    private static void SetResistance(EnvironmentCheckBody body, float resistance) =>
        typeof(BasicEntity).GetField("self_resistanceCoefficient", Hidden).SetValue(body, resistance);

    private static void Speed(EnvironmentCheckBody body, float initial, float resistance, float impact, float expected, string message)
    {
        body.GetComponent<Rigidbody2D>().velocity = new Vector2(initial, 0f);
        body.envImpact = impact;
        SetResistance(body, resistance);
        body.RunForces();
        Near(body.GetPlanarVelocity().x, expected, message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        passed.Add(message);
    }

    private static void Near(float actual, float expected, string message) =>
        Check(Mathf.Abs(actual - expected) < .001f, message + $" [{actual} vs {expected}]");

    private sealed class FixedSource : IDynamicAddForce
    {
        public void ForceCalculation(IForceAction receiver) { }
    }
}

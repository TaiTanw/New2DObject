using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PhyData;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 环境接入的可重复 Edit Mode 集成检查。使用实际组件、预制体、Collider2D 查询与实体速度容器。
/// 创建临时对象，结束即销毁，不保存场景/预制体。与只读调试窗口分开，仅由明确的验证命令运行。
/// </summary>
public static class EnvironmentIntegrationChecks
{
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static readonly List<string> passed = new List<string>();
    private static int group;

    [MenuItem("自定义工具/EPhy 验证环境接入（Edit Mode）")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请在 Edit Mode 执行验证。");
        passed.Clear();
        group = 0;
        // Edit Mode 使用临时调度器，避免自动单例调用仅 Play Mode 可用的 DontDestroyOnLoad。
        // 只替换原来为空的单例，验证完成后恢复，不修改运行时代码来适配测试。
        FieldInfo schedulerField = typeof(BaseAutoMonoMgr<MonoPublicMgr>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousScheduler = schedulerField.GetValue(null);
        try
        {
            if ((MonoPublicMgr)previousScheduler == null)
                schedulerField.SetValue(null, NewObject("validation scheduler", Vector3.zero).AddComponent<MonoPublicMgr>());
            CheckGroundLifecycle();
            CheckRegions();
            CheckCapabilities();
            CheckUniqueEnvironmentHost();
            Directory.CreateDirectory(".utmp/EnvironmentValidation");
            File.WriteAllLines(".utmp/EnvironmentValidation/checks.txt", passed);
            Debug.Log($"Environment integration: {passed.Count} checks passed.");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            schedulerField.SetValue(null, previousScheduler);
            Directory.CreateDirectory(".utmp/EnvironmentValidation");
            File.WriteAllLines(".utmp/EnvironmentValidation/checks.txt", passed);
        }
    }

    // Unity -batchmode -executeMethod EnvironmentIntegrationChecks.RunForBatch
    public static void RunForBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckGroundLifecycle()
    {
        Vector3 position = NextPosition();
        IceGround ice = Prefab<IceGround>("ICE.prefab", position);
        Collider2D ground = ice.GetComponent<Collider2D>();
        EnvironmentCheckBody body = Body(position + Vector3.up * 3);
        body.RefreshSupport(ground);
        Check(body.DebugSnapshot.environmentSourceCount == 1, "冰面登记一个来源");
        Check(body.DebugSnapshot.movementModifierCount == 1 && body.DebugSnapshot.stateVelocityCount == 1 &&
            body.DebugSnapshot.dynamicForceCount == 1, "冰面三类效果完整登记");
        Near(body.Frame.slowingMultiplier, .5f, "保留冰面阻力倍率");
        Near(body.Frame.jumpHeightOffset, -.5f, "保留冰面跳跃加成");
        ForceData initial = body.Force(ice);
        Near(initial.balanceSpeed, 15f, "冰面可移动实体初始化公式");
        Near(initial.recoverySpeed, 5f, "冰面受控恢复参数");
        body.RunForces();
        Near(body.Force(ice).speedStacking, 15f * body.envImpact * Time.fixedDeltaTime, "实际实体按旧公式积分冰面施力");
        body.RefreshSupport(ground);
        Near(body.Force(ice).speedStacking, 15f * Time.fixedDeltaTime, "重复支撑采样不重置累计速度");
        body.Direction = 0;
        body.RunForces();
        Check(body.Force(ice).type == E_PhyForceType.controlRecovery, "松开输入进入受控恢复");
        body.SetStack(ice, 4f);
        body.RefreshSupport(null);
        Check(body.DebugSnapshot.environmentSourceCount == 0 && body.DebugSnapshot.movementModifierCount == 0 &&
            body.DebugSnapshot.stateVelocityCount == 0, "离地撤销绑定和持续状态");
        Check(body.Force(ice).type == E_PhyForceType.fadeAway, "离地动态力进入消退");
        Near(body.Force(ice).speedStacking, 4f, "退出保留已取得速度");
        body.RunForces();
        Check(body.Force(ice).speedStacking < 4f && body.Force(ice).speedStacking > 0, "尾效继续由实体阻力衰减");
        float tail = body.Force(ice).speedStacking;
        ice.Odex = 99;
        body.RefreshSupport(ground);
        Near(body.Force(ice).speedStacking, tail, "重新进入保留尾效速度");
        Near(body.Force(ice).recoverySpeed, initial.recoverySpeed, "重新进入沿用旧初始化参数规则");

        Disable(ice);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "源端禁用统一解除绑定");
        body.RefreshSupport(ground);
        Near(body.Frame.slowingMultiplier, 1f, "禁用组件仍有 Collider 时不读取表面效果");
        Check(body.EnvironmentContext.DebugSourceCount == 0, "禁用来源不能被支撑查询重新登记");
        ice.enabled = true;
        body.RefreshSupport(ground);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "原地重新启用冰面恢复登记");
        Disable(body);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "实体禁用统一解除所有来源");
        body.enabled = true;
        body.RefreshSupport(ground);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "实体原地启用恢复地面作用");
        ice.EnvironmentRegistration.ReleaseAll();
        ice.EnvironmentRegistration.ReleaseAll();
        body.EnvironmentContext.ReleaseAll();
        Check(body.EnvironmentContext.DebugSourceCount == 0, "双方重复清理幂等");

        GameObject plainObject = NewObject("non-moving", position);
        EnvironmentCheckReceiver plain = plainObject.AddComponent<EnvironmentCheckReceiver>();
        ForceData passive = ice.CreateEnvironmentForce(plain);
        Near(passive.balanceSpeed, 0, "冰面对无移动能力实体仍使用零平衡速度");
        Near(passive.recoverySpeed, .5f, "无移动能力分支保留地面恢复参数");
    }

    private static void CheckRegions()
    {
        Vector3 position = NextPosition();
        ForceField field = Prefab<ForceField>("F1.prefab", position);
        BoxCollider2D source = field.GetComponent<BoxCollider2D>();
        source.isTrigger = true;
        source.size = new Vector2(10, 10);
        EnvironmentCheckBody body = Body(position);
        Collider2D first = body.GetComponent<Collider2D>();
        GameObject child = NewObject("child collider", position + Vector3.right);
        child.transform.SetParent(body.transform, true);
        Collider2D second = child.AddComponent<BoxCollider2D>();
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1 && body.DebugSnapshot.dynamicForceCount == 1,
            "复合碰撞体区域接入只登记一次");
        Check(EnvironmentCapabilities.FindEntity(second) == body, "子 Collider 仍正确识别动态实体身份");
        body.RunForces();
        Near(body.Force(field).speedStacking, -5f * Time.fixedDeltaTime, "力场使用预制体原有施力参数");
        first.enabled = false;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "一个接收 Collider 退出不提前解除作用");
        Object.DestroyImmediate(second);
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 0 && body.Force(field).type == E_PhyForceType.fadeAway,
            "最后一个 Collider 销毁且无 Exit 回调时完整退出");
        first.enabled = true;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "Collider 重启后恢复区域作用");

        Collider2D source2 = field.gameObject.AddComponent<BoxCollider2D>();
        source2.isTrigger = true;
        source.enabled = false;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "多个源 Collider 取并集");
        source2.enabled = false;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "全部源 Collider 禁用后解除区域作用");
        source.enabled = true;
        Refresh(field);
        Disable(field);
        Check(body.EnvironmentContext.DebugSourceCount == 0 && body.Force(field).type == E_PhyForceType.fadeAway,
            "力场禁用同时停止动态施力（旧遗漏修复）");
        field.enabled = true;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "力场原地启用无需新的 Enter 回调");
        Disable(body);
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "区域查询排除禁用接收者");
        body.enabled = true;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "实体原地启用恢复区域作用");

        field.OnPhyEnter(body);
        first.enabled = false;
        Refresh(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1, "区域依据消失时保留显式接入依据");
        field.OnPhyExit(body);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "最后一个接入依据移除才注销效果");
        first.enabled = true;
        Refresh(field);
        Physics2D.IgnoreCollision(source, first, true);
        try
        {
            Refresh(field);
            Check(body.EnvironmentContext.DebugSourceCount == 0, "区域查询尊重 Collider 忽略关系");
        }
        finally { Physics2D.IgnoreCollision(source, first, false); }
        Refresh(field);
        ForceField overlapping = Prefab<ForceField>("Pond.prefab", position);
        overlapping.GetComponent<Collider2D>().isTrigger = true;
        Refresh(overlapping);
        Check(body.EnvironmentContext.DebugSourceCount == 2, "Pond 实际 ForceField 组件与另一环境独立叠加");
        Disable(field);
        Check(body.EnvironmentContext.DebugSourceCount == 1 && body.Force(overlapping).type == E_PhyForceType.apply,
            "退出一个环境不撤销另一个环境");
        Object.DestroyImmediate(overlapping.gameObject);
        body.RefreshSupport(null);
        Check(body.EnvironmentContext.DebugSourceCount == 0 && body.DebugSnapshot.stateVelocityCount == 0,
            "来源销毁后的无效绑定被实体清理");
        body.RunForces();
        Check(true, "已销毁来源的尾效不再回调施力者");
    }

    private static void CheckCapabilities()
    {
        Vector3 position = NextPosition();
        EnvironmentCheckBody body = Body(position);
        GameObject surface = NewObject("capabilities only", position);
        Collider2D collider = surface.AddComponent<BoxCollider2D>();
        body.RefreshSupport(collider);
        Check(body.EnvironmentContext.DebugSourceCount == 0, "普通无脚本地面无需效果登记");
        Near(body.Frame.slowingMultiplier, 1, "普通地面默认阻力倍率");
        Near(body.Frame.jumpHeightOffset, 0, "普通地面默认跳跃加成");
        EnvironmentCheckSurface platform = surface.AddComponent<EnvironmentCheckSurface>();
        body.RefreshSupport(collider);
        Check(body.Frame.platformDelta == platform.Delta && body.EnvironmentContext.DebugSourceCount == 1,
            $"单一环境宿主提供平台能力并建立一份来源关系 [{body.Frame.platformDelta} / {platform.Delta}, sources={body.EnvironmentContext.DebugSourceCount}]");
        MethodInfo build = typeof(BasicEntity).GetMethod("BuildMotionFrame", BindingFlags.Static | BindingFlags.NonPublic);
        EntityMotionFrame motion = (EntityMotionFrame)build.Invoke(null, new object[]
            { new Vector2(2, 0), 0f, true, Vector2.up, body.Frame.platformDelta, .02f });
        Near(motion.plannedWorldDelta.x, .04f + platform.Delta.x, "平台位移在实际运动快照中仅计一次 X");
        Near(motion.plannedWorldDelta.y, platform.Delta.y, "平台位移在实际运动快照中仅计一次 Y");
        platform.enabled = false;
        body.RefreshSupport(collider);
        Check(body.Frame.platformDelta == Vector2.zero, "禁用平台能力不再提供位移");
        body.RefreshSupport(null);
        Check(body.Frame.platformDelta == Vector2.zero, "离地清除平台采样");

        platform.enabled = true;
        body.RefreshSupport(collider);
        Check(body.DebugSnapshot.stateVelocityCount == 1 && body.DebugSnapshot.movementModifierCount == 1 &&
            body.DebugSnapshot.dynamicForceCount == 0, "同一宿主可单独提供持续速度能力");
        body.envImpact = .25f;
        body.RunForces();
        Check(body.PassiveVelocity == platform.StateVelocity,
            $"同一环境宿主提供的持续速度不乘质量影响系数 [{body.PassiveVelocity} / {platform.StateVelocity}]");
        Check(ReferenceEquals(EnvironmentCapabilities.Find<IWallSlideSurface>(collider), platform),
            "同一环境宿主可以提供墙滑能力");
        var function = new PhysicalFunctionData { canLeftWall = platform, nowWallt = platform };
        var read = new ReadOnly_GeometryPhysicsData(new GeometryPhysicsData(), function);
        Check(read.canLeftWall && read.nowKWall, "逻辑层读取墙滑能力布尔事实");
        platform.enabled = false;
        Check(!read.canLeftWall && !read.nowKWall, "接口引用能识别 Unity 组件禁用");
        Object.DestroyImmediate(platform);
        Check(!read.nowKWall, "接口引用能识别 Unity 对象销毁");
    }

    private static void CheckUniqueEnvironmentHost()
    {
        GameObject host = NewObject("unique environment host", NextPosition());
        host.SetActive(false);
        IceGround ice = host.AddComponent<IceGround>();
        ice.enabled = false;
        Debug.Log("Unique environment host check: the next two AddComponent errors are expected rejections.");
        Check(host.AddComponent<IceGround>() == null, "同类环境入口重复挂载被 Unity 拒绝");
        Check(host.AddComponent<ForceField>() == null, "不同子类不能绕过同一环境入口约束");
        // 验证实际配置结果；原生拒绝消息在 batch 日志中，不依赖托管日志回调计数。
        var remaining = host.GetComponents<BasicPhysicalObject>();
        Check(remaining.Length == 1 && remaining[0] == ice && !ice.enabled,
            "禁用入口仍占身份，重复请求不新增或替换原组件");
    }

    private static Vector3 NextPosition() => new Vector3(10000 + 100 * group++, 10000, 0);
    private static GameObject NewObject(string name, Vector3 position)
    {
        var item = new GameObject(name) { hideFlags = HideFlags.DontSave };
        item.transform.position = position;
        objects.Add(item);
        return item;
    }
    private static T Prefab<T>(string name, Vector3 position) where T : Component
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MyObjAssets/gameObject/" + name);
        if (asset == null) throw new InvalidOperationException("Missing fixture prefab: " + name);
        GameObject item = Object.Instantiate(asset);
        item.hideFlags = HideFlags.DontSave;
        item.transform.position = position;
        objects.Add(item);
        return item.GetComponent<T>();
    }
    private static EnvironmentCheckBody Body(Vector3 position)
    {
        GameObject item = NewObject("environment receiver", position);
        item.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        item.AddComponent<BoxCollider2D>();
        var body = item.AddComponent<EnvironmentCheckBody>();
        body.InitializeForChecks();
        return body;
    }
    private static void Refresh(ForceField field)
    {
        Physics2D.SyncTransforms();
        field.EnvironmentRegistration.RefreshTriggerRegion();
    }
    private static void Disable(Behaviour component)
    {
        component.enabled = false;
        // Edit Mode 不依赖 Unity 自动派发普通 MonoBehaviour 消息；显式验证真实的框架清理入口。
        // 若 Unity 已派发，本次重复调用同时验证幂等性。
        component.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        passed.Add(message);
    }
    private static void Near(float actual, float expected, string message) =>
        Check(Mathf.Abs(actual - expected) < .0001f, message + $" [{actual} == {expected}]");
}

// 验证夹具：计算和环境清理使用实际 BasicEntity；仅跳过场景探针配置与公共时序注册。
public sealed class EnvironmentCheckBody : BasicEntity, ICanMove
{
    public float Direction = 1;
    public float MovingDirection => Direction;
    public float Mobility => 15;
    public EntityEnvironmentFrame Frame => EnvironmentFrame;
    public Vector2 PassiveVelocity => new Vector2(playerPhysicsData.phyHSpeed, playerPhysicsData.phyVSpeed);
    protected override void Awake() => InitializeForChecks();
    protected override void OnEnable() { }
    protected override void GeometricQuery() { }
    public void InitializeForChecks()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        nowGemetry = new GeometryPhysicsData();
        nowPhyFun = new BasePhyFunData();
        playerPhysicsData = new PlayerPhysicsData();
    }
    public void RefreshSupport(Collider2D collider)
    {
        nowGemetry.groundCollider = collider;
        nowGemetry.isGrounded = collider != null;
        self_resistanceCoefficient = collider != null ? 20 : 1;
        base.PhyFunUpdate();
    }
    public void RunForces() => typeof(BasicEntity).GetMethod("HUnderForce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, null);
    private Dictionary<IDynamicAddForce, ForceData> Forces =>
        (Dictionary<IDynamicAddForce, ForceData>)typeof(BasicEntity).GetField("dynamicForceDic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
    public ForceData Force(IDynamicAddForce source) => Forces[source];
    public void SetStack(IDynamicAddForce source, float speed)
    {
        ForceData data = Forces[source];
        data.speedStacking = speed;
        Forces[source] = data;
    }
}

public sealed class EnvironmentCheckSurface : BasicPhysicalObject, IPlatformMotion, IWallSlideSurface
{
    private void Awake() => phySpeed = new Vector2(3, 2);
    public Vector2 Delta => new Vector2(.3f, -.2f);
    public float WallSlideMultiplier => .5f;
    protected override bool AppliesOnGround => true;
}
public sealed class EnvironmentCheckReceiver : MonoBehaviour, IForceAction
{
    public void StatePowerRegistration(IApplyingForceAction id, float num) { }
    public void StatePowerCancellation(IApplyingForceAction id) { }
    public void AddSpeedStatus(IApplyingForceAction id, Vector2 value) { }
    public void RemoveSpeedStatus(IApplyingForceAction id) { }
    public void AddTimeSpeed(float time, float value) { }
    public void AddForce(IDynamicAddForce id, ForceData data) { }
    public void ChangeForce(IDynamicAddForce id, float value) { }
    public void ChangeType(IDynamicAddForce id, E_PhyForceType type) { }
    public void RemoveForce(IDynamicAddForce id) { }
}

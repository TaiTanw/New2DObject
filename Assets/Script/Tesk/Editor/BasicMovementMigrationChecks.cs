using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PhyData;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>使用本工程人物预制体、原生模拟及实际切片验证基础运动迁移，不写场景或资源。</summary>
public static class BasicMovementMigrationChecks
{
    private const string PrefabPath = "Assets/MyObjAssets/gameObject/player20.prefab";
    private static readonly List<string> passed = new List<string>();
    private static readonly List<GameObject> objects = new List<GameObject>();
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("自定义工具/测试工具/验证/基础四向移动与人物动画")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请在 Edit Mode 执行。");
        passed.Clear();
        var singleton = typeof(BaseAutoMonoMgr<MonoPublicMgr>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousManager = singleton.GetValue(null);
        SimulationMode2D previousMode = Physics2D.simulationMode;
        CharacterPhysics body = null;
        try
        {
            var manager = NewObject("migration scheduler").AddComponent<MonoPublicMgr>();
            singleton.SetValue(null, manager);
            manager.Init();
            CheckAnimationAssets();

            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            objects.Add(instance);
            instance.hideFlags = HideFlags.DontSave;
            instance.transform.position = new Vector3(12000f, 12000f, 0f);
            body = instance.GetComponent<CharacterPhysics>();
            Invoke(body, "Awake");
            var player = instance.GetComponent<Player>();
            var input = new PlayerInputData();
            var action = new MovementData();
            var fsm = new PlayerStateMachine();
            fsm.InitData(body.ReadOnly_GeometryPhysicsData, input, action);
            body.Init(new ReadOnly_ActionData(action), fsm.EventSystem);
            Set(player, "playPDate", body);
            Set(player, "inputData", input);
            Set(player, "actionData", action);
            Set(player, "fsm", fsm);
            player._ActionData = new ReadOnly_ActionData(action);
            Invoke(body, "OnEnable");
            Physics2D.SyncTransforms();

            Check(player.TrySetFlying(true) && player.IsFlying && player._ActionData.isFlying,
                "公开开关同时切换执行和行为为飞行");
            Check(body.GetComponent<BoxCollider2D>().isTrigger, "飞行碰撞框为 Trigger");
            input.moveInput = input.verticalInput = 1f;
            Step(manager, player);
            Vector2 diagonal = body.GetPlanarVelocity();
            Near(diagonal.magnitude, 6f, "斜向速率不超过四向移速");
            Near(diagonal.x, diagonal.y, "斜向两轴等比例");
            Check(body.TryGetLastDisplacementObservation(out _), "相位 5 产生本步实际结果");

            input.moveInput = input.verticalInput = 0f;
            Vector2 stopped = body.GetComponent<Rigidbody2D>().position;
            Step(manager, player);
            Step(manager, player);
            Near(body.GetPlanarVelocity().magnitude, 0f, "零输入不继承飞行惯性或重力");
            Near(Vector2.Distance(stopped, body.GetComponent<Rigidbody2D>().position), 0f, "松手悬停");

            var wall = NewObject("restore blocker");
            wall.transform.position = body.BodyBounds.center;
            wall.AddComponent<BoxCollider2D>().size = Vector2.one * 4f;
            Physics2D.SyncTransforms();
            Check(!player.TrySetFlying(false) && player.IsFlying && body.GetComponent<BoxCollider2D>().isTrigger,
                "深嵌入拒绝恢复实心且保持飞行");
            Vector2 before = body.GetComponent<Rigidbody2D>().position;
            input.moveInput = 1f;
            Step(manager, player);
            Check(body.GetComponent<Rigidbody2D>().position.x > before.x, "飞行仍可在实心障碍内部移动");
            wall.SetActive(false);
            Physics2D.SyncTransforms();
            input.moveInput = 0f;
            Check(player.TrySetFlying(false) && !player.IsFlying && !body.GetComponent<BoxCollider2D>().isTrigger,
                "空位恢复普通行走与实心碰撞");
            Step(manager, player);
            Check(body.GetPlanarVelocity().y < 0f && !player._ActionData.isFlying, "退出飞行恢复重力，不补旧跳跃");

            GameObject waterObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MyObjAssets/gameObject/WaterPool.prefab"));
            objects.Add(waterObject);
            waterObject.hideFlags = HideFlags.DontSave;
            waterObject.transform.position = body.BodyBounds.center;
            waterObject.transform.localScale = Vector3.one;
            var water = waterObject.GetComponent<WaterRegion>();
            var waterBox = waterObject.GetComponent<BoxCollider2D>();
            waterBox.offset = Vector2.zero;
            waterBox.size = Vector2.one * 20f;
            Physics2D.SyncTransforms();
            body.ResampleEnvironment();
            Invoke(player, "Update");
            Check(player._ActionData.FourWay == FourWayMode.Swimming, "实心人物入水自动进入游泳");
            input.moveInput = input.verticalInput = 1f;
            Step(manager, player);
            Near(new Vector2(action.onMove, action.verticalInput).magnitude, 1f, "原游泳斜向输入保持归一");
            Check(player.TrySetFlying(true) && !body.EnvironmentContext.WaterFrame.submerged,
                "飞行优先于水，清除水事实");
            Check(player.TrySetFlying(false) && player._ActionData.isSwimming, "水中结束飞行重新采样并恢复游泳");

            CheckPresentation(instance, body, action);
            Debug.Log($"Basic movement migration: {passed.Count} checks passed.");
        }
        finally
        {
            if (body != null) Invoke(body, "OnDisable");
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            singleton.SetValue(null, previousManager);
            Physics2D.simulationMode = previousMode;
            Directory.CreateDirectory(".utmp/BasicMovementMigration");
            File.WriteAllLines(".utmp/BasicMovementMigration/checks.txt", passed);
        }
    }

    public static void RunForBatch()
    {
        try
        {
            EnvironmentIntegrationChecks.Run();
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void CheckAnimationAssets()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null && prefab.GetComponent<PlayerFourWayTesk>() != null, "人物预制体已关联 Tesk 测试入口");
        var controller = prefab.GetComponentInChildren<Animator>().runtimeAnimatorController as AnimatorController;
        string[] names = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
        foreach (string state in new[] { "On", "move", "move_loop", "Jump_Up", "OnAir", "Jump_Down", "OnAir_Down", "Wall",
            "Swim", "SwimIdle", "SurfaceSwim", "SurfaceSwimIdle" })
            Check(names.Contains(state), "本体动画状态存在：" + state);
        Check(!names.Contains("Flight"), "本体控制器不引用精神体飞行动画");
        AnimationClip[] clips = controller.animationClips.Distinct().ToArray();
        Check(clips.Length == 18, "本体控制器的 18 个动画片段已迁移");
        foreach (AnimationClip clip in clips)
        {
            Check(!AssetDatabase.GetAssetPath(clip).Contains("/Animation2/"), "动画使用本体资源：" + clip.name);
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var keys = bindings.SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding)).ToArray();
            Check(keys.Length > 0 && keys.All(key => key.value is Sprite), "切片引用完整：" + clip.name);
        }
        Check(prefab.GetComponentInChildren<SpriteRenderer>().sprite != null, "预制体默认人物切片有效");
    }

    private static void CheckPresentation(GameObject instance, CharacterPhysics body, MovementData action)
    {
        var presentation = instance.GetComponentInChildren<PresentationLayer>();
        var animator = presentation.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        Invoke(presentation, "Awake");
        presentation.Init(body.ReadOnly_PlayersPhysicsData, body.ReadOnly_GeometryPhysicsData, new ReadOnly_ActionData(action));
        foreach (var sample in new[] {
            (PlayerStateMachine.E_playerState.swimming, 0f, "SwimIdle"),
            (PlayerStateMachine.E_playerState.swimming, 1f, "Swim"),
            (PlayerStateMachine.E_playerState.surfaceSwimming, 0f, "SurfaceSwimIdle"),
            (PlayerStateMachine.E_playerState.surfaceSwimming, 1f, "SurfaceSwim"),
            (PlayerStateMachine.E_playerState.flying, 1f, "On") })
        {
            action.nowState = sample.Item1;
            action.onMove = sample.Item2;
            action.verticalInput = 0f;
            Invoke(presentation, "Update");
            animator.Update(0f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + sample.Item3),
                "行为选择本体动画：" + sample.Item3);
        }
        // 新素材把行走分为起手与循环，持续输入须能进入循环而非停在起手末帧。
        action.nowState = PlayerStateMachine.E_playerState.isOnGround;
        action.onMove = 0f;
        Invoke(presentation, "Update");
        animator.Update(0f);
        action.onMove = 1f;
        for (int i = 0; i < 30; i++)
        {
            Invoke(presentation, "Update");
            animator.Update(1f / 30f);
        }
        AnimatorStateInfo walking = animator.GetCurrentAnimatorStateInfo(0);
        Check(walking.IsName("Base Layer.move_loop"),
            "行走起手后进入 move_loop" + $" [state={walking.fullPathHash}, t={walking.normalizedTime}]");
    }

    private static void Step(MonoPublicMgr manager, Player player)
    {
        Invoke(player, "Update");
        Invoke(manager, "FixedUpdate");
    }

    private static GameObject NewObject(string name)
    {
        var item = new GameObject(name) { hideFlags = HideFlags.DontSave };
        objects.Add(item);
        return item;
    }

    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, PrivateInstance).SetValue(owner, value);

    private static void Invoke(object owner, string method) =>
        owner.GetType().GetMethod(method, PrivateInstance).Invoke(owner, null);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        passed.Add(message);
    }

    private static void Near(float actual, float expected, string message) =>
        Check(Mathf.Abs(actual - expected) < .005f, message + $" [{actual} vs {expected}]");
}

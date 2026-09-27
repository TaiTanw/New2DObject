// PlayerDataWindow.cs
using PhyData;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EPhy 只读调试窗口。
/// 本工具只读取运行时快照，不注册物理回调、不改写实体状态、不参与解算。
/// </summary>
public class PlayerDataWindow : EditorWindow
{
    BasicEntity targetEntity;
    Vector2 scrollPosition;

    [MenuItem("自定义工具/EPhy 实时数据显示")]
    public static void ShowWindow()
    {
        GetWindow<PlayerDataWindow>("EPhy Debug");
    }

    private void OnEnable()
    {
        EditorApplication.update += Repaint;
        PhysicsEditorEntityObservation.StartObserving();
        PhysicsEditorContactObservation.StartObserving();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
        PhysicsEditorEntityObservation.StopObserving();
        PhysicsEditorContactObservation.StopObserving();
    }

    private void OnGUI()
    {
        GUILayout.Label("EPhy 只读物理快照", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("窗口只读取调试快照；所有数值均不可回写到物理解算。", MessageType.Info);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("请先进入 Play 模式。", MessageType.Warning);
            return;
        }

        targetEntity = (BasicEntity)EditorGUILayout.ObjectField(
            "目标实体", targetEntity, typeof(BasicEntity), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("关联当前玩家"))
        {
            Player player = InputControlMgr.Instance.Player;
            targetEntity = player != null
                ? player.GetComponentInChildren<BasicEntity>()
                : null;
        }

        if (GUILayout.Button("关联当前选择"))
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                targetEntity = selected.GetComponentInParent<BasicEntity>();
                if (targetEntity == null)
                    targetEntity = selected.GetComponentInChildren<BasicEntity>();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (targetEntity == null)
        {
            EditorGUILayout.HelpBox("请选择玩家或任意 PhysicalBox。", MessageType.Warning);
            return;
        }

        EntityPhysicsDebugSnapshot snapshot = PhysicsEditorEntityObservation.GetSnapshot(targetEntity);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawIdentity(snapshot);
            DrawMotion(snapshot);
            DrawGeometry(snapshot);
            DrawEnvironment(snapshot);
        }
        DrawContactPairs(targetEntity);

        EditorGUILayout.EndScrollView();
    }

    static void DrawIdentity(EntityPhysicsDebugSnapshot snapshot)
    {
        EditorGUILayout.Space();
        GUILayout.Label("实体", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("名称", snapshot.entityName);
        EditorGUILayout.LabelField("Fixed Tick", snapshot.fixedTick.ToString());
        string sampledPhase = snapshot.phase == -2
            ? "无样本"
            : snapshot.phase == -1 ? "物理循环开始" : snapshot.phase.ToString();
        EditorGUILayout.LabelField("采样相位", sampledPhase);
        EditorGUILayout.LabelField("Body Type", snapshot.bodyType.ToString());
        DrawVector("实际位置（物理帧开始采样）", snapshot.actualPosition);
        EditorGUILayout.LabelField("旋转", snapshot.rotation.ToString("F3"));
        EditorGUILayout.LabelField("角速度", snapshot.angularVelocity.ToString("F3"));
    }

    static void DrawMotion(EntityPhysicsDebugSnapshot snapshot)
    {
        EditorGUILayout.Space();
        GUILayout.Label("运动与解算", EditorStyles.boldLabel);
        DrawVector("未约束总速度（相位 3）", snapshot.planarVelocity);
        DrawVector("速度积分位移", snapshot.integratedVelocityDelta);
        DrawVector("斜坡修正后位移", snapshot.motionDelta);
        DrawVector("平台位移", snapshot.platformDelta);
        // 来自实体相位 3 的运动快照；窗口只展示，不通过各分量另算一份运行时输入。
        DrawVector("预测/提交共用基础位移", snapshot.plannedWorldDelta);
        DrawVector("接触对偏置", snapshot.solverOffset);
        DrawVector("环境约束前位移", snapshot.unconstrainedDelta);
        DrawVector("最终请求位移", snapshot.requestedDelta);
        DrawVector("MovePosition 目标", snapshot.requestedTarget);
        DrawVector("引擎修正量", snapshot.engineCorrection);
        EditorGUILayout.Toggle("环境阻挡 X", snapshot.horizontalEnvironmentBlocked);

        EditorGUILayout.Space();
        GUILayout.Label("预测 AABB", EditorStyles.boldLabel);
        DrawVector("预测中心", snapshot.predictedCenter);
        DrawVector("预测半长宽", snapshot.predictedExtents);
    }

    static void DrawGeometry(EntityPhysicsDebugSnapshot snapshot)
    {
        EditorGUILayout.Space();
        GUILayout.Label("几何状态", EditorStyles.boldLabel);
        EditorGUILayout.Toggle("着地", snapshot.isGrounded);
        EditorGUILayout.Toggle("顶头", snapshot.isTopBlocked);
        EditorGUILayout.Toggle("左墙", snapshot.isOnLeftWall);
        EditorGUILayout.Toggle("右墙", snapshot.isOnRightWall);
        DrawVector("地面法线", snapshot.groundNormal);
        EditorGUILayout.ObjectField("脚下 Collider", snapshot.groundCollider, typeof(Collider2D), true);
        EditorGUILayout.ObjectField("左墙 Collider", snapshot.leftWallCollider, typeof(Collider2D), true);
        EditorGUILayout.ObjectField("右墙 Collider", snapshot.rightWallCollider, typeof(Collider2D), true);
        EditorGUILayout.Space();
        GUILayout.Label("相位 4 候选环境（原始命中）", EditorStyles.boldLabel);
        DrawVector("候选中心", snapshot.candidateCenter);
        EditorGUILayout.IntField("路径命中数", snapshot.candidatePathCount);
        EditorGUILayout.ObjectField("首个路径 Collider", snapshot.candidateFirstPathCollider, typeof(Collider2D), true);
        DrawVector("首个路径法线", snapshot.candidateFirstPathNormal);
        EditorGUILayout.FloatField("首个路径距离", snapshot.candidateFirstPathDistance);
        EditorGUILayout.Toggle("首个路径零距离", snapshot.candidateFirstPathZeroDistance);
        EditorGUILayout.IntField("终点重叠数", snapshot.candidateOverlapCount);
        EditorGUILayout.ObjectField("首个终点 Collider", snapshot.candidateFirstOverlapCollider, typeof(Collider2D), true);
        EditorGUILayout.IntField("单向平台数", snapshot.candidateOneWayCount);
        EditorGUILayout.IntField("向下命中数", snapshot.candidateDownwardCount);
        DrawVector("首个向下命中法线", snapshot.candidateFirstDownwardNormal);
        EditorGUILayout.FloatField("首个向下命中间隙", snapshot.candidateFirstDownwardGap);
        EditorGUILayout.Toggle("首个向下命中零距离", snapshot.candidateFirstDownwardZeroDistance);
    }

    static void DrawEnvironment(EntityPhysicsDebugSnapshot snapshot)
    {
        EditorGUILayout.Space();
        GUILayout.Label("环境接入（只读）", EditorStyles.boldLabel);
        EditorGUILayout.IntField("有效环境来源", snapshot.environmentSourceCount);
        EditorGUILayout.IntField("主动移速修饰项", snapshot.movementModifierCount);
        EditorGUILayout.IntField("持续速度项", snapshot.stateVelocityCount);
        EditorGUILayout.IntField("动态力项（含接触及消退）", snapshot.dynamicForceCount);
        EditorGUILayout.FloatField("支撑面消退阻力倍率", snapshot.environmentSlowingMultiplier);
        EditorGUILayout.FloatField("支撑面跳跃加成", snapshot.environmentJumpHeightOffset);
    }

    static void DrawContactPairs(BasicEntity target)
    {
        EditorGUILayout.Space();
        GUILayout.Label("本物理帧预测接触对", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("接触批次 Fixed Tick", PhysicsEditorContactObservation.PublishedFixedTick.ToString());

        int count = 0;
        foreach (ContactPairDebugSnapshot pair in PhysicsEditorContactObservation.Contacts)
        {
            if (pair.entityA != target && pair.entityB != target)
                continue;

            count++;
            BasicEntity other = pair.entityA == target ? pair.entityB : pair.entityA;
            Vector2 ownOffset = pair.entityA == target ? pair.offsetA : pair.offsetB;
            Vector2 ownNormal = pair.entityA == target ? pair.normalA : -pair.normalA;
            bool ownAppliedForce = pair.entityA == target
                ? pair.aAppliedForce
                : pair.bAppliedForce;
            bool otherAppliedForce = pair.entityA == target
                ? pair.bAppliedForce
                : pair.aAppliedForce;
            float ownTargetSpeed = pair.entityA == target
                ? pair.targetSpeedAToB
                : pair.targetSpeedBToA;
            float otherTargetSpeed = pair.entityA == target
                ? pair.targetSpeedBToA
                : pair.targetSpeedAToB;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("对方", other != null ? other.name : "<Missing>");
            EditorGUILayout.LabelField("挤出轴", pair.separateOnX ? "X" : "Y");
            EditorGUILayout.LabelField("原始重叠深度", pair.depth.ToString("F5"));
            EditorGUILayout.LabelField("实际修正深度", pair.correctionDepth.ToString("F5"));
            EditorGUILayout.LabelField("自身运动相对闭合速度", pair.closingSpeed.ToString("F5"));
            EditorGUILayout.LabelField(
                "本方 / 对方施力",
                $"{ownAppliedForce} / {otherAppliedForce}");
            EditorGUILayout.LabelField(
                "本方 / 对方目标速度",
                $"{ownTargetSpeed:F5} / {otherTargetSpeed:F5}");
            DrawVector("本方分离法线", ownNormal);
            DrawVector("本方偏置", ownOffset);
            EditorGUILayout.LabelField(
                "权重 A / B", $"{pair.weightA:F3} / {pair.weightB:F3}");
            EditorGUILayout.EndVertical();
        }

        if (count == 0)
            EditorGUILayout.LabelField("无预测重叠");
    }

    static void DrawVector(string label, Vector2 value)
    {
        EditorGUILayout.LabelField(label, $"({value.x:F5}, {value.y:F5})");
    }
}

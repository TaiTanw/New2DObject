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
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
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

        EntityPhysicsDebugSnapshot snapshot = targetEntity.DebugSnapshot;
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawIdentity(snapshot);
            DrawMotion(snapshot);
            DrawGeometry(snapshot);
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
        EditorGUILayout.LabelField("Body Type", snapshot.bodyType.ToString());
        DrawVector("实际位置（相位 3 采样）", snapshot.actualPosition);
        EditorGUILayout.LabelField("旋转", snapshot.rotation.ToString("F3"));
        EditorGUILayout.LabelField("角速度", snapshot.angularVelocity.ToString("F3"));
    }

    static void DrawMotion(EntityPhysicsDebugSnapshot snapshot)
    {
        EditorGUILayout.Space();
        GUILayout.Label("运动与解算", EditorStyles.boldLabel);
        DrawVector("平面总速度", snapshot.planarVelocity);
        DrawVector("待下帧接触速度", snapshot.pendingSecondOrderSpeed);
        DrawVector("速度积分位移", snapshot.integratedVelocityDelta);
        DrawVector("斜坡修正后位移", snapshot.motionDelta);
        DrawVector("平台位移", snapshot.platformDelta);
        DrawVector("接触对偏置", snapshot.solverOffset);
        DrawVector("静墙裁剪前位移", snapshot.unconstrainedDelta);
        DrawVector("最终请求位移", snapshot.requestedDelta);
        DrawVector("MovePosition 目标", snapshot.requestedTarget);
        DrawVector("引擎修正量", snapshot.engineCorrection);
        EditorGUILayout.Toggle("发生静墙整轴裁剪", snapshot.staticWallClamped);

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
    }

    static void DrawContactPairs(BasicEntity target)
    {
        EditorGUILayout.Space();
        GUILayout.Label("本物理帧预测接触对", EditorStyles.boldLabel);

        int count = 0;
        foreach (ContactPairDebugSnapshot pair in PhysicsSolverMgr.Instance.DebugContacts)
        {
            if (pair.entityA != target && pair.entityB != target)
                continue;

            count++;
            BasicEntity other = pair.entityA == target ? pair.entityB : pair.entityA;
            Vector2 ownOffset = pair.entityA == target ? pair.offsetA : pair.offsetB;
            Vector2 ownNormal = pair.entityA == target ? pair.normalA : -pair.normalA;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("对方", other != null ? other.name : "<Missing>");
            EditorGUILayout.LabelField("挤出轴", pair.separateOnX ? "X" : "Y");
            EditorGUILayout.LabelField("重叠深度", pair.depth.ToString("F5"));
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

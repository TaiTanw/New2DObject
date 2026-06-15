// PlayerDataWindow.cs
using UnityEditor;
using UnityEngine;

public class PlayerDataWindow : EditorWindow
{
    private Player targetPlayer; // 要观察的角色组件

    [MenuItem("自定义工具/玩家实时数据显示")] // 菜单项，点击打开窗口
    public static void ShowWindow()
    {
        GetWindow<PlayerDataWindow>("Player Data");
    }

    private void OnEnable()
    {
        // 注册更新回调，让窗口不断重绘
        EditorApplication.update += Repaint;
    }

    private void OnDisable()
    {
        // 窗口关闭时取消注册，避免报错
        EditorApplication.update -= Repaint;
    }

    private void OnGUI()
    {
        GUILayout.Label("实时角色数据查看", EditorStyles.boldLabel);

        // 手动拖入或自动查找
        //targetPlayer = (Player)EditorGUILayout.ObjectField("目标角色", targetPlayer, typeof(Player), true);
        // 关键：避免非运行情况下访问管理器
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("请先进入 Play 模式", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("刷新并关联当前控制器玩家"))
        {
            targetPlayer=InputControlMgr.Instance.Player;

        }
        if (targetPlayer == null)
        {
            EditorGUILayout.HelpBox("当前未关联到玩家，请在游戏正式运行时关联", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("世界坐标", targetPlayer.transform.position.ToString());
        // 画一条分隔线
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("当前相对水平速度", targetPlayer.NowPhyData.horizontalSpeed.ToString());
        EditorGUILayout.LabelField("当前竖直下落速度", targetPlayer.NowPhyData.verticalSpeed.ToString());
        EditorGUILayout.LabelField("当前状态机", targetPlayer._ActionData.NowState.ToString());

    }
}
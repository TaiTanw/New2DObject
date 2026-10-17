using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>场景测试入口：F 或按钮切换普通行走与穿墙四向移动，运动仍走框架时序。</summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Player))]
public sealed class PlayerFourWayTesk : MonoBehaviour
{
    private Player player;
    private string feedback;

    private void Awake() => player = GetComponent<Player>();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            ToggleFlight();
    }

    private void ToggleFlight()
    {
        feedback = player.TrySetFlying(!player.IsFlying) ? null
            : "当前位置不能恢复实心，请先移出障碍物。";
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16f, 16f, 340f, 110f), GUI.skin.box);
        GUILayout.Label("WASD 移动 / F 切换飞行；水中自动游泳");
        if (GUILayout.Button(player.IsFlying ? "恢复行走（F）" : "四向飞行（F）")) ToggleFlight();
        if (!string.IsNullOrEmpty(feedback)) GUILayout.Label(feedback);
        GUILayout.EndArea();
    }
}

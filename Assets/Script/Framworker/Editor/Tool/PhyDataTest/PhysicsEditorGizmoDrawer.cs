#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 绘制角色和箱体的物理探测框。
/// 职能：Editor Scene 视图绘制；只读探针位置和尺寸，不写入物理状态。
/// </summary>
public static class PhysicsEditorGizmoDrawer
{
    private static readonly Color ProbeColor = new Color(0f, 1f, 0f, 0.5f);

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawCharacterProbes(BasePhysicsEntity entity, GizmoType gizmoType)
    {
        if (entity == null || !entity.TryGetEditorGizmoData(out PhysicsEditorObservationBridge.GizmoData data))
            return;
        if (!data.hasGround || !data.hasLeft || !data.hasRight)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = ProbeColor;
        Gizmos.DrawWireCube(data.groundCenter, data.horizontalSize);
        Gizmos.DrawWireCube(data.leftCenter, data.verticalSize);
        Gizmos.DrawWireCube(data.rightCenter, data.verticalSize);
        Gizmos.color = previousColor;
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawBoxProbes(PhysicalBox entity, GizmoType gizmoType)
    {
        if (entity == null || !entity.TryGetEditorGizmoData(out PhysicsEditorObservationBridge.GizmoData data))
            return;
        if (!data.hasGround || !data.hasTop || !data.hasLeft || !data.hasRight)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = ProbeColor;
        Gizmos.DrawWireCube(data.groundCenter, data.horizontalSize);
        Gizmos.DrawWireCube(data.topCenter, data.horizontalSize);
        Gizmos.DrawWireCube(data.leftCenter, data.verticalSize);
        Gizmos.DrawWireCube(data.rightCenter, data.verticalSize);
        Gizmos.color = previousColor;
    }
}
#endif

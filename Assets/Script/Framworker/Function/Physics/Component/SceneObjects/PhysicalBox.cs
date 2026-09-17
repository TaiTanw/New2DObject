using PhyData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicalBox : BasicEntity
{
    protected override void Awake()
    {
        base.Awake();
        // 当前推箱解算只处理平移。冻结物理根节点旋转；表现子节点仍可独立旋转。
        rb.freezeRotation = true;
        rb.angularVelocity = 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (cPhysics == null) return;
        if (groundV == null) return;
        if (upV == null) return;
        if (leftV == null) return;
        if (rightV == null) return;
        // 设置颜色：绿色半透明，便于观察
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.5f);

        // 绘制检测区域的线框矩形（位置、大小、旋转）
        Gizmos.DrawWireCube(groundV.position, cPhysics.boxCastH);
        Gizmos.DrawWireCube(upV.position, cPhysics.boxCastH);
        Gizmos.DrawWireCube(leftV.position, cPhysics.boxCastV);
        Gizmos.DrawWireCube(rightV.position, cPhysics.boxCastV);
    }
#endif
    protected override void GeometricQuery()
    {
        float a = Vector2.SignedAngle(Vector2.up,transform.up);
        RaycastHit2D hit = Physics2D.BoxCast(groundV.position, cPhysics.boxCastH, a, -transform.up, 0f, cPhysics.groundLayer);
        //判断是否顶头
        RaycastHit2D topHit = Physics2D.BoxCast(upV.position, cPhysics.boxCastH, a, transform.up, 0f, cPhysics.groundLayer);
        nowGemetry.istop = topHit.collider != null;
        //缓存真实命中法线；斜坡位移与着地判定必须使用表面信息
        nowGemetry.groundNormal = hit.normal;
        bool onGroundNow = false;
        //默认的空中阻力系数
        self_resistanceCoefficient = 1;
        //一定角度内正对碰撞才算着地
        if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
        {
            onGroundNow = true;
            //在地面，则自身阻力增大
            self_resistanceCoefficient = 20;
        }

        // 几何只保留 Collider；脚下能力由相位 2 经 groundCollider 解析。
        nowGemetry.isGrounded = onGroundNow;
        nowGemetry.groundCollider = onGroundNow ? hit.collider : null;
        //检测左右靠墙
        RaycastHit2D hit1 = Physics2D.BoxCast(leftV.position, cPhysics.boxCastV, a, -transform.right, 0f, cPhysics.wallLayer);
        RaycastHit2D hit2 = Physics2D.BoxCast(rightV.position, cPhysics.boxCastV, a, transform.right, 0f, cPhysics.wallLayer);
        nowGemetry.onLeftWall = false;
        nowGemetry.leftWallCollider = null;
        //夹角需小于25度，内积近似0.9
        if (hit1.collider != null && Vector2.Dot(hit1.normal, Vector2.right) > 0.9f)
        {
            nowGemetry.onLeftWall = true;
            nowGemetry.leftWallCollider = hit1.collider;
        }
        nowGemetry.onRightWall = false;
        nowGemetry.rightWallCollider = null;
        if (hit2.collider != null && Vector2.Dot(hit2.normal, Vector2.left) > 0.9f)
        {
            nowGemetry.onRightWall = true;
            nowGemetry.rightWallCollider = hit2.collider;
        }

    }

}

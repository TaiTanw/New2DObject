using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 场景物体拖拽接口
/// </summary>
public interface IobjDrag 
{
    void DragIn(Vector3 delta);

    void OnDrag(Vector3 delta);

    void DragOut(Vector3 delta);


}

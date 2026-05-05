using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 拖拽测试物体
/// </summary>
public class uoo : MonoBehaviour,IobjDrag
{
    public void DragIn(Vector3 delta)
    {
        print("开始拖拽");
        transform.localScale = Vector3.one*1.5f;
    }

    public void DragOut(Vector3 delta)
    {
        print("拖拽结束");
        transform.localScale = Vector3.one ;
    }

    public void OnDrag(Vector3 delta)
    {
        print("正在拖拽");
        transform.position =delta;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

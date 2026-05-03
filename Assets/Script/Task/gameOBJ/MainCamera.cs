using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
    Vector3 toPoint;

    float followSpeed = 5f;
    private void Awake()
    {
        InputControlMgr.Instance.SetMainCamera(this);

    }
    void Start()
    {
    }
    public void SetPoint(Vector3 vector3)
    {
        toPoint = new Vector3(vector3.x, vector3.y, -10);
    }
    // Update is called once per frame
    void Update()
    {
        //摄像机缓动
        transform.position = Vector3.Lerp(transform.position, toPoint, followSpeed*Time.deltaTime);
    }
}

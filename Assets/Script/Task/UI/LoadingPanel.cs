using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPanel : BasePanel
{
    // Start is called before the first frame update
    /// <summary>
    /// 进度条
    /// </summary>
    Slider slider;
    protected override void Awake()
    {
        base.Awake();
        slider = FindUIObj<Slider>("LoadingSlider");
        //GameObject.DontDestroyOnLoad(this.gameObject);
    }
    private void OnEnable()
    {
        slider.value = 0;
        EventCenterSystem.Instance.AddEventListener<float>(E_EventEnum.E_LoadScene, SetValue);
    }

    void SetValue(float value)
    {
        slider.value += value;
    }

    private void OnDisable()
    {
        EventCenterSystem.Instance.RemoveEventListener<float>(E_EventEnum.E_LoadScene, SetValue);
    }


    void Update()
    {
        
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{

    private void Awake()
    {
        DataConfigurationMgr.Instance.Init();
        DataAndInitMgr.Instance.Init();

        SceneLogicMgr.Instance.Init();

        UIMgr.Instance.Init();
        MusicMgr.Instance.Init();

        InputControlMgr.Instance.Init();
        //缓存加载界面
        UIMgr.Instance.ShowOneUI<LoadingPanel>(E_UILayer.Top, (UI) =>
        {
            UI.gameObject.SetActive(false);
        });
        //print(Application.persistentDataPath);
    }
    void Start()
    {
        //UIMgr.Instance.ShowOneUI<BeginPanel>();
        //MusicMgr.Instance.PlayBkMusic("BkMusic");

        GameObject.DontDestroyOnLoad(this.gameObject);
        //GameObjData data = new GameObjData();
        //print(data.TestInfoDic[2].name);
        //读表功能测试
        //yyydddd yyydddd = new();
        //print(yyydddd.Sheet1Dic[2].name);
        //print(yyydddd.Sheet1Dic[3].name);
    }
}

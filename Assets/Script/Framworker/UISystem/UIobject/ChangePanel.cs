using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ChangePanel : BasePanel
{
    protected override void OnClickButton(string UIname)
    {
        base.OnClickButton(UIname);

        switch (UIname)
        {
            case "t1":
                SceneChangeMgr.Instance.LoadResToChangeSence("TeskScenes1", new Dictionary<string, int> { { "Taijie", 15 } });
                InputControlMgr.Instance.InputOpenOrClose(true);
                UIMgr.Instance.HideOneUI<ChangePanel>();
                break;
            case "t2":
                SceneChangeMgr.Instance.LoadResToChangeSence("TeskScenes2");
                InputControlMgr.Instance.InputOpenOrClose(true);
                UIMgr.Instance.HideOneUI<ChangePanel>();
                break;
            case "fan":
                //UIMgr.Instance.ShowOneUI<BeginPanel>();
                //UIMgr.Instance.HideOneUI<ChangePanel>();
                UIMgr.Instance.UIBack();
                break;
        }
    }
}

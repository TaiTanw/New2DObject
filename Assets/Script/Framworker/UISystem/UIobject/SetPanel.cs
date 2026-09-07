using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SetPanel : BasePanel
{
    protected override void OnClickButton(string UIname)
    {
        base.OnClickButton(UIname);
        switch(UIname) 
        {
            case "mu":
                UIMgr.Instance.SceneUIShow(E_UI_Process.MusicSetPanel);
                break;
            case "ke":
                //UIMgr.Instance.ShowOneUI<InputChangePanel>();

                UIMgr.Instance.SceneUIShow(E_UI_Process.InputChangePanel);
                break;
            case "hui":
                //UIMgr.Instance.HideOneUI<SetPanel>();
                //UIMgr.Instance.ShowOneUI<BeginPanel>();

                UIMgr.Instance.UIBack();
                break;

        }
    }
}

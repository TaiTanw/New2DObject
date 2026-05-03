using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MusicSetPanel : BasePanel
{
    private Toggle tt1;
    private Toggle tt2;
    private Slider s1;
    private Slider s2;
    protected override void Awake()
    {
        base.Awake();
        tt1 = FindUIObj<Toggle>("tt1");
        tt2 = FindUIObj<Toggle>("tt2");
        s1 = FindUIObj<Slider>("s1");
        s2 = FindUIObj<Slider>("s2");
    }

    protected override void ToggleValueChange(string sliderName, bool value)
    {
        base.ToggleValueChange(sliderName, value);
        switch (sliderName)
        {
            case "tt1":
                if (value)
                {
                    MusicMgr.Instance.BknoMute=value;
                    MusicMgr.Instance.StartBkMusic();
                }
                else
                {
                    MusicMgr.Instance.BknoMute=value;
                    MusicMgr.Instance.PauseBKMusic();//暂停背景音乐
                }
            break;

            case "tt2":
                MusicMgr.Instance.SoundnoMute=value;
                MusicMgr.Instance.PauseOrPlaySoundMusic(value);
                break;
        }
    }

    protected override void SliderValueChange(string UIname, float value)
    {
        base.SliderValueChange(UIname, value);
        switch (UIname)
        {
            case "s1":
                MusicMgr.Instance.BkVolume=value;
                MusicMgr.Instance.SetBkMusicVolume(value);
                break;
            case "s2":
                MusicMgr.Instance.SoundVolume=value;
                MusicMgr.Instance.SetSoundVolume(value);
                break;

        }
    }

    protected override void OnClickButton(string UIname)
    {
        base.OnClickButton(UIname);
        UIMgr.Instance.UIBack();
    }

    public override void HideMe()
    {
        base.HideMe();
       
    }
    public override void ShowMe()
    {
        base.ShowMe();
        tt1.isOn = DataAndInitMgr.Instance.musicData.bkMusicOpen;
        tt2.isOn = DataAndInitMgr.Instance.musicData.soundOpen;
        s1.value = DataAndInitMgr.Instance.musicData.bkMusicValue;
        s2.value = DataAndInitMgr.Instance.musicData.soundValue;
        //print("音乐设置面板开启");

    }

    private void OnDisable()
    {
        //隐藏面板时，再开始存入数据
        DataAndInitMgr.Instance.musicData.bkMusicOpen = tt1.isOn;
        DataAndInitMgr.Instance.musicData.soundOpen = tt2.isOn;
        DataAndInitMgr.Instance.musicData.bkMusicValue = s1.value;
        DataAndInitMgr.Instance.musicData.soundValue = s2.value;
        DataAndInitMgr.Instance.SaveMusicData();
    }
}

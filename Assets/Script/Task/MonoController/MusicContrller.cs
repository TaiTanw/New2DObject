using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class MusicContrller : MonoBehaviour
{
    [Header("此场景音乐")]
    /// <summary>
    /// 此场景音乐指示
    /// </summary>
    public AssetReferenceT<AudioClip> bgmRef;

    [Header("是否默认播放")]
    public bool toplay;
    private void Awake()
    {
        if (toplay)
        {
            MusicMgr.Instance.PlayBkMusic(bgmRef.RuntimeKey.ToString());
        }
        else
        {
            MusicMgr.Instance.PlayBkMusic(bgmRef.RuntimeKey.ToString(), (mu) =>
            {
                //不循环音乐
                mu.loop = false;
                MusicMgr.Instance.StartBkMusic();
                MusicMgr.Instance.PauseBKMusic();
            });
        }
    }

    private void OnDestroy()
    {
        //主页面音乐资源卸载
        MusicMgr.Instance.StopBkMusic(true, bgmRef.RuntimeKey.ToString());
    }
}

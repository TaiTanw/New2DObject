using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;
using UnityEngine.EventSystems;

public class DialogBoxPanel : BasePanel, IPointerClickHandler
{
    TextMeshProUGUI tmp;

    protected override void Awake()
    {
        base.Awake();
        tmp = FindUIObj<TextMeshProUGUI>("T1");
    }

    bool isTyping = false;

    bool isOk = false;
    /// <summary>
    /// 是否可进行到下一步
    /// </summary>
    public bool IsOK=>isOk;
    CancellationTokenSource cts;

    string currentText;
    /// <summary>
    /// 开始打字
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public async UniTask PlayText(string str)
    {
        currentText = str;

        cts?.Cancel();
        cts = new CancellationTokenSource();

        isTyping = true;
        isOk = false;
        tmp.text = str;
        tmp.maxVisibleCharacters = 0;

        try
        {
            for (int i = 0; i <= str.Length; i++)
            {
                tmp.maxVisibleCharacters = i;
                await UniTask.Delay(40, cancellationToken: cts.Token);
            }
        }
        catch
        {
        }

        tmp.maxVisibleCharacters = str.Length;
        isTyping = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isTyping)
        {
            cts.Cancel(); // 停止打字
            tmp.maxVisibleCharacters = currentText.Length;
            isTyping = false;
        }
        else
        {
            NextDialog();
        }
    }

    void NextDialog()
    {
        Debug.Log("内部赋值");
        isOk = true;
    }
}

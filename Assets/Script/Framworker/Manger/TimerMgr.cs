using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 计时器管理器
/// </summary>
public class TimerMgr : BaseMgr<TimerMgr>
{
    public class TimerItemData : I_InitDataToPool
    {
        //DataFormatting 只能在真正回收时调用
        public void DataFormatting()
        {
            //数据格式化
            endCallBack=null;
            intervalCallBack=null;
            //将是否初始化标识改为false
            isNewInit=false;
            isOnDelet=false;
        }
        /// <summary>
        /// 初始化方法，单位：毫秒
        /// </summary>
        /// <param name="eCallBack"></param>
        /// <param name="eTime"></param>
        /// <param name="iCallBack"></param>
        /// <param name="iTime"></param>
        public void InitTimer(UnityAction eCallBack,int eTime,UnityAction iCallBack=null,int iTime=1)
        {
            //数据初始化
            if (isNewInit)
            {
                Debug.LogError("不可在已经初始化计时器中再次调用");
                //此处确保回调干净，且保证运行时间只有初始化，主动重置，自行计算的情况下才会变化
                return;
            }
            endTime = eTime;
            intervalTime = iTime;
            endCallBack += eCallBack;
            intervalCallBack += iCallBack;
            nowEndTime = endTime;
            nowIntervalTime = intervalTime;
            isNewInit = true;
        }
        /// <summary>
        /// 开启或暂停
        /// </summary>
        /// <param name="run"></param>
        public void RunOrStop(bool run)
        {
            isRun = run;
        }
        /// <summary>
        /// 重置时间（距离结束的时间
        /// </summary>
        public void ResetEndTime()
        {
            nowEndTime = endTime;
        }
        /// <summary>
        /// 回调增减
        /// </summary>
        /// <param name="eCallBack"></param>
        public void AddCallBack(UnityAction eCallBack)
        {

        }
        /// <summary>
        /// 计时器唯一标识
        /// </summary>
        public int ID;
        /// <summary>
        /// 计时器结束时回调
        /// </summary>
        public UnityAction endCallBack;
        /// <summary>
        /// 计时器间隔事件回调
        /// </summary>
        public UnityAction intervalCallBack;
        /// <summary>
        /// 计时器总时间
        /// </summary>
        public int endTime;
        /// <summary>
        /// 计时器间隔时间
        /// </summary>
        public int intervalTime;
        /// <summary>
        /// 当前时间，距离结束所剩时间
        /// </summary>
        public int nowEndTime;
        /// <summary>
        /// 距离间隔触发所剩时间
        /// </summary>
        public int nowIntervalTime;
        //此计时器是否开启
        public bool isRun;
        /// <summary>
        /// 是否已经初始化
        /// </summary>
        public bool isNewInit;
        /// <summary>
        /// 是否待删除
        /// </summary>
        public bool isOnDelet;
        /// <summary>
        /// 计时器自行进行的时针推动
        /// </summary>
        public void TimerRun()
        {
            if (isRun)
            {
                if(intervalCallBack != null)
                {
                    nowIntervalTime -= (int)(minTimeDelta*1000);
                    if(nowIntervalTime <= 0)
                    {
                        //间隔时间到，执行间隔回调
                        intervalCallBack?.Invoke();
                        //重置间隔时间
                        nowIntervalTime = intervalTime;
                    }
                }
                nowEndTime -= (int)(minTimeDelta * 1000);
                if (nowEndTime <= 0)
                {
                    //计时结束，执行结束回调
                    endCallBack?.Invoke();

                    isOnDelet = true;   // 标记删除
                    
                }

            }
        }
    }
    /// <summary>
    /// 计时器运行的最小单位
    /// </summary>
    public static float minTimeDelta = 0.1f;
    /// <summary>
    /// 唯一ID生成
    /// </summary>
    private int oneID = 0;
    /// <summary>
    /// 用于统一管理计时器的协程
    /// </summary>
    public Coroutine coroutine;
    /// <summary>
    /// 避免频繁创建的性能消耗
    /// </summary>
    private WaitForSeconds waitForSeconds;
    public TimerMgr()
    {
        Debug.Log("计时器管理器初始化");
        waitForSeconds=new WaitForSeconds(minTimeDelta);
        coroutine = MonoPublicMgr.Instance.StartCoroutine(TimeisRuning());
    }
    IEnumerator TimeisRuning()
    {
        while (true)
        {
            //控制计时器按照设定的最小时间运行，避免过大的性能开销
            yield return waitForSeconds;
            //遍历计时器字典，执行计时和待删除放入
            foreach(var v in timerDic)
            {
                if (!v.Value.isOnDelet)
                {
                    //时针执行
                    v.Value.TimerRun();
                }
                else//当发现计时器已经格式化后，放入待删除列表
                {

                    timerList.Add(v.Value);
                    
                } 
            }
            //开始执行计时器删除(将遍历和删除分开，防止出错
            foreach(var v in timerList)
            {
                //字典对应引用删除
                timerDic.Remove(v.ID);
                //数据格式化
                v.DataFormatting();
                //放入缓存池
                PoolMgr.Instance.PushInObj<TimerItemData>(v);
            }
            //清空待删除列表
            if (timerList.Count>0)
            {
                timerList.Clear();
            }
        }
    }

    /// <summary>
    /// 计时器容器
    /// </summary>
    private Dictionary<int,TimerItemData> timerDic = new Dictionary<int,TimerItemData>();
    public Dictionary<int, TimerItemData> keyDic=>timerDic;
    /// <summary>
    /// 待删除列表
    /// </summary>
    private List<TimerItemData> timerList = new List<TimerItemData>();
    /// <summary>
    /// 创建计时器，默认创建即为启动，返回值为计时器ID,单位：毫秒
    /// 若声明创建时不启动，则通过返回值设置
    /// </summary>
    /// <param name="eCallBack">结束时回调</param>
    /// <param name="eTime">结束时间</param>
    /// <param name="iCallBack">间隔时回调</param>
    /// <param name="iTime">间隔时间</param>
    /// <param name="isrun">是否开启</param>
    /// <returns></returns>
    public int StartTimerDataObj(UnityAction eCallBack, int eTime, UnityAction iCallBack = null, int iTime = 1,bool isrun=true)
    {
        TimerItemData t = PoolMgr.Instance.GetPoolValue<TimerItemData>();
        t.InitTimer(eCallBack, eTime, iCallBack, iTime);
        t.RunOrStop(isrun);
        //赋值唯一ID
        t.ID=oneID;
        //ID自增1，防止重复
        oneID += 1;
        if (timerDic.ContainsKey(t.ID))
        {
            Debug.LogError("取出的计时器有重复ID！");
        }
        timerDic.Add(t.ID, t);
        return t.ID;
    }
     
    /// <summary>
    /// 删除指定计时器
    /// </summary>
    /// <param name="timerID"></param>
    public void DeleteOneTimer(int timerID)
    {
        if (timerDic.ContainsKey(timerID))
        {
            //timerDic[timerID].InitMyData();
            timerDic[timerID].isOnDelet=true;
        }
        else
        {
            Debug.LogError($"未找到ID为{timerID}的计时器");
        }
    }

}

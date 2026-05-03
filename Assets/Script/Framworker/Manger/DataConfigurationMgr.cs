using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataConfigurationMgr : BaseMgr<DataConfigurationMgr> 
{
    #region SO数据配置
    StartRes res;
    /// <summary>
    /// 默认资源路径
    /// </summary>
    public StartRes StartRes=>res;
    #endregion

    #region Excel数据配置

    #endregion
    public void Init()
    {
        res = Resources.Load<StartRes>("Data/StartResName");
    }
}

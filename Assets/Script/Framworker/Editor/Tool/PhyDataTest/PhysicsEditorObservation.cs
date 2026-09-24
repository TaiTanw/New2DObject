#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器观测者按需订阅物理帧和相位通知；退出播放或程序集重载时解除桥接订阅。
/// 职能：管理编辑器观测订阅生命周期，并隔离各观测者异常。
/// </summary>
[InitializeOnLoad]
public static class PhysicsEditorObservation
{
    private static Action<int>[] frameObservers = Array.Empty<Action<int>>();
    private static Action<int, int>[] phaseObservers = Array.Empty<Action<int, int>>();
    private static bool bridgeSubscribed;

    public static event Action<int> PhysicsFrameStarted
    {
        add { frameObservers = AddObserver(frameObservers, value); SubscribeBridge(); }
        remove { frameObservers = RemoveObserver(frameObservers, value); UnsubscribeWhenUnused(); }
    }

    public static event Action<int, int> PhysicsPhaseCompleted
    {
        add { phaseObservers = AddObserver(phaseObservers, value); SubscribeBridge(); }
        remove { phaseObservers = RemoveObserver(phaseObservers, value); UnsubscribeWhenUnused(); }
    }

    static PhysicsEditorObservation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += UnsubscribeBridge;
        if (Application.isPlaying) SubscribeBridge();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            PhysicsEditorObservationBridge.ResetFixedTick();
            SubscribeBridge();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            UnsubscribeBridge();
        }
    }

    private static void SubscribeBridge()
    {
        if (bridgeSubscribed || !Application.isPlaying ||
            (frameObservers.Length == 0 && phaseObservers.Length == 0)) return;

        PhysicsEditorObservationBridge.PhysicsFrameStarted += DispatchFrameStarted;
        PhysicsEditorObservationBridge.PhysicsPhaseCompleted += DispatchPhaseCompleted;
        bridgeSubscribed = true;
    }

    private static void UnsubscribeWhenUnused()
    {
        if (frameObservers.Length == 0 && phaseObservers.Length == 0)
            UnsubscribeBridge();
    }

    private static void UnsubscribeBridge()
    {
        if (!bridgeSubscribed) return;
        PhysicsEditorObservationBridge.PhysicsFrameStarted -= DispatchFrameStarted;
        PhysicsEditorObservationBridge.PhysicsPhaseCompleted -= DispatchPhaseCompleted;
        bridgeSubscribed = false;
    }

    private static void DispatchFrameStarted(int fixedTick)
    {
        Action<int>[] observers = frameObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](fixedTick); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private static void DispatchPhaseCompleted(int fixedTick, int phase)
    {
        Action<int, int>[] observers = phaseObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](fixedTick, phase); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private static T[] AddObserver<T>(T[] observers, T observer) where T : Delegate
    {
        if (observer == null) return observers;
        T[] updated = new T[observers.Length + 1];
        Array.Copy(observers, updated, observers.Length);
        updated[observers.Length] = observer;
        return updated;
    }

    private static T[] RemoveObserver<T>(T[] observers, T observer) where T : Delegate
    {
        if (observer == null) return observers;
        int index = -1;
        for (int i = observers.Length - 1; i >= 0; i--)
        {
            if (Equals(observers[i], observer))
            {
                index = i;
                break;
            }
        }
        if (index < 0) return observers;

        T[] updated = new T[observers.Length - 1];
        Array.Copy(observers, 0, updated, 0, index);
        Array.Copy(observers, index + 1, updated, index, observers.Length - index - 1);
        return updated;
    }
}
#endif

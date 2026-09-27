#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// 物理调度器在每个 FixedUpdate 开始及各相位完整结束时发出通知，供编辑器观测器读取已产生的事实。
/// 职能：运行逻辑与编辑器观测之间的只读时序桥，不保存物理数据，也不参与解算。
/// </summary>
public static class PhysicsEditorObservationBridge
{
    public delegate void MotionBuiltObserver(
        BasicEntity entity,
        Vector2 freeVelocity,
        Vector2 integratedVelocityDelta,
        Vector2 motionDelta,
        Vector2 platformDelta,
        Vector2 plannedWorldDelta);
    public delegate void PredictionBuiltObserver(BasicEntity entity, Vector2 center, Vector2 extents);
    public delegate void MovementRequestedObserver(
        BasicEntity entity,
        Vector2 solverOffset,
        Vector2 unconstrainedDelta,
        Vector2 requestedDelta,
        Vector2 requestedTarget,
        bool horizontalEnvironmentBlocked);
    public delegate void ContactPairObserver(
        BasicEntity entityA,
        BasicEntity entityB,
        Vector2 predictedCenterA,
        Vector2 predictedCenterB,
        Vector2 centerDelta,
        bool separateOnX,
        float depth,
        float correctionDepth,
        float closingSpeed,
        bool aAppliedForce,
        bool bAppliedForce,
        float targetSpeedAToB,
        float targetSpeedBToA,
        float weightA,
        float weightB,
        Vector2 offsetA,
        Vector2 offsetB);

    public struct GizmoData
    {
        public bool hasGround;
        public bool hasTop;
        public bool hasLeft;
        public bool hasRight;
        public Vector3 groundCenter;
        public Vector3 topCenter;
        public Vector3 leftCenter;
        public Vector3 rightCenter;
        public Vector2 horizontalSize;
        public Vector2 verticalSize;
    }

    private static Action<int>[] frameObservers = Array.Empty<Action<int>>();
    private static Action<int, int>[] phaseObservers = Array.Empty<Action<int, int>>();
    private static Action<BasicEntity>[] entityEnabledObservers = Array.Empty<Action<BasicEntity>>();
    private static Action<BasicEntity>[] entityDisabledObservers = Array.Empty<Action<BasicEntity>>();
    private static MotionBuiltObserver[] motionObservers = Array.Empty<MotionBuiltObserver>();
    private static PredictionBuiltObserver[] predictionObservers = Array.Empty<PredictionBuiltObserver>();
    private static MovementRequestedObserver[] movementObservers = Array.Empty<MovementRequestedObserver>();
    private static ContactPairObserver[] contactPairObservers = Array.Empty<ContactPairObserver>();

    public static event Action<int> PhysicsFrameStarted
    {
        add { frameObservers = AddObserver(frameObservers, value); }
        remove { frameObservers = RemoveObserver(frameObservers, value); }
    }

    public static event Action<int, int> PhysicsPhaseCompleted
    {
        add { phaseObservers = AddObserver(phaseObservers, value); }
        remove { phaseObservers = RemoveObserver(phaseObservers, value); }
    }

    public static event Action<BasicEntity> EntityEnabled
    {
        add { entityEnabledObservers = AddObserver(entityEnabledObservers, value); }
        remove { entityEnabledObservers = RemoveObserver(entityEnabledObservers, value); }
    }

    public static event Action<BasicEntity> EntityDisabled
    {
        add { entityDisabledObservers = AddObserver(entityDisabledObservers, value); }
        remove { entityDisabledObservers = RemoveObserver(entityDisabledObservers, value); }
    }

    public static event MotionBuiltObserver MotionBuilt
    {
        add { motionObservers = AddObserver(motionObservers, value); }
        remove { motionObservers = RemoveObserver(motionObservers, value); }
    }

    public static event PredictionBuiltObserver PredictionBuilt
    {
        add { predictionObservers = AddObserver(predictionObservers, value); }
        remove { predictionObservers = RemoveObserver(predictionObservers, value); }
    }

    public static event MovementRequestedObserver MovementRequested
    {
        add { movementObservers = AddObserver(movementObservers, value); }
        remove { movementObservers = RemoveObserver(movementObservers, value); }
    }

    public static event ContactPairObserver ContactPairReported
    {
        add { contactPairObservers = AddObserver(contactPairObservers, value); }
        remove { contactPairObservers = RemoveObserver(contactPairObservers, value); }
    }

    public static int FixedTick { get; private set; }

    public static void BeginPhysicsFrame()
    {
        FixedTick++;
        InvokeSafely(FixedTick);
    }

    public static void CompletePhysicsPhase(int phase)
    {
        InvokeSafely(FixedTick, phase);
    }

    public static void ResetFixedTick()
    {
        FixedTick = 0;
    }

    public static void NotifyEntityEnabled(BasicEntity entity)
    {
        InvokeSafely(entityEnabledObservers, entity);
    }

    public static void NotifyEntityDisabled(BasicEntity entity)
    {
        InvokeSafely(entityDisabledObservers, entity);
    }

    public static void ReportMotionBuilt(
        BasicEntity entity,
        Vector2 freeVelocity,
        Vector2 integratedVelocityDelta,
        Vector2 motionDelta,
        Vector2 platformDelta,
        Vector2 plannedWorldDelta)
    {
        MotionBuiltObserver[] observers = motionObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](entity, freeVelocity, integratedVelocityDelta, motionDelta, platformDelta, plannedWorldDelta); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    public static void ReportPredictionBuilt(BasicEntity entity, Vector2 center, Vector2 extents)
    {
        PredictionBuiltObserver[] observers = predictionObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](entity, center, extents); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    public static void ReportMovementRequested(
        BasicEntity entity,
        Vector2 solverOffset,
        Vector2 unconstrainedDelta,
        Vector2 requestedDelta,
        Vector2 requestedTarget,
        bool horizontalEnvironmentBlocked)
    {
        MovementRequestedObserver[] observers = movementObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](entity, solverOffset, unconstrainedDelta, requestedDelta, requestedTarget, horizontalEnvironmentBlocked); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    public static void ReportContactPair(
        BasicEntity entityA,
        BasicEntity entityB,
        Vector2 predictedCenterA,
        Vector2 predictedCenterB,
        Vector2 centerDelta,
        bool separateOnX,
        float depth,
        float correctionDepth,
        float closingSpeed,
        bool aAppliedForce,
        bool bAppliedForce,
        float targetSpeedAToB,
        float targetSpeedBToA,
        float weightA,
        float weightB,
        Vector2 offsetA,
        Vector2 offsetB)
    {
        ContactPairObserver[] observers = contactPairObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try
            {
                observers[i](entityA, entityB, predictedCenterA, predictedCenterB, centerDelta,
                    separateOnX, depth, correctionDepth, closingSpeed, aAppliedForce, bAppliedForce,
                    targetSpeedAToB, targetSpeedBToA, weightA, weightB, offsetA, offsetB);
            }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private static void InvokeSafely(Action<BasicEntity>[] observers, BasicEntity entity)
    {
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](entity); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private static void InvokeSafely(int fixedTick)
    {
        Action<int>[] observers = frameObservers;
        for (int i = 0; i < observers.Length; i++)
        {
            try { observers[i](fixedTick); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private static void InvokeSafely(int fixedTick, int phase)
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

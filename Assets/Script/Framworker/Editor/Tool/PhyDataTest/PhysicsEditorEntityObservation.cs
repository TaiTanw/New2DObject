#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器按物理帧和相位采样实体只读出口，并保存一份值快照供窗口和检查工具读取。
/// 职能：编辑器观测数据的采集、跨帧关联和快照组装；不参与物理解算。
/// </summary>
public static class PhysicsEditorEntityObservation
{
    private sealed class EntityRecord
    {
        public EntityPhysicsDebugSnapshot snapshot;
        public bool hasRequestedTarget;
        public Vector2 requestedTarget;
    }

    private static readonly List<BasicEntity> entities = new List<BasicEntity>();
    private static readonly Dictionary<BasicEntity, EntityRecord> records = new Dictionary<BasicEntity, EntityRecord>();
    private static int observerCount;
    private static bool bridgeSubscribed;

    static PhysicsEditorEntityObservation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopAllObservers;
    }

    public static void StartObserving()
    {
        observerCount++;
        if (observerCount != 1) return;

        PhysicsEditorObservation.PhysicsFrameStarted += OnPhysicsFrameStarted;
        PhysicsEditorObservation.PhysicsPhaseCompleted += OnPhysicsPhaseCompleted;
        if (Application.isPlaying)
            StartBridgeObservation();
    }

    public static void StopObserving()
    {
        if (observerCount == 0) return;
        observerCount--;
        if (observerCount != 0) return;

        PhysicsEditorObservation.PhysicsFrameStarted -= OnPhysicsFrameStarted;
        PhysicsEditorObservation.PhysicsPhaseCompleted -= OnPhysicsPhaseCompleted;
        StopBridgeObservation();
        ClearRecords();
    }

    public static EntityPhysicsDebugSnapshot GetSnapshot(BasicEntity entity)
    {
        if (entity == null) return default;
        if (Application.isPlaying)
        {
            if (records.TryGetValue(entity, out EntityRecord record))
                return record.snapshot;
            return new EntityPhysicsDebugSnapshot
            {
                fixedTick = PhysicsEditorObservationBridge.FixedTick,
                phase = -2,
                entityName = entity.name
            };
        }

        // Edit Mode 检查直接读取当前只读出口，不伪造尚未发生的 Play Mode 采样记录。
        return CaptureCurrentState(entity, PhysicsEditorObservationBridge.FixedTick);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (observerCount == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            StartBridgeObservation();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            StopBridgeObservation();
            ClearRecords();
        }
    }

    private static void StartBridgeObservation()
    {
        if (bridgeSubscribed || observerCount == 0 || !Application.isPlaying) return;

        PhysicsEditorObservationBridge.EntityEnabled += RegisterEntity;
        PhysicsEditorObservationBridge.EntityDisabled += UnregisterEntity;
        PhysicsEditorObservationBridge.MotionBuilt += RecordMotionBuilt;
        PhysicsEditorObservationBridge.PredictionBuilt += RecordPredictionBuilt;
        PhysicsEditorObservationBridge.MovementRequested += RecordMovementRequested;
        bridgeSubscribed = true;

        BasicEntity[] activeEntities = UnityEngine.Object.FindObjectsOfType<BasicEntity>();
        for (int i = 0; i < activeEntities.Length; i++)
            RegisterEntity(activeEntities[i]);
    }

    private static void StopBridgeObservation()
    {
        if (!bridgeSubscribed) return;

        PhysicsEditorObservationBridge.EntityEnabled -= RegisterEntity;
        PhysicsEditorObservationBridge.EntityDisabled -= UnregisterEntity;
        PhysicsEditorObservationBridge.MotionBuilt -= RecordMotionBuilt;
        PhysicsEditorObservationBridge.PredictionBuilt -= RecordPredictionBuilt;
        PhysicsEditorObservationBridge.MovementRequested -= RecordMovementRequested;
        bridgeSubscribed = false;
    }

    private static void StopAllObservers()
    {
        if (observerCount > 0)
        {
            PhysicsEditorObservation.PhysicsFrameStarted -= OnPhysicsFrameStarted;
            PhysicsEditorObservation.PhysicsPhaseCompleted -= OnPhysicsPhaseCompleted;
        }
        observerCount = 0;
        StopBridgeObservation();
        ClearRecords();
    }

    private static void RegisterEntity(BasicEntity entity)
    {
        if (entity == null || !entity.isActiveAndEnabled || records.ContainsKey(entity)) return;
        records.Add(entity, new EntityRecord { snapshot = CaptureCurrentState(entity, PhysicsEditorObservationBridge.FixedTick) });
        entities.Add(entity);
    }

    private static void UnregisterEntity(BasicEntity entity)
    {
        if (ReferenceEquals(entity, null) || !records.Remove(entity)) return;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(entities[i], entity))
                entities.RemoveAt(i);
        }
    }

    private static void OnPhysicsFrameStarted(int fixedTick)
    {
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            BasicEntity entity = entities[i];
            if (entity == null)
            {
                records.Remove(entity);
                entities.RemoveAt(i);
                continue;
            }

            EntityRecord record = records[entity];
            Vector2 actualPosition = entity.DebugActualPosition;
            record.snapshot.fixedTick = fixedTick;
            record.snapshot.phase = -1;
            record.snapshot.entityName = entity.name;
            record.snapshot.bodyType = entity.DebugBodyType;
            record.snapshot.actualPosition = actualPosition;
            record.snapshot.rotation = entity.DebugRotation;
            record.snapshot.angularVelocity = entity.DebugAngularVelocity;
            record.snapshot.engineCorrection = record.hasRequestedTarget
                ? actualPosition - record.requestedTarget
                : Vector2.zero;
        }
    }

    private static void OnPhysicsPhaseCompleted(int fixedTick, int phase)
    {
        if (phase != 1 && phase != 2 && phase != 3 && phase != 4 && phase != 5) return;
        for (int i = 0; i < entities.Count; i++)
        {
            BasicEntity entity = entities[i];
            if (entity == null || !records.TryGetValue(entity, out EntityRecord record)) continue;
            record.snapshot.fixedTick = fixedTick;
            record.snapshot.phase = phase;
            SampleStableState(entity, ref record.snapshot);
        }
    }

    private static void RecordMotionBuilt(
        BasicEntity entity,
        Vector2 freeVelocity,
        Vector2 integratedVelocityDelta,
        Vector2 motionDelta,
        Vector2 platformDelta,
        Vector2 plannedWorldDelta)
    {
        if (!records.TryGetValue(entity, out EntityRecord record)) return;
        record.snapshot.planarVelocity = freeVelocity;
        record.snapshot.phase = 3;
        record.snapshot.integratedVelocityDelta = integratedVelocityDelta;
        record.snapshot.motionDelta = motionDelta;
        record.snapshot.platformDelta = platformDelta;
        record.snapshot.plannedWorldDelta = plannedWorldDelta;
    }

    private static void RecordPredictionBuilt(BasicEntity entity, Vector2 center, Vector2 extents)
    {
        if (!records.TryGetValue(entity, out EntityRecord record)) return;
        record.snapshot.predictedCenter = center;
        record.snapshot.predictedExtents = extents;
        record.snapshot.phase = 3;
    }

    private static void RecordMovementRequested(
        BasicEntity entity,
        Vector2 solverOffset,
        Vector2 unconstrainedDelta,
        Vector2 requestedDelta,
        Vector2 requestedTarget,
        bool staticWallClamped)
    {
        if (!records.TryGetValue(entity, out EntityRecord record)) return;
        record.snapshot.solverOffset = solverOffset;
        record.snapshot.unconstrainedDelta = unconstrainedDelta;
        record.snapshot.requestedDelta = requestedDelta;
        record.snapshot.requestedTarget = requestedTarget;
        record.snapshot.staticWallClamped = staticWallClamped;
        record.snapshot.phase = 5;
        record.hasRequestedTarget = true;
        record.requestedTarget = requestedTarget;
    }

    private static EntityPhysicsDebugSnapshot CaptureCurrentState(BasicEntity entity, int fixedTick)
    {
        EntityPhysicsDebugSnapshot snapshot = new EntityPhysicsDebugSnapshot
        {
            fixedTick = fixedTick,
            phase = -1,
            entityName = entity.name,
            bodyType = entity.DebugBodyType,
            actualPosition = entity.DebugActualPosition,
            rotation = entity.DebugRotation,
            angularVelocity = entity.DebugAngularVelocity,
            planarVelocity = entity.DebugPlanarVelocity
        };
        SampleStableState(entity, ref snapshot);
        return snapshot;
    }

    private static void SampleStableState(BasicEntity entity, ref EntityPhysicsDebugSnapshot snapshot)
    {
        snapshot.isGrounded = entity.DebugIsGrounded;
        snapshot.isTopBlocked = entity.DebugIsTopBlocked;
        snapshot.isOnLeftWall = entity.DebugIsOnLeftWall;
        snapshot.isOnRightWall = entity.DebugIsOnRightWall;
        snapshot.groundNormal = entity.DebugGroundNormal;
        snapshot.environmentSourceCount = entity.DebugEnvironmentSourceCount;
        snapshot.movementModifierCount = entity.DebugMovementModifierCount;
        snapshot.stateVelocityCount = entity.DebugStateVelocityCount;
        snapshot.dynamicForceCount = entity.DebugDynamicForceCount;
        snapshot.environmentSlowingMultiplier = entity.DebugEnvironmentSlowingMultiplier;
        snapshot.environmentJumpHeightOffset = entity.DebugEnvironmentJumpHeightOffset;
    }

    private static void ClearRecords()
    {
        records.Clear();
        entities.Clear();
    }
}

/// <summary>
/// 编辑器窗口和检查入口读取的一帧实体值快照，包含各相位采样结果及跨帧位置修正。
/// 职能：编辑器展示数据；与运行时运动状态分离，不允许回写物理结果。
/// </summary>
public struct EntityPhysicsDebugSnapshot
{
    public int fixedTick;
    public int phase;
    public string entityName;
    public RigidbodyType2D bodyType;
    public Vector2 actualPosition;
    public float rotation;
    public float angularVelocity;
    public Vector2 planarVelocity;
    public Vector2 predictedCenter;
    public Vector2 predictedExtents;
    public Vector2 integratedVelocityDelta;
    public Vector2 motionDelta;
    public Vector2 platformDelta;
    public Vector2 plannedWorldDelta;
    public Vector2 solverOffset;
    public Vector2 unconstrainedDelta;
    public Vector2 requestedDelta;
    public Vector2 requestedTarget;
    public Vector2 engineCorrection;
    public bool staticWallClamped;
    public bool isGrounded;
    public bool isTopBlocked;
    public bool isOnLeftWall;
    public bool isOnRightWall;
    public Vector2 groundNormal;
    public int environmentSourceCount;
    public int movementModifierCount;
    public int stateVelocityCount;
    public int dynamicForceCount;
    public float environmentSlowingMultiplier;
    public float environmentJumpHeightOffset;
}
#endif

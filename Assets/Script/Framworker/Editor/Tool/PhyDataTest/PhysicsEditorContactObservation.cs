#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PhyData;

/// <summary>
/// 收集一帧预测接触对的编辑器展示数据，并在相位 4 完成后发布完整批次。
/// 职能：接触观测快照组装与批次生命周期，不参与物理解算。
/// </summary>
[InitializeOnLoad]
public static class PhysicsEditorContactObservation
{
    private static List<ContactPairDebugSnapshot> collectingContacts = new List<ContactPairDebugSnapshot>();
    private static List<ContactPairDebugSnapshot> publishedContacts = new List<ContactPairDebugSnapshot>();
    private static int observerCount;
    private static bool bridgeSubscribed;

    static PhysicsEditorContactObservation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopAllObservers;
    }

    public static IReadOnlyList<ContactPairDebugSnapshot> Contacts => publishedContacts;
    public static int PublishedFixedTick { get; private set; } = -1;

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
        ClearContacts();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (observerCount == 0) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearContacts();
            StartBridgeObservation();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            StopBridgeObservation();
            ClearContacts();
        }
    }

    private static void StartBridgeObservation()
    {
        if (bridgeSubscribed || observerCount == 0 || !Application.isPlaying) return;

        PhysicsEditorObservationBridge.ContactPairReported += RecordContactPair;
        PhysicsEditorObservationBridge.EntityDisabled += RemoveEntityContacts;
        bridgeSubscribed = true;
    }

    private static void StopBridgeObservation()
    {
        if (!bridgeSubscribed) return;

        PhysicsEditorObservationBridge.ContactPairReported -= RecordContactPair;
        PhysicsEditorObservationBridge.EntityDisabled -= RemoveEntityContacts;
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
        ClearContacts();
    }

    private static void OnPhysicsFrameStarted(int fixedTick)
    {
        // Drop only an incomplete batch; the last fully published batch stays visible until phase 4.
        collectingContacts.Clear();
    }

    private static void OnPhysicsPhaseCompleted(int fixedTick, int phase)
    {
        if (phase != 4) return;

        List<ContactPairDebugSnapshot> oldPublished = publishedContacts;
        publishedContacts = collectingContacts;
        collectingContacts = oldPublished;
        collectingContacts.Clear();
        PublishedFixedTick = fixedTick;
    }

    private static void RemoveEntityContacts(BasicEntity entity)
    {
        RemoveEntityContacts(collectingContacts, entity);
        RemoveEntityContacts(publishedContacts, entity);
    }

    private static void RemoveEntityContacts(List<ContactPairDebugSnapshot> contacts, BasicEntity entity)
    {
        for (int i = contacts.Count - 1; i >= 0; i--)
            if (ReferenceEquals(contacts[i].entityA, entity) || ReferenceEquals(contacts[i].entityB, entity))
                contacts.RemoveAt(i);
    }

    private static void RecordContactPair(
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
        Vector2 normalA = separateOnX
            ? new Vector2(centerDelta.x >= 0f ? 1f : -1f, 0f)
            : new Vector2(0f, centerDelta.y >= 0f ? 1f : -1f);

        collectingContacts.Add(new ContactPairDebugSnapshot
        {
            entityA = entityA,
            entityB = entityB,
            predictedCenterA = predictedCenterA,
            predictedCenterB = predictedCenterB,
            separateOnX = separateOnX,
            normalA = normalA,
            depth = depth,
            correctionDepth = correctionDepth,
            closingSpeed = closingSpeed,
            aAppliedForce = aAppliedForce,
            bAppliedForce = bAppliedForce,
            targetSpeedAToB = targetSpeedAToB,
            targetSpeedBToA = targetSpeedBToA,
            weightA = weightA,
            weightB = weightB,
            offsetA = offsetA,
            offsetB = offsetB
        });
    }

    private static void ClearContacts()
    {
        collectingContacts.Clear();
        publishedContacts.Clear();
        PublishedFixedTick = -1;
    }
}

/// <summary>仅供编辑器窗口展示的预测接触对快照。</summary>
namespace PhyData
{
    public struct ContactPairDebugSnapshot
    {
        public BasicEntity entityA;
        public BasicEntity entityB;
        public Vector2 predictedCenterA;
        public Vector2 predictedCenterB;
        public bool separateOnX;
        public Vector2 normalA;
        public float depth;
        public float correctionDepth;
        public float closingSpeed;
        public bool aAppliedForce;
        public bool bAppliedForce;
        public float targetSpeedAToB;
        public float targetSpeedBToA;
        public float weightA;
        public float weightB;
        public Vector2 offsetA;
        public Vector2 offsetB;
    }
}
#endif

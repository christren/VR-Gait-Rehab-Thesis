using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandFootCorrelator : MonoBehaviour
{
    [System.Serializable]
    public class AxisMapping
    {
        public float scale = 1f;
        public bool invert = false;
        public float Apply(float delta)
        {
            float d = delta * scale;
            return invert ? -d : d;
        }
    }

    [Header("References")]
    public Transform head;
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Per-Axis Mapping (applies to both hands)")]
    public AxisMapping xAxis = new AxisMapping();
    public AxisMapping yAxis = new AxisMapping();
    public AxisMapping zAxis = new AxisMapping();

    [Header("Side Assignment")]
    public bool crossMapSides = false;

    [Header("Reachable Range Safety")]
    public float maxReachMeters = 0.6f;

    private XRHandSubsystem handSubsystem;
    private Vector3 leftHandRestPos, rightHandRestPos;
    private Vector3 leftFootRestPos, rightFootRestPos;
    private bool calibrated;

    void OnEnable()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;

        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            handSubsystem.updatedHands += OnHandsUpdated;
        }
        else
        {
            UnityEngine.Debug.LogWarning("HandFootCorrelator: No XRHandSubsystem found.");
        }

        if (leftFootTarget != null) leftFootRestPos = leftFootTarget.localPosition;
        if (rightFootTarget != null) rightFootRestPos = rightFootTarget.localPosition;
    }

    void OnDisable()
    {
        if (handSubsystem != null)
            handSubsystem.updatedHands -= OnHandsUpdated;
    }

    void OnHandsUpdated(XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType type)
    {
        if (head == null) return;

        var leftHand = subsystem.leftHand;
        var rightHand = subsystem.rightHand;

        Vector3 leftPos = default;
        Vector3 rightPos = default;

        bool gotLeft = leftHand.isTracked && TryGetPalmPosition(leftHand, out leftPos);
        bool gotRight = rightHand.isTracked && TryGetPalmPosition(rightHand, out rightPos);

        if (gotLeft) leftPos = ToHeadFrame(leftPos);
        if (gotRight) rightPos = ToHeadFrame(rightPos);

        if (!calibrated && gotLeft && gotRight)
        {
            leftHandRestPos = leftPos;
            rightHandRestPos = rightPos;
            calibrated = true;
            return;
        }

        if (!calibrated) return;

        if (gotLeft) ApplyHandToFoot(leftPos, leftHandRestPos,
            crossMapSides ? rightFootTarget : leftFootTarget,
            crossMapSides ? rightFootRestPos : leftFootRestPos);

        if (gotRight) ApplyHandToFoot(rightPos, rightHandRestPos,
            crossMapSides ? leftFootTarget : rightFootTarget,
            crossMapSides ? leftFootRestPos : rightFootRestPos);
    }

    bool TryGetPalmPosition(XRHand hand, out Vector3 pos)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (palm.TryGetPose(out Pose pose))
        {
            pos = pose.position;
            return true;
        }
        pos = default;
        return false;
    }

    Vector3 ToHeadFrame(Vector3 trackingPos)
    {
        Vector3 world = transform.TransformPoint(trackingPos);
        Vector3 flat = world - head.position;
        flat.y = 0f;
        Quaternion yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        Vector3 rel = Quaternion.Inverse(yaw) * flat;
        rel.y = world.y;
        return rel;
    }

    void ApplyHandToFoot(Vector3 handPos, Vector3 restHandPos,
        Transform footTarget, Vector3 footRestPos)
    {
        if (footTarget == null) return;

        Vector3 delta = handPos - restHandPos;
        Vector3 mapped = new Vector3(
            xAxis.Apply(delta.x),
            yAxis.Apply(delta.y),
            zAxis.Apply(delta.z));

        mapped = Vector3.ClampMagnitude(mapped, maxReachMeters);
        footTarget.localPosition = footRestPos + mapped;
    }

    public void Recalibrate()
    {
        calibrated = false;
        if (leftFootTarget != null) leftFootTarget.localPosition = leftFootRestPos;
        if (rightFootTarget != null) rightFootTarget.localPosition = rightFootRestPos;
    }
}
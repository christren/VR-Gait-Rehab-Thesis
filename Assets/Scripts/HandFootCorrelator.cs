using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Continuously maps hand displacement (from a calibrated rest pose) onto a
/// foot IK target's position, per axis, with independent scale and invert
/// controls. Right hand drives right foot target, left hand drives left foot
/// target (matching sides), unless swapped in the Inspector.
///
/// This replaces discrete "step trigger" locomotion (ArmSwingGaitDetector /
/// GaitLocomotion) with smooth, proportional, continuous limb correlation:
/// hand moves 10cm forward -> foot target moves 10cm forward (after scale).
///
/// Requires: com.unity.xr.hands package + OpenXR Hand Tracking Subsystem
/// enabled (see Project Settings > XR Plug-in Management > OpenXR).
/// Requires: com.unity.animation.rigging Two Bone IK Constraint targets
/// assigned in leftFootTarget / rightFootTarget.
/// </summary>
public class HandFootCorrelator : MonoBehaviour
{
    [System.Serializable]
    public class AxisMapping
    {
        [Tooltip("Multiplier applied to hand displacement before applying to the foot target on this axis.")]
        public float scale = 1f;
        [Tooltip("Flip the direction of movement on this axis.")]
        public bool invert = false;

        public float Apply(float delta)
        {
            float d = delta * scale;
            return invert ? -d : d;
        }
    }

    [Header("Foot IK Targets (Two Bone IK Constraint targets)")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;

    [Header("Per-Axis Mapping (applies to both hands)")]
    public AxisMapping xAxis = new AxisMapping();
    public AxisMapping yAxis = new AxisMapping();
    public AxisMapping zAxis = new AxisMapping();

    [Header("Side Assignment")]
    [Tooltip("If true, left hand drives the right foot target and vice versa.")]
    public bool crossMapSides = false;

    [Header("Reachable Range Safety")]
    [Tooltip("Foot target cannot move further than this from its rest position on any axis, to keep the IK solver stable.")]
    public float maxReachMeters = 0.6f;

    private XRHandSubsystem handSubsystem;
    private Vector3 leftHandRestPos, rightHandRestPos;
    private Vector3 leftFootRestPos, rightFootRestPos;
    private bool calibrated;

    void OnEnable()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            handSubsystem.updatedHands += OnHandsUpdated;
        }
        else
        {
            Debug.LogWarning("HandFootCorrelator: No XRHandSubsystem found. " +
                "Enable OpenXR Hand Tracking Subsystem in Project Settings.");
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
        XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
        XRHandSubsystem.UpdateType updateType)
    {
        var leftHand = subsystem.leftHand;
        var rightHand = subsystem.rightHand;

        Vector3 leftPos = default;
        Vector3 rightPos = default;

        bool gotLeft = leftHand.isTracked && TryGetPalmPosition(leftHand, out leftPos);
        bool gotRight = rightHand.isTracked && TryGetPalmPosition(rightHand, out rightPos);

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

    bool TryGetPalmPosition(XRHand hand, out Vector3 worldPos)
    {
        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (palm.TryGetPose(out Pose pose))
        {
            worldPos = pose.position;
            return true;
        }
        worldPos = default;
        return false;
    }

    void ApplyHandToFoot(Vector3 currentHandPos, Vector3 restHandPos,
        Transform footTarget, Vector3 footRestPos)
    {
        if (footTarget == null) return;

        // Displacement in world space, converted into this object's local
        // axes so X/Y/Z mapping is relative to the player's own orientation,
        // not world space (important if the player rig can rotate).
        Vector3 worldDelta = currentHandPos - restHandPos;
        Vector3 localDelta = transform.InverseTransformDirection(worldDelta);

        Vector3 mapped = new Vector3(
            xAxis.Apply(localDelta.x),
            yAxis.Apply(localDelta.y),
            zAxis.Apply(localDelta.z)
        );

        mapped = Vector3.ClampMagnitude(mapped, maxReachMeters);

        footTarget.localPosition = footRestPos + mapped;
    }

    /// <summary>
    /// Re-baseline both hand rest positions and foot rest positions.
    /// Call when the patient settles into a comfortable seated/starting pose.
    /// </summary>
    public void Recalibrate()
    {
        calibrated = false;
        if (leftFootTarget != null) leftFootRestPos = leftFootTarget.localPosition;
        if (rightFootTarget != null) rightFootRestPos = rightFootTarget.localPosition;
    }
}

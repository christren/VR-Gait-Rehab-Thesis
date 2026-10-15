using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.Hands;

public class PinchRecalibrate : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float cooldownSeconds = 2f;

    [Header("Pinch detection")]
    [Tooltip("Thumb tip to index tip distance (metres) that counts as pinched.")]
    [SerializeField] private float pinchDistance = 0.025f;

    [Header("Feedback (optional)")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip doneClip;

    [Header("Debug (read-only)")]
    [SerializeField] private float holdProgress01;
    [SerializeField] private bool leftPinched;
    [SerializeField] private bool rightPinched;

    private HandFootCorrelator correlator;
    private FootLocomotion locomotion;
    private XRHandSubsystem handSubsystem;
    private float holdTimer;
    private float cooldownEnd;
    private bool startClipPlayed;

    void Start()
    {
        correlator = FindFirstObjectByType<HandFootCorrelator>();
        locomotion = FindFirstObjectByType<FootLocomotion>();
    }

    void Update()
    {
        if (handSubsystem == null)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0) return;
            handSubsystem = subsystems[0];
        }

        leftPinched = IsPinched(handSubsystem.leftHand);
        rightPinched = IsPinched(handSubsystem.rightHand);

        bool both = leftPinched && rightPinched && Time.time >= cooldownEnd;

        if (both)
        {
            if (!startClipPlayed)
            {
                Play(startClip);
                startClipPlayed = true;
            }

            holdTimer += Time.unscaledDeltaTime;
            holdProgress01 = Mathf.Clamp01(holdTimer / holdSeconds);

            if (holdTimer >= holdSeconds)
            {
                DoRecalibrate();
                holdTimer = 0f;
                holdProgress01 = 0f;
                startClipPlayed = false;
                cooldownEnd = Time.time + cooldownSeconds;
            }
        }
        else
        {
            holdTimer = 0f;
            holdProgress01 = 0f;
            startClipPlayed = false;
        }
    }

    bool IsPinched(XRHand hand)
    {
        if (!hand.isTracked) return false;

        var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
        var index = hand.GetJoint(XRHandJointID.IndexTip);

        if (thumb.TryGetPose(out Pose thumbPose) && index.TryGetPose(out Pose indexPose))
            return Vector3.Distance(thumbPose.position, indexPose.position) < pinchDistance;

        return false;
    }

    void DoRecalibrate()
    {
        GlanceTurn turn = FindFirstObjectByType<GlanceTurn>();

        if (turn != null)
            turn.RecalibrateNeutral();

        if (correlator != null)
            correlator.Recalibrate();

        if (locomotion != null)
            locomotion.Recalibrate();

        Play(doneClip);

        UnityEngine.Debug.Log(
            "PinchRecalibrate: hands, feet and glance neutral reset.");
    }

    void Play(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
    }
}
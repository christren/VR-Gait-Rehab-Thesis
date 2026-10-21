using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

public class PinchRecalibrate : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float cooldownSeconds = 2f;

    [Tooltip("Total continuous pinch time before reloading the scene.")]
    [SerializeField] private float resetHoldSeconds = 15f;

    [Header("Pinch detection")]
    [SerializeField] private float pinchDistance = 0.025f;

    [Header("Feedback (optional)")]
    [SerializeField] private AudioClip startClip;
    [SerializeField] private AudioClip doneClip;

    [Header("Debug - read only")]
    [SerializeField] private float holdProgress01;
    [SerializeField] private float resetProgress01;
    [SerializeField] private bool leftPinched;
    [SerializeField] private bool rightPinched;

    private HandFootCorrelator correlator;
    private FootLocomotion locomotion;
    private GlanceTurn turning;
    private XRHandSubsystem handSubsystem;

    private readonly List<XRHandSubsystem> subsystems =
        new List<XRHandSubsystem>();

    private float holdTimer;
    private float cooldownEnd;

    private bool startClipPlayed;
    private bool calibratedThisHold;
    private bool waitForRelease;
    private bool resetRequested;

    void OnEnable()
    {
        ClearHold();
        waitForRelease = true;
        resetRequested = false;
    }

    void Start()
    {
        correlator = FindFirstObjectByType<HandFootCorrelator>();
        locomotion = FindFirstObjectByType<FootLocomotion>();
        turning = FindFirstObjectByType<GlanceTurn>();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            return;

        ClearHold();
        waitForRelease = true;
    }

    void Update()
    {
        if (resetRequested)
            return;

        FindHandSubsystem();

        bool running =
            handSubsystem != null && handSubsystem.running;

        leftPinched =
            running && IsPinched(handSubsystem.leftHand);

        rightPinched =
            running && IsPinched(handSubsystem.rightHand);

        // Prevent an already-held pinch from triggering
        // immediately after enabling this component.
        if (waitForRelease)
        {
            if (!leftPinched && !rightPinched)
                waitForRelease = false;

            return;
        }

        bool bothPinched = leftPinched && rightPinched;

        if (!bothPinched)
        {
            if (calibratedThisHold)
            {
                cooldownEnd =
                    Time.unscaledTime + cooldownSeconds;
            }

            ClearHold();
            return;
        }

        if (Time.unscaledTime < cooldownEnd)
            return;

        if (!startClipPlayed)
        {
            Play(startClip);
            startClipPlayed = true;
        }

        holdTimer += Time.unscaledDeltaTime;

        float calibrationTime = Mathf.Max(0.1f, holdSeconds);

        float resetTime = Mathf.Max(
            resetHoldSeconds, calibrationTime + 1f);

        holdProgress01 =
            Mathf.Clamp01(holdTimer / calibrationTime);

        resetProgress01 =
            Mathf.Clamp01(holdTimer / resetTime);

        if (!calibratedThisHold &&
            holdTimer >= calibrationTime)
        {
            DoRecalibrate();
            calibratedThisHold = true;
        }

        // Do not reset holdTimer after the normal calibration.
        // The same continuous hold can reach the reset duration.
        if (holdTimer >= resetTime)
            ReturnToStart();
    }

    void FindHandSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running)
            return;

        handSubsystem = null;

        SubsystemManager.GetSubsystems(subsystems);

        foreach (XRHandSubsystem candidate in subsystems)
        {
            if (!candidate.running)
                continue;

            handSubsystem = candidate;
            break;
        }
    }

    bool IsPinched(XRHand hand)
    {
        if (!hand.isTracked)
            return false;

        bool gotThumb =
            hand.GetJoint(XRHandJointID.ThumbTip)
                .TryGetPose(out Pose thumbPose);

        bool gotIndex =
            hand.GetJoint(XRHandJointID.IndexTip)
                .TryGetPose(out Pose indexPose);

        return gotThumb &&
            gotIndex &&
            Vector3.Distance(
                thumbPose.position,
                indexPose.position) < pinchDistance;
    }

    void DoRecalibrate()
    {
        if (turning != null)
            turning.RecalibrateNeutral();

        if (correlator != null)
            correlator.Recalibrate();

        if (locomotion != null)
            locomotion.Recalibrate();

        Play(doneClip);

        UnityEngine.Debug.Log(
            "PinchRecalibrate: neutral positions reset.");
    }

    void ReturnToStart()
    {
        int sceneIndex =
            SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex < 0)
        {
            UnityEngine.Debug.LogError(
                "Return to start failed: add this scene " +
                "to the Build Profiles scene list.");

            ClearHold();
            waitForRelease = true;
            return;
        }

        resetRequested = true;

        if (locomotion != null)
            locomotion.enabled = false;

        if (turning != null)
            turning.enabled = false;

        if (correlator != null)
            correlator.enabled = false;

        UnityEngine.Debug.Log(
            "PinchRecalibrate: returning to start/menu.");

        SceneManager.LoadSceneAsync(
            sceneIndex, LoadSceneMode.Single);
    }

    void ClearHold()
    {
        holdTimer = 0f;
        holdProgress01 = 0f;
        resetProgress01 = 0f;
        startClipPlayed = false;
        calibratedThisHold = false;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(
                clip, Camera.main.transform.position);
        }
    }
}
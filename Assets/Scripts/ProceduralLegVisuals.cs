using UnityEngine;

public class ProceduralLegVisuals : MonoBehaviour
{
    [Header("Drag GaitSystem here")]
    [SerializeField] private ArmSwingGaitDetector gaitDetector;

    [Header("Drag left leg pivots here")]
    [SerializeField] private Transform leftThighPivot;
    [SerializeField] private Transform leftKneePivot;

    [Header("Drag right leg pivots here")]
    [SerializeField] private Transform rightThighPivot;
    [SerializeField] private Transform rightKneePivot;

    [Header("Step animation")]
    [SerializeField] private float stepDuration = 0.20f;
    [SerializeField] private float thighSwingAngle = 20f;
    [SerializeField] private float kneeBendAngle = 28f;

    private bool leftStepping;
    private bool rightStepping;
    private float leftStepStartTime;
    private float rightStepStartTime;

    void OnEnable()
    {
        gaitDetector.HeelStrike += StartLegStep;
    }

    void OnDisable()
    {
        gaitDetector.HeelStrike -= StartLegStep;
    }

    void Update()
    {
        AnimateLeg(
            leftThighPivot,
            leftKneePivot,
            leftStepping,
            leftStepStartTime,
            ref leftStepping);

        AnimateLeg(
            rightThighPivot,
            rightKneePivot,
            rightStepping,
            rightStepStartTime,
            ref rightStepping);
    }

    private void StartLegStep(bool leftFoot)
    {
        if (leftFoot)
        {
            leftStepStartTime = Time.unscaledTime;
            leftStepping = true;
        }
        else
        {
            rightStepStartTime = Time.unscaledTime;
            rightStepping = true;
        }
    }

    private void AnimateLeg(
        Transform thighPivot,
        Transform kneePivot,
        bool isStepping,
        float stepStartTime,
        ref bool steppingState)
    {
        if (!isStepping)
            return;

        float progress =
            Mathf.Clamp01(
                (Time.unscaledTime - stepStartTime) / stepDuration);

        float swingPhase = Mathf.Sin(progress * Mathf.PI);

        float thighAngle = swingPhase * thighSwingAngle;
        float kneeAngle = swingPhase * kneeBendAngle;

        thighPivot.localRotation =
            Quaternion.Euler(thighAngle, 0f, 0f);

        kneePivot.localRotation =
            Quaternion.Euler(kneeAngle, 0f, 0f);

        if (progress >= 1f)
        {
            thighPivot.localRotation = Quaternion.identity;
            kneePivot.localRotation = Quaternion.identity;
            steppingState = false;
        }
    }
}
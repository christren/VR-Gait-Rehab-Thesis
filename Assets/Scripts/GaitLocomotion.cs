using System.Diagnostics;
using UnityEngine;

[RequireComponent(typeof(ArmSwingGaitDetector))]
public class GaitLocomotion : MonoBehaviour
{
    [Header("Drag the top-level XR Origin here")]
    [SerializeField] private Transform xrOrigin;

    [Header("Per-step movement")]
    [SerializeField] private float metresPerStep = 0.35f;
    [SerializeField] private float movementDuration = 0.20f;

    private ArmSwingGaitDetector gaitDetector;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float moveStartTime;
    private bool isMoving;

    void Awake()
    {
        gaitDetector = GetComponent<ArmSwingGaitDetector>();
    }

    void OnEnable()
    {
        gaitDetector.HeelStrike += BeginStepMovement;
    }

    void OnDisable()
    {
        gaitDetector.HeelStrike -= BeginStepMovement;
    }

    void Update()
    {
        if (!isMoving)
            return;

        float rawProgress =
            (Time.unscaledTime - moveStartTime) / movementDuration;

        float progress = Mathf.Clamp01(rawProgress);

        progress = progress * progress * (3f - 2f * progress);

        xrOrigin.position =
            Vector3.Lerp(startPosition, targetPosition, progress);

        if (progress >= 1f)
            isMoving = false;
    }

    private void BeginStepMovement(bool leftFoot)
    {
        if (xrOrigin == null)
        {
            UnityEngine.Debug.LogError("GaitLocomotion: Assign XR Origin in the Inspector.");
            return;
        }

        startPosition = xrOrigin.position;

        Vector3 flatForward = xrOrigin.forward;
        flatForward.y = 0f;
        flatForward.Normalize();

        targetPosition =
            startPosition + flatForward * metresPerStep;

        moveStartTime = Time.unscaledTime;
        isMoving = true;

        string footName = leftFoot ? "LEFT" : "RIGHT";
        UnityEngine.Debug.Log(footName + " step: moving forward.");
    }
}
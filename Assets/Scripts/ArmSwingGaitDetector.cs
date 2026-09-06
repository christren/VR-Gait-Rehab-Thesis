using System;
using System.Diagnostics;
using UnityEngine;

public class ArmSwingGaitDetector : MonoBehaviour
{
    [Header("Drag these from the Hierarchy")]
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;
    [SerializeField] private Transform referenceFrame;

    [Header("Sensitivity")]
    [SerializeField] private float forwardVelocityThreshold = 0.18f;
    [SerializeField] private float minimumTimeBetweenSteps = 0.30f;
    [SerializeField] private bool useIpsilateralMapping = true;

    public float LeftForwardVelocity { get; private set; }
    public float RightForwardVelocity { get; private set; }
    public bool LastStepWasLeftFoot { get; private set; }

    public event Action<bool> HeelStrike;

    private Vector3 previousLeftLocalPosition;
    private Vector3 previousRightLocalPosition;
    private float previousLeftVelocity;
    private float previousRightVelocity;
    private float lastStepTime = -10f;
    private bool expectLeftArm = true;

    void Start()
    {
        if (leftController == null || rightController == null || referenceFrame == null)
        {
            UnityEngine.Debug.LogError("ArmSwingGaitDetector: Assign both controllers and XR Origin in the Inspector.");
            enabled = false;
            return;
        }

        previousLeftLocalPosition = referenceFrame.InverseTransformPoint(leftController.position);
        previousRightLocalPosition = referenceFrame.InverseTransformPoint(rightController.position);
    }

    void Update()
    {
        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        Vector3 leftLocalPosition = referenceFrame.InverseTransformPoint(leftController.position);
        Vector3 rightLocalPosition = referenceFrame.InverseTransformPoint(rightController.position);

        LeftForwardVelocity =
            (leftLocalPosition.z - previousLeftLocalPosition.z) / deltaTime;

        RightForwardVelocity =
            (rightLocalPosition.z - previousRightLocalPosition.z) / deltaTime;

        bool leftForwardSwingEnded =
            previousLeftVelocity > forwardVelocityThreshold &&
            LeftForwardVelocity <= 0f;

        bool rightForwardSwingEnded =
            previousRightVelocity > forwardVelocityThreshold &&
            RightForwardVelocity <= 0f;

        bool enoughTimePassed =
            Time.unscaledTime - lastStepTime >= minimumTimeBetweenSteps;

        if (enoughTimePassed)
        {
            if (expectLeftArm && leftForwardSwingEnded)
            {
                RegisterStep(true);
            }
            else if (!expectLeftArm && rightForwardSwingEnded)
            {
                RegisterStep(false);
            }
        }

        previousLeftVelocity = LeftForwardVelocity;
        previousRightVelocity = RightForwardVelocity;
        previousLeftLocalPosition = leftLocalPosition;
        previousRightLocalPosition = rightLocalPosition;
    }

    private void RegisterStep(bool wasLeftArm)
    {
        bool virtualLeftFoot = useIpsilateralMapping
            ? wasLeftArm
            : !wasLeftArm;

        LastStepWasLeftFoot = virtualLeftFoot;
        lastStepTime = Time.unscaledTime;
        expectLeftArm = !wasLeftArm;

        string footName = virtualLeftFoot ? "LEFT" : "RIGHT";
        UnityEngine.Debug.Log(footName + " virtual heel strike at " + Time.unscaledTime.ToString("F3"));

        HeelStrike?.Invoke(virtualLeftFoot);
    }
}
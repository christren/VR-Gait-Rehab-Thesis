using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(100)]
public class GlanceTurn : MonoBehaviour
{
    [Header("References")]
    public Transform xrOrigin;
    public Transform trackingSpace;
    public Transform head;
    public Transform navigationFrame;

    [Header("Glance detection")]
    public bool turningEnabled = true;

    [Min(1f)]
    public float triggerAngle = 15f;

    [Min(0f)]
    public float glanceHoldSeconds = 0.3f;

    [Min(0f)]
    public float neutralAngle = 7f;

    [Min(0f)]
    public float neutralHoldSeconds = 0.15f;

    [Min(1f)]
    public float maxTurnPerGesture = 30f;

    [Header("Virtual turning")]
    [Min(1f)]
    public float maxTurnSpeed = 20f;

    [Min(0.1f)]
    public float arrivalResponsiveness = 4f;

    public bool pauseWalkingDuringTurn = true;

    [Header("Debug")]
    [SerializeField] private float headYaw;
    [SerializeField] private float remainingTurn;
    [SerializeField] private bool waitingForHeadReturn;

    private Vector3 neutralForward;
    private bool initialized;
    private bool armed;
    private float glanceTimer;
    private float neutralTimer;
    private float glanceSign;
    private float requestedTurn;

    public bool IsTurning => Mathf.Abs(remainingTurn) > 0.05f;

    public bool BlockWalking =>
        !initialized ||
        !HeadTracked ||
        (turningEnabled && pauseWalkingDuringTurn && IsTurning);

    public bool HeadTracked
    {
        get
        {
            InputDevice device =
                InputDevices.GetDeviceAtXRNode(XRNode.Head);

            return device.isValid &&
                device.TryGetFeatureValue(
                    CommonUsages.isTracked, out bool tracked) &&
                tracked;
        }
    }

    void OnEnable()
    {
        initialized = false;
        CancelTurn();
    }

    void OnDisable()
    {
        CancelTurn();
    }

    void CancelTurn()
    {
        remainingTurn = 0f;
        requestedTurn = 0f;
        waitingForHeadReturn = false;
        armed = false;
        glanceTimer = 0f;
        neutralTimer = 0f;
        glanceSign = 0f;
    }

    bool TryHeadForward(out Vector3 forward)
    {
        forward = Vector3.zero;

        if (head == null || trackingSpace == null)
            return false;

        forward = trackingSpace.InverseTransformDirection(
            head.forward);

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
            return false;

        forward.Normalize();
        return true;
    }

    public void RecalibrateNeutral()
    {
        if (xrOrigin == null ||
            navigationFrame == null ||
            !HeadTracked ||
            !TryHeadForward(out Vector3 forward))
            return;

        neutralForward = forward;

        Vector3 worldForward =
            Vector3.ProjectOnPlane(head.forward, Vector3.up);

        navigationFrame.rotation = Quaternion.LookRotation(
            worldForward.normalized, Vector3.up);

        CancelTurn();
        initialized = true;
    }

    void LateUpdate()
    {
        if (xrOrigin == null ||
            navigationFrame == null ||
            head == null ||
            trackingSpace == null)
            return;

        if (!HeadTracked)
        {
            CancelTurn();
            return;
        }

        if (!initialized)
        {
            RecalibrateNeutral();
            return;
        }

        if (!turningEnabled)
        {
            CancelTurn();
            return;
        }

        if (!TryHeadForward(out Vector3 forward))
        {
            glanceTimer = 0f;
            neutralTimer = 0f;
            return;
        }

        headYaw = Vector3.SignedAngle(
            neutralForward, forward, Vector3.up);

        float dt = Time.deltaTime;

        if (IsTurning)
        {
            float speed = Mathf.Min(
                maxTurnSpeed,
                Mathf.Abs(remainingTurn) *
                arrivalResponsiveness);

            float step = Mathf.Sign(remainingTurn) *
                Mathf.Min(Mathf.Abs(remainingTurn), speed * dt);

            xrOrigin.RotateAround(
                head.position, Vector3.up, step);

            remainingTurn -= step;

            if (!IsTurning)
                remainingTurn = 0f;

            return;
        }

        bool lookingNeutral =
            Mathf.Abs(headYaw) <= neutralAngle;

        if (waitingForHeadReturn)
        {
            neutralTimer = lookingNeutral
                ? neutralTimer + dt
                : 0f;

            if (neutralTimer >= neutralHoldSeconds)
            {
                remainingTurn = requestedTurn;
                waitingForHeadReturn = false;
                neutralTimer = 0f;
            }

            return;
        }

        if (!armed)
        {
            neutralTimer = lookingNeutral
                ? neutralTimer + dt
                : 0f;

            if (neutralTimer >= neutralHoldSeconds)
            {
                armed = true;
                neutralTimer = 0f;
            }

            return;
        }

        if (Mathf.Abs(headYaw) < triggerAngle)
        {
            glanceTimer = 0f;
            glanceSign = 0f;
            return;
        }

        float currentSign = Mathf.Sign(headYaw);

        if (currentSign != glanceSign)
        {
            glanceTimer = 0f;
            glanceSign = currentSign;
        }

        glanceTimer += dt;

        if (glanceTimer >= glanceHoldSeconds)
        {
            requestedTurn = Mathf.Clamp(
                headYaw,
                -maxTurnPerGesture,
                maxTurnPerGesture);

            waitingForHeadReturn = true;
            armed = false;
            glanceTimer = 0f;
            neutralTimer = 0f;
        }
    }
}
/* Different old turn method
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
*/

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

    [Header("Lean steering")]
    public bool turningEnabled = true;

    [Tooltip("Sideways head movement before turning begins, in metres.")]
    [Min(0f)]
    public float deadZoneMeters = 0.03f;

    [Tooltip("Sideways movement that produces maximum turn speed.")]
    [Min(0.001f)]
    public float fullLeanMeters = 0.08f;

    [Tooltip("Maximum virtual turning speed in degrees per second.")]
    [Min(0f)]
    public float maxTurnSpeed = 20f;

    [Tooltip("Small filter on the lean input. Zero disables filtering.")]
    [Min(0f)]
    public float leanSmoothTime = 0.06f;

    public bool invertTurn;

    [Header("Walking")]
    public bool pauseWalkingDuringTurn = false;

    [Header("Debug - read only")]
    [SerializeField] private float lateralLeanMeters;
    [SerializeField] private float filteredLeanMeters;
    [SerializeField] private float currentTurnSpeed;
    [SerializeField] private bool waitingForNeutral;

    private Vector3 neutralHeadPosition;
    private Vector3 neutralRight;
    private bool initialized;

    public bool IsTurning =>
        Mathf.Abs(currentTurnSpeed) > 0.05f;

    public bool BlockWalking =>
        isActiveAndEnabled &&
        (!ReferencesValid ||
         !initialized ||
         !HeadTracked ||
         waitingForNeutral ||
         (turningEnabled &&
          pauseWalkingDuringTurn &&
          IsTurning));

    private bool ReferencesValid =>
        xrOrigin != null &&
        trackingSpace != null &&
        head != null &&
        navigationFrame != null;

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
        waitingForNeutral = false;
        StopTurning();
    }

    void OnDisable()
    {
        StopTurning();
    }

    void StopTurning()
    {
        filteredLeanMeters = 0f;
        currentTurnSpeed = 0f;
    }

    public void RecalibrateNeutral()
    {
        if (!ReferencesValid || !HeadTracked)
            return;

        Vector3 worldForward = Vector3.ProjectOnPlane(
            head.forward, Vector3.up);

        if (worldForward.sqrMagnitude < 0.01f)
            return;

        worldForward.Normalize();

        // Preserve the existing calibration behaviour:
        // looking ahead defines the initial travel heading.
        navigationFrame.rotation = Quaternion.LookRotation(
            worldForward, Vector3.up);

        neutralHeadPosition =
            trackingSpace.InverseTransformPoint(head.position);

        Vector3 localForward =
            trackingSpace.InverseTransformDirection(worldForward);

        localForward.y = 0f;
        localForward.Normalize();

        neutralRight = Vector3.Cross(
            Vector3.up, localForward).normalized;

        lateralLeanMeters = 0f;
        waitingForNeutral = false;
        initialized = true;

        StopTurning();
    }

    void LateUpdate()
    {
        currentTurnSpeed = 0f;

        if (!ReferencesValid)
        {
            StopTurning();
            return;
        }

        if (!HeadTracked)
        {
            StopTurning();

            if (initialized)
                waitingForNeutral = true;

            return;
        }

        if (!initialized)
        {
            RecalibrateNeutral();
            return;
        }

        Vector3 currentHeadPosition =
            trackingSpace.InverseTransformPoint(head.position);

        Vector3 displacement =
            currentHeadPosition - neutralHeadPosition;

        lateralLeanMeters = Vector3.Dot(
            displacement, neutralRight);

        float deadZone = Mathf.Max(0f, deadZoneMeters);

        if (waitingForNeutral)
        {
            StopTurning();

            if (Mathf.Abs(lateralLeanMeters) <= deadZone)
                waitingForNeutral = false;

            return;
        }

        if (!turningEnabled)
        {
            StopTurning();
            return;
        }

        // Stop immediately when the user returns to neutral.
        if (Mathf.Abs(lateralLeanMeters) <= deadZone)
        {
            StopTurning();
            return;
        }

        // Avoid lingering briefly in the old turn direction.
        if (filteredLeanMeters * lateralLeanMeters < 0f)
            filteredLeanMeters = 0f;

        float blend = leanSmoothTime <= 0f
            ? 1f
            : 1f - Mathf.Exp(-Time.deltaTime / leanSmoothTime);

        filteredLeanMeters = Mathf.Lerp(
            filteredLeanMeters, lateralLeanMeters, blend);

        float magnitude = Mathf.Abs(filteredLeanMeters);

        if (magnitude <= deadZone)
            return;

        float fullLean = Mathf.Max(
            fullLeanMeters, deadZone + 0.001f);

        float turnAmount = Mathf.Clamp01(
            (magnitude - deadZone) / (fullLean - deadZone));

        float direction = Mathf.Sign(filteredLeanMeters);

        if (invertTurn)
            direction = -direction;

        currentTurnSpeed =
            direction * turnAmount * Mathf.Max(0f, maxTurnSpeed);

        xrOrigin.RotateAround(
            head.position,
            Vector3.up,
            currentTurnSpeed * Time.deltaTime);
    }
}
/* Version prior to 14.10.26 (backup)
using UnityEngine;

public class FootLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;

    [Header("Ground contact")]
    [SerializeField] private float contactHeight = 0.03f;

    [Header("Movement")]
    [SerializeField] private bool movementEnabled = true;
    [SerializeField] private float gain = 2f;
    [SerializeField] private bool invertDirection = false;
    [SerializeField] private bool forwardOnly = true;
    [SerializeField] private float maxMovePerFrame = 0.1f;

    [Tooltip("Velocity smoothing time constant in seconds. 0 disables smoothing.")]
    [Min(0f)]
    [SerializeField] private float moveSmoothTime = 0.04f;

    [Header("Debug (read-only)")]
    [SerializeField] private bool leftGrounded;
    [SerializeField] private bool rightGrounded;

    private Vector3 prevL, prevR;
    private float groundL, groundR;
    private Vector3 smoothedVelocity;

    void Start()
    {
        if (xrOrigin == null)
            xrOrigin = transform;

        Recalibrate();
    }

    void OnDisable()
    {
        smoothedVelocity = Vector3.zero;
    }

    public void Recalibrate()
    {
        smoothedVelocity = Vector3.zero;

        if (leftFootTarget == null || rightFootTarget == null)
            return;

        prevL = leftFootTarget.localPosition;
        prevR = rightFootTarget.localPosition;
        groundL = prevL.y;
        groundR = prevR.y;
    }

    Vector3 ToWorld(Transform target, Vector3 localDelta)
    {
        return target.parent != null
            ? target.parent.TransformVector(localDelta)
            : localDelta;
    }

    Vector3 BodyForward()
    {
        Transform frame = leftFootTarget.parent != null
            ? leftFootTarget.parent
            : xrOrigin;

        Vector3 forward = frame.forward;
        forward.y = 0f;

        return forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : Vector3.forward;
    }

    void LateUpdate()
    {
        if (xrOrigin == null ||
            leftFootTarget == null ||
            rightFootTarget == null)
        {
            smoothedVelocity = Vector3.zero;
            return;
        }

        Vector3 left = leftFootTarget.localPosition;
        Vector3 right = rightFootTarget.localPosition;

        leftGrounded = left.y <= groundL + contactHeight;
        rightGrounded = right.y <= groundR + contactHeight;

        Vector3 sum = Vector3.zero;
        int groundedCount = 0;

        if (leftGrounded)
        {
            Vector3 delta = left - prevL;
            delta.y = 0f;
            sum += ToWorld(leftFootTarget, delta);
            groundedCount++;
        }

        if (rightGrounded)
        {
            Vector3 delta = right - prevR;
            delta.y = 0f;
            sum += ToWorld(rightFootTarget, delta);
            groundedCount++;
        }

        prevL = left;
        prevR = right;

        float dt = Time.deltaTime;

        if (!movementEnabled || groundedCount == 0 || dt <= 0f)
        {
            smoothedVelocity = Vector3.zero;
            return;
        }

        Vector3 move = -(sum / groundedCount) * gain;
        move.y = 0f;

        if (invertDirection)
            move = -move;

        if (forwardOnly)
        {
            Vector3 forward = BodyForward();
            float forwardAmount = Vector3.Dot(move, forward);

            if (forwardAmount < 0f)
                move -= forward * forwardAmount;
        }

        float moveLimit = Mathf.Max(0f, maxMovePerFrame);
        move = Vector3.ClampMagnitude(move, moveLimit);

        Vector3 targetVelocity = move / dt;

        if (moveSmoothTime <= 0f)
        {
            smoothedVelocity = targetVelocity;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-dt / moveSmoothTime);
            smoothedVelocity = Vector3.Lerp(
                smoothedVelocity, targetVelocity, blend);
        }

        Vector3 finalMove = Vector3.ClampMagnitude(
            smoothedVelocity * dt, moveLimit);

        xrOrigin.position += finalMove;
    }
}
*/
using System;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class FootLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform navigationFrame;
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;
    [SerializeField] private HandFootCorrelator correlator;
    [SerializeField] private GlanceTurn glanceTurn;

    [Header("Ground contact")]
    [SerializeField] private float contactHeight = 0.03f;

    [Header("Movement")]
    [SerializeField] private bool movementEnabled = true;
    [SerializeField] private float gain = 2f;
    [SerializeField] private bool invertDirection;
    [SerializeField] private bool forwardOnly = true;
    [SerializeField] private float maxMovePerFrame = 0.1f;

    [Min(0f)]
    [SerializeField] private float moveSmoothTime = 0.04f;

    [Header("Footstep audio")]
    [SerializeField] private AudioSource leftFootAudio;
    [SerializeField] private AudioSource rightFootAudio;
    [SerializeField] private AudioClip[] footstepClips;

    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.5f;

    [Min(0f)]
    [SerializeField] private float stepLiftMargin = 0.02f;

    [Min(0f)]
    [SerializeField] private float stepCooldown = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool leftGrounded;
    [SerializeField] private bool rightGrounded;

    private Vector3 prevL, prevR;
    private Vector3 smoothedLocalVelocity;
    private float groundL, groundR;
    private float nextLeftSound, nextRightSound;
    private bool leftStepArmed, rightStepArmed;
    private bool inputWasReady;

    void Start()
    {
        if (xrOrigin == null)
            xrOrigin = transform;

        Recalibrate();
    }

    void OnDisable()
    {
        ResetMotion();
    }

    void ResetMotion()
    {
        smoothedLocalVelocity = Vector3.zero;
        leftStepArmed = false;
        rightStepArmed = false;
        inputWasReady = false;
    }

    public void Recalibrate()
    {
        ResetMotion();

        nextLeftSound = nextRightSound =
            Time.unscaledTime + stepCooldown;

        if (leftFootTarget == null || rightFootTarget == null)
            return;

        prevL = leftFootTarget.localPosition;
        prevR = rightFootTarget.localPosition;

        groundL = prevL.y;
        groundR = prevR.y;
    }

    Vector3 ToWorld(Transform target, Vector3 delta)
    {
        delta.y = 0f;

        return target.parent != null
            ? target.parent.TransformVector(delta)
            : delta;
    }

    void PlayStep(AudioSource source)
    {
        if (source == null ||
            footstepClips == null ||
            footstepClips.Length == 0)
            return;

        AudioClip clip = footstepClips[
            UnityEngine.Random.Range(0, footstepClips.Length)];

        if (clip != null)
            source.PlayOneShot(clip, footstepVolume);
    }

    void CheckFootsteps(Vector3 left, Vector3 right)
    {
        if (left.y > groundL + contactHeight + stepLiftMargin)
            leftStepArmed = true;

        if (right.y > groundR + contactHeight + stepLiftMargin)
            rightStepArmed = true;

        if (leftGrounded && leftStepArmed)
        {
            leftStepArmed = false;

            if (Time.unscaledTime >= nextLeftSound)
            {
                nextLeftSound = Time.unscaledTime + stepCooldown;
                PlayStep(leftFootAudio);
            }
        }

        if (rightGrounded && rightStepArmed)
        {
            rightStepArmed = false;

            if (Time.unscaledTime >= nextRightSound)
            {
                nextRightSound = Time.unscaledTime + stepCooldown;
                PlayStep(rightFootAudio);
            }
        }
    }

    void LateUpdate()
    {
        if (xrOrigin == null ||
            navigationFrame == null ||
            leftFootTarget == null ||
            rightFootTarget == null)
        {
            ResetMotion();
            return;
        }

        Vector3 left = leftFootTarget.localPosition;
        Vector3 right = rightFootTarget.localPosition;

        leftGrounded = left.y <= groundL + contactHeight;
        rightGrounded = right.y <= groundR + contactHeight;

        bool ready =
            movementEnabled &&
            correlator != null &&
            correlator.InputReady &&
            (glanceTurn == null || !glanceTurn.BlockWalking);

        if (!ready)
        {
            prevL = left;
            prevR = right;
            ResetMotion();
            return;
        }

        if (!inputWasReady)
        {
            prevL = left;
            prevR = right;
            inputWasReady = true;
            return;
        }

        CheckFootsteps(left, right);

        Vector3 sum = Vector3.zero;
        int groundedCount = 0;

        if (leftGrounded)
        {
            sum += ToWorld(leftFootTarget, left - prevL);
            groundedCount++;
        }

        if (rightGrounded)
        {
            sum += ToWorld(rightFootTarget, right - prevR);
            groundedCount++;
        }

        prevL = left;
        prevR = right;

        float dt = Time.deltaTime;

        if (groundedCount == 0 || dt <= 0f)
        {
            smoothedLocalVelocity = Vector3.zero;
            return;
        }

        Vector3 move = -(sum / groundedCount) * gain;

        if (invertDirection)
            move = -move;

        move.y = 0f;

        Vector3 localMove =
            navigationFrame.InverseTransformDirection(move);

        localMove.y = 0f;

        // !!! 
        if (forwardOnly)
        {
            localMove.x = 0f;
            localMove.z = Mathf.Max(0f, localMove.z);
        }

        float limit = Mathf.Max(0f, maxMovePerFrame);

        localMove = Vector3.ClampMagnitude(localMove, limit);

        Vector3 desiredVelocity = localMove / dt;

        float blend = moveSmoothTime <= 0f
            ? 1f
            : 1f - Mathf.Exp(-dt / moveSmoothTime);

        smoothedLocalVelocity = Vector3.Lerp(
            smoothedLocalVelocity, desiredVelocity, blend);

        Vector3 finalMove =
            navigationFrame.TransformDirection(
                smoothedLocalVelocity) * dt;

        xrOrigin.position += Vector3.ClampMagnitude(
            finalMove, limit);
    }
}
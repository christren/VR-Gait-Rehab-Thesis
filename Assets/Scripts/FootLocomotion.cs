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

    [Header("Debug (read-only)")]
    [SerializeField] private bool leftGrounded;
    [SerializeField] private bool rightGrounded;

    private Vector3 prevL, prevR;
    private float groundL, groundR;

    void Start()
    {
        if (xrOrigin == null) xrOrigin = transform;
        Recalibrate();
    }

    public void Recalibrate()
    {
        prevL = leftFootTarget.localPosition;
        prevR = rightFootTarget.localPosition;
        groundL = prevL.y;
        groundR = prevR.y;
    }

    Vector3 ToWorld(Transform t, Vector3 localDelta)
    {
        return t.parent != null ? t.parent.TransformVector(localDelta) : localDelta;
    }

    Vector3 BodyForward()
    {
        Transform frame = leftFootTarget.parent != null ? leftFootTarget.parent : xrOrigin;
        Vector3 f = frame.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    void LateUpdate()
    {
        if (leftFootTarget == null || rightFootTarget == null) return;

        Vector3 l = leftFootTarget.localPosition;
        Vector3 r = rightFootTarget.localPosition;

        leftGrounded = l.y <= groundL + contactHeight;
        rightGrounded = r.y <= groundR + contactHeight;

        Vector3 sum = Vector3.zero;
        int n = 0;

        if (leftGrounded)
        {
            Vector3 d = l - prevL; d.y = 0f;
            sum += ToWorld(leftFootTarget, d); n++;
        }
        if (rightGrounded)
        {
            Vector3 d = r - prevR; d.y = 0f;
            sum += ToWorld(rightFootTarget, d); n++;
        }

        prevL = l;
        prevR = r;

        if (!movementEnabled || n == 0) return;

        Vector3 move = -(sum / n) * gain;
        move.y = 0f;
        if (invertDirection) move = -move;

        if (forwardOnly)
        {
            Vector3 fwd = BodyForward();
            float f = Vector3.Dot(move, fwd);
            if (f < 0f) move -= fwd * f;
        }

        move = Vector3.ClampMagnitude(move, maxMovePerFrame);
        xrOrigin.position += move;
    }
}
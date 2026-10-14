/*
using UnityEngine;

public class BodyHeadTether : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform head;

    [Header("Position")]
    [SerializeField] private float forwardOffset = 0.25f;
    [SerializeField] private float followSpeed = 14f;

    [Header("Rotation")]
    [SerializeField] private float yawFollowSpeed = 10f;

    private float calibratedLocalY;

    private void Awake()
    {
        calibratedLocalY = transform.localPosition.y;
    }

    private void LateUpdate()
    {
        if (head == null || transform.parent == null)
            return;

        Vector3 localHeadPosition =
            transform.parent.InverseTransformPoint(head.position);

        Vector3 targetLocalPosition = new Vector3(
            localHeadPosition.x,
            calibratedLocalY,
            localHeadPosition.z
        );

        Vector3 flatHeadForward =
            Vector3.ProjectOnPlane(head.forward, Vector3.up);

        if (flatHeadForward.sqrMagnitude > 0.001f)
        {
            flatHeadForward.Normalize();

            Vector3 localForward =
                transform.parent.InverseTransformDirection(flatHeadForward);

            targetLocalPosition += localForward * forwardOffset;

            Quaternion targetRotation =
                Quaternion.LookRotation(flatHeadForward, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                yawFollowSpeed * Time.deltaTime
            );
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetLocalPosition,
            followSpeed * Time.deltaTime
        );
    }

    public void RecalibrateHeight()
    {
        calibratedLocalY = transform.localPosition.y;
    }
}
*/

using UnityEngine;

public class BodyHeadTether : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform head;

    [Header("Position")]
    [SerializeField] private float forwardOffset = 0.25f;
    [SerializeField] private float sideOffset = 0f;
    [SerializeField] private float followSpeed = 14f;

    [Header("Rotation")]
    [SerializeField] private float yawFollowSpeed = 10f;

    private float calibratedLocalY;
    private Vector3 lastFlatDir = Vector3.forward;

    private void Awake()
    {
        calibratedLocalY = transform.localPosition.y;
    }

    private Vector3 StableFlatForward()
    {
        Vector3 f = head.forward;
        Vector3 flatF = Vector3.ProjectOnPlane(f, Vector3.up);
        Vector3 dir = flatF.sqrMagnitude > 0.0001f ? flatF.normalized : lastFlatDir;

        float w = Mathf.InverseLerp(0.5f, 0.9f, Mathf.Abs(f.y));
        Vector3 up = f.y < 0f ? head.up : -head.up;
        Vector3 flatU = Vector3.ProjectOnPlane(up, Vector3.up);

        if (w > 0f && flatU.sqrMagnitude > 0.0001f)
        {
            Vector3 blended = Vector3.Lerp(dir, flatU.normalized, w);
            if (blended.sqrMagnitude > 0.0001f) dir = blended.normalized;
        }

        lastFlatDir = dir;
        return dir;
    }

    private void LateUpdate()
    {
        if (head == null || transform.parent == null) return;

        Vector3 dir = StableFlatForward();
        Vector3 right = Vector3.Cross(Vector3.up, dir);

        Vector3 headLocal = transform.parent.InverseTransformPoint(head.position);
        Vector3 offsetWorld = dir * forwardOffset + right * sideOffset;
        Vector3 offsetLocal = transform.parent.InverseTransformDirection(offsetWorld);

        Vector3 targetLocal = new Vector3(
            headLocal.x + offsetLocal.x,
            calibratedLocalY,
            headLocal.z + offsetLocal.z);

        float kPos = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        float kYaw = 1f - Mathf.Exp(-yawFollowSpeed * Time.deltaTime);

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, kYaw);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocal, kPos);
    }

    public void RecalibrateHeight()
    {
        calibratedLocalY = transform.localPosition.y;
    }
}
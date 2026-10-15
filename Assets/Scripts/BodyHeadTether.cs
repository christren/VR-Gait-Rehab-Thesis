/* Version prior to 14.10.26 (backup)
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
*/

using UnityEngine;

[DefaultExecutionOrder(150)]
public class BodyHeadTether : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform navigationFrame;

    [Header("Position")]
    [SerializeField] private float forwardOffset = -0.2f;
    [SerializeField] private float sideOffset;
    [SerializeField] private float followSpeed = 14f;

    private float calibratedLocalY;

    void Awake()
    {
        calibratedLocalY = transform.localPosition.y;
    }

    void LateUpdate()
    {
        if (head == null ||
            navigationFrame == null ||
            transform.parent == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(
            navigationFrame.forward, Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.001f)
            return;

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 goalWorld = head.position +
            forward * forwardOffset +
            right * sideOffset;

        Vector3 goalLocal =
            transform.parent.InverseTransformPoint(goalWorld);

        goalLocal.y = calibratedLocalY;

        float blend =
            1f - Mathf.Exp(-followSpeed * Time.deltaTime);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition, goalLocal, blend);

        transform.rotation =
            Quaternion.LookRotation(forward, Vector3.up);
    }

    public void RecalibrateHeight()
    {
        calibratedLocalY = transform.localPosition.y;
    }
}
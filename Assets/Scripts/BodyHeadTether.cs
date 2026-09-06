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
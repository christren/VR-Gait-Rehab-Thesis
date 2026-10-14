using UnityEngine;

public class LegRigAligner : MonoBehaviour
{
    [SerializeField] private Transform body;
    [SerializeField] private float hintDistance = 0.4f;

    [Header("Left")]
    [SerializeField] private Transform leftFootBone;
    [SerializeField] private Transform leftKneeBone;
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform leftKneeHint;

    [Header("Right")]
    [SerializeField] private Transform rightFootBone;
    [SerializeField] private Transform rightKneeBone;
    [SerializeField] private Transform rightFootTarget;
    [SerializeField] private Transform rightKneeHint;

    [ContextMenu("Align Targets And Hints")]
    void Align()
    {
        AlignOne(leftFootBone, leftKneeBone, leftFootTarget, leftKneeHint);
        AlignOne(rightFootBone, rightKneeBone, rightFootTarget, rightKneeHint);
    }

    void AlignOne(Transform foot, Transform knee, Transform target, Transform hint)
    {
        if (foot == null || target == null) return;

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(target, "Align rig");
#endif
        target.SetPositionAndRotation(foot.position, foot.rotation);

        if (knee == null || hint == null || body == null) return;

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(hint, "Align rig");
#endif
        Vector3 fwd = body.forward;
        fwd.y = 0f;
        fwd.Normalize();
        hint.position = knee.position + fwd * hintDistance;
    }
}
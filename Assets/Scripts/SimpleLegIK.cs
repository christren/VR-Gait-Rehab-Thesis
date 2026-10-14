using UnityEngine;

public class SimpleLegIK : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        public Transform upper;
        public Transform lower;
        public Transform foot;
        public Transform target;

        [HideInInspector] public float upperLength;
        [HideInInspector] public float lowerLength;
        [HideInInspector] public Vector3 offsetLocal;
        [HideInInspector] public Quaternion footRotOffset;
    }

    [SerializeField] private Transform body;
    [SerializeField] private Leg leftLeg = new Leg();
    [SerializeField] private Leg rightLeg = new Leg();
    [Range(0.9f, 1f)]
    [SerializeField] private float maxStretch = 0.995f;

    void Start()
    {
        Capture(leftLeg);
        Capture(rightLeg);
    }

    bool Valid(Leg l)
    {
        return body != null && l.upper != null && l.lower != null
            && l.foot != null && l.target != null;
    }

    void Capture(Leg l)
    {
        if (!Valid(l)) return;
        l.upperLength = Vector3.Distance(l.upper.position, l.lower.position);
        l.lowerLength = Vector3.Distance(l.lower.position, l.foot.position);
        l.offsetLocal = body.InverseTransformVector(l.foot.position - l.target.position);
        l.footRotOffset = Quaternion.Inverse(l.target.rotation) * l.foot.rotation;
    }

    void LateUpdate()
    {
        Solve(leftLeg);
        Solve(rightLeg);
    }

    void Solve(Leg l)
    {
        if (!Valid(l)) return;

        Vector3 hip = l.upper.position;
        Vector3 goal = l.target.position + body.TransformVector(l.offsetLocal);

        float a = l.upperLength;
        float b = l.lowerLength;
        Vector3 toGoal = goal - hip;
        float dist = toGoal.magnitude;
        if (dist < 0.0001f) return;

        float minD = Mathf.Abs(a - b) + 0.001f;
        float maxD = (a + b) * maxStretch;
        float d = Mathf.Clamp(dist, minD, maxD);
        Vector3 u = toGoal / dist;

        Vector3 pole = Vector3.ProjectOnPlane(body.forward, u);
        if (pole.sqrMagnitude < 0.0001f)
            pole = Vector3.ProjectOnPlane(body.right, u);
        pole.Normalize();

        float cosHip = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        float sinHip = Mathf.Sqrt(1f - cosHip * cosHip);
        Vector3 kneeGoal = hip + u * (a * cosHip) + pole * (a * sinHip);

        l.upper.rotation = Quaternion.FromToRotation(
            l.lower.position - hip, kneeGoal - hip) * l.upper.rotation;

        Vector3 knee = l.lower.position;
        Vector3 footGoal = hip + u * d;
        l.lower.rotation = Quaternion.FromToRotation(
            l.foot.position - knee, footGoal - knee) * l.lower.rotation;

        l.foot.rotation = l.target.rotation * l.footRotOffset;
    }
}
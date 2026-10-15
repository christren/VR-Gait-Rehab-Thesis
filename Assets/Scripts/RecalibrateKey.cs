using UnityEngine;
using UnityEngine.InputSystem;

public class RecalibrateKey : MonoBehaviour
{
    private HandFootCorrelator correlator;
    private FootLocomotion locomotion;
    private GlanceTurn glanceTurn;

    void Start()
    {
        correlator = FindFirstObjectByType<HandFootCorrelator>();
        locomotion = FindFirstObjectByType<FootLocomotion>();
        glanceTurn = FindFirstObjectByType<GlanceTurn>();
    }

    void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.cKey.wasPressedThisFrame)
            return;

        if (glanceTurn != null)
            glanceTurn.RecalibrateNeutral();

        if (correlator != null)
            correlator.Recalibrate();

        if (locomotion != null)
            locomotion.Recalibrate();
    }
}
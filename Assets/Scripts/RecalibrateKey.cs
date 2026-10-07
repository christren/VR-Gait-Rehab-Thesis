using UnityEngine;
using UnityEngine.InputSystem;

public class RecalibrateKey : MonoBehaviour
{
    private HandFootCorrelator correlator;
    private FootLocomotion locomotion;

    void Start()
    {
        correlator = FindFirstObjectByType<HandFootCorrelator>();
        locomotion = FindFirstObjectByType<FootLocomotion>();
    }

    void Update()
    {
        if (correlator == null || locomotion == null) return;

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            correlator.Recalibrate();
            locomotion.Recalibrate();
        }
    }
}
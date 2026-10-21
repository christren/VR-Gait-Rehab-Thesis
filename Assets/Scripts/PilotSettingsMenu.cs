using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class PilotSettingsMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuRoot;
    public CanvasGroup canvasGroup;
    public Transform content;
    public Toggle checkboxTemplate;
    public Button applyButton;
    public TMP_Text statusText;

    [Header("Position")]
    public Transform head;
    public float menuDistance = 0.6f;
    public float menuHeightOffset = -0.08f;

    [Header("Existing gameplay components")]
    public FootLocomotion locomotion;
    public HandFootCorrelator correlator;
    public GlanceTurn turning;

    [Header("Optional advanced components")]
    public BodyHeadTether bodyTether;
    public SimpleLegIK legIK;
    public PinchRecalibrate pinchCalibration;
    public RecalibrateKey keyboardCalibration;

    [Header("Menu-only hand interaction objects")]
    public GameObject[] handPokeRoots;

    [Header("Startup")]
    public float preparationSeconds = 3f;

    private class SettingRow
    {
        public string label;
        public Func<bool> read;
        public Action<bool> write;
        public Toggle toggle;
    }

    private readonly List<SettingRow> rows =
        new List<SettingRow>();

    private bool menuOpen;
    private bool menuPlaced;
    private bool applying;

    void Start()
    {
        if (menuRoot == null ||
            canvasGroup == null ||
            content == null ||
            checkboxTemplate == null ||
            applyButton == null ||
            head == null ||
            locomotion == null ||
            correlator == null ||
            turning == null)
        {
            Debug.LogError(
                "PilotSettingsMenu: assign all required references.");

            enabled = false;
            return;
        }

        BuildRows();

        applyButton.onClick.AddListener(ApplyAndStart);

        ShowMenu();
    }

    void OnDestroy()
    {
        if (applyButton != null)
            applyButton.onClick.RemoveListener(ApplyAndStart);
    }

    void BuildRows()
    {
        checkboxTemplate.gameObject.SetActive(false);

        AddRow(
            "Movement enabled",
            () => locomotion.MovementEnabled,
            value => locomotion.MovementEnabled = value);

        AddRow(
            "Forward only",
            () => locomotion.ForwardOnly,
            value => locomotion.ForwardOnly = value);

        AddRow(
            "Invert movement direction",
            () => locomotion.InvertDirection,
            value => locomotion.InvertDirection = value);

        AddRow(
            "Cross-map left and right hands",
            () => correlator.crossMapSides,
            value => correlator.crossMapSides = value);

        AddRow(
            "Invert hand-foot X",
            () => correlator.xAxis.invert,
            value => correlator.xAxis.invert = value);

        AddRow(
            "Invert hand-foot Y",
            () => correlator.yAxis.invert,
            value => correlator.yAxis.invert = value);

        AddRow(
            "Invert hand-foot Z",
            () => correlator.zAxis.invert,
            value => correlator.zAxis.invert = value);

        AddRow(
            "Lean steering enabled",
            () => turning.turningEnabled,
            value => turning.turningEnabled = value);

        AddRow(
            "Invert lean steering",
            () => turning.invertTurn,
            value => turning.invertTurn = value);

        AddRow(
            "Pause walking while turning",
            () => turning.pauseWalkingDuringTurn,
            value => turning.pauseWalkingDuringTurn = value);

        AddComponentRow("Hand-foot correlator", correlator);
        AddComponentRow("Foot locomotion", locomotion);
        AddComponentRow("Turning component", turning);
        AddComponentRow("Body tether", bodyTether);
        AddComponentRow("Leg IK", legIK);
        AddComponentRow("Pinch calibration", pinchCalibration);
        AddComponentRow("Keyboard calibration", keyboardCalibration);
    }

    void AddComponentRow(string label, Behaviour component)
    {
        if (component == null)
            return;

        AddRow(
            "Advanced: " + label,
            () => component.enabled,
            value => component.enabled = value);
    }

    void AddRow(
        string label,
        Func<bool> read,
        Action<bool> write)
    {
        Toggle toggle = Instantiate(checkboxTemplate, content);

        toggle.name = label;
        toggle.group = null;

        TMP_Text tmpLabel =
            toggle.GetComponentInChildren<TMP_Text>(true);

        if (tmpLabel != null)
        {
            tmpLabel.text = label;
        }
        else
        {
            Text legacyLabel =
                toggle.GetComponentInChildren<Text>(true);

            if (legacyLabel != null)
                legacyLabel.text = label;
        }

        toggle.SetIsOnWithoutNotify(read());
        toggle.gameObject.SetActive(true);

        rows.Add(new SettingRow
        {
            label = label,
            read = read,
            write = write,
            toggle = toggle
        });
    }

    [ContextMenu("Show Menu")]
    public void ShowMenu()
    {
        if (!Application.isPlaying || applying || rows.Count == 0)
            return;

        if (menuOpen)
            return;

        // Read the intended configuration before pausing scripts.
        foreach (SettingRow row in rows)
            row.toggle.SetIsOnWithoutNotify(row.read());

        // Pause gameplay, not the XR tracking/UI systems.
        locomotion.enabled = false;
        turning.enabled = false;
        correlator.enabled = false;

        if (pinchCalibration != null)
            pinchCalibration.enabled = false;

        if (keyboardCalibration != null)
            keyboardCalibration.enabled = false;

        menuRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            (RectTransform)content);

        ScrollRect scrollRect =
            content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        SetHandPokes(true);

        menuOpen = true;
        menuPlaced = false;

        SetStatus(
            "Choose settings, then Apply & Start.\n" +
            "You will have time to return to neutral.");
    }

    void LateUpdate()
    {
        if (!menuOpen || menuPlaced || !turning.HeadTracked)
            return;

        Vector3 forward =
            Vector3.ProjectOnPlane(head.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.01f)
            return;

        forward.Normalize();

        Vector3 position =
            head.position +
            forward * menuDistance +
            Vector3.up * menuHeightOffset;

        menuRoot.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(forward, Vector3.up));

        menuPlaced = true;
    }

    public void ApplyAndStart()
    {
        if (!menuOpen || applying)
            return;

        StartCoroutine(ApplyRoutine());
    }

    IEnumerator ApplyRoutine()
    {
        applying = true;
        canvasGroup.interactable = false;

        bool[] selectedValues = new bool[rows.Count];

        for (int i = 0; i < rows.Count; i++)
            selectedValues[i] = rows[i].toggle.isOn;

        float remaining = Mathf.Max(0f, preparationSeconds);

        while (remaining > 0f)
        {
            SetStatus(
                "Look straight ahead and return hands to neutral.\n" +
                "Starting in " + Mathf.CeilToInt(remaining));

            yield return null;

            remaining -= Time.unscaledDeltaTime;
        }

        while (!turning.HeadTracked)
        {
            SetStatus("Waiting for headset tracking...");
            yield return null;
        }

        for (int i = 0; i < rows.Count; i++)
            rows[i].write(selectedValues[i]);

        // These methods can be called even if an advanced
        // checkbox intentionally leaves a component disabled.
        turning.RecalibrateNeutral();
        correlator.Recalibrate();
        locomotion.Recalibrate();

        List<string> values = new List<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            values.Add(
                rows[i].label + "=" + selectedValues[i]);
        }

        Debug.Log(
            "Pilot settings applied " +
            DateTime.UtcNow.ToString("o") + "\n" +
            string.Join("; ", values));

        SetHandPokes(false);

        menuRoot.SetActive(false);
        menuOpen = false;
        applying = false;
    }

    void SetHandPokes(bool active)
    {
        if (handPokeRoots == null)
            return;

        foreach (GameObject root in handPokeRoots)
        {
            if (root != null)
                root.SetActive(active);
        }
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
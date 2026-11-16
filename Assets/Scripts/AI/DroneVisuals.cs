using UnityEngine;
using UnityEngine.SceneManagement;

// Drone search light + HUD alert feedback.
// Zero setup: on Play it attaches itself to every DroneAI in the scene,
// creates a spotlight if none is assigned, and creates the SonarHUD.
// Reads the drone's real AI state (not its speed), so the light doesn't
// flicker red at patrol waypoints.
[RequireComponent(typeof(DroneAI))]
public class DroneVisuals : MonoBehaviour
{
    [Header("Search Light (auto-created if empty)")]
    public Light searchLight;
    [SerializeField] private float lightRange = 10f;
    [SerializeField] private float lightSpotAngle = 70f;
    [SerializeField] private float lightIntensity = 8f;

    [Header("Colours")]
    public Color patrolColor = Color.cyan;
    public Color suspiciousColor = new Color(1f, 0.6f, 0f);
    public Color alertColor = Color.red;

    [Header("Smoothing")]
    [SerializeField] private float colorLerpSpeed = 6f;
    [SerializeField] private float riseSpeed = 3f;
    [SerializeField] private float fallSpeed = 0.5f;

    private DroneAI droneAI;
    private DroneVision[] visions;
    private float alertLevel;

    // Auto-attach

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToAllDrones();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) =>
        AttachToAllDrones();

    private static void AttachToAllDrones()
    {
        foreach (var drone in FindObjectsByType<DroneAI>(FindObjectsSortMode.None))
        {
            if (drone.GetComponent<DroneVisuals>() == null)
                drone.gameObject.AddComponent<DroneVisuals>();
        }
    }

    // Lifecycle

    private void Awake()
    {
        droneAI = GetComponent<DroneAI>();
        // The drone may carry more than one DroneVision; watch all of them.
        visions = GetComponents<DroneVision>();

        if (searchLight == null)
            searchLight = CreateSearchLight();
    }

    private void Start()
    {
        SonarHUD.EnsureExists();
        searchLight.color = patrolColor;
    }

    private void Update()
    {
        bool caught = false;
        float detection = 0f;

        foreach (var vision in visions)
        {
            if (vision == null) continue;
            caught |= vision.PlayerCaught;
            detection = Mathf.Max(detection, vision.DetectionProgress);
        }

        float target;
        string status;

        if (caught)
        {
            target = 1f;
            status = "DETECTED";
        }
        else if (detection > 0f)
        {
            target = Mathf.Lerp(0.6f, 0.95f, detection);
            status = "VISUAL CONTACT";
        }
        else
        {
            switch (droneAI.CurrentState)
            {
                case DroneAI.DroneState.Alert:
                    target = 0.8f; status = "NOISE DETECTED"; break;
                case DroneAI.DroneState.InvestigateNoise:
                    target = 0.7f; status = "INVESTIGATING"; break;
                case DroneAI.DroneState.Search:
                    target = 0.5f; status = "SEARCHING"; break;
                case DroneAI.DroneState.Resume:
                    target = 0.2f; status = "RESUMING PATROL"; break;
                default:
                    target = 0.1f; status = "PATROLLING"; break;
            }
        }

        // Rise quickly, cool down slowly; snap to full when caught.
        if (caught)
            alertLevel = 1f;
        else
            alertLevel = Mathf.MoveTowards(
                alertLevel,
                target,
                (target > alertLevel ? riseSpeed : fallSpeed) * Time.deltaTime);

        UpdateLight();

        if (SonarHUD.Instance != null)
            SonarHUD.Instance.ReportAlert(this, alertLevel, status);
    }

    private void UpdateLight()
    {
        if (searchLight == null) return;

        Color targetColor =
            alertLevel >= 0.65f ? alertColor :
            alertLevel >= 0.35f ? suspiciousColor :
            patrolColor;

        searchLight.color = Color.Lerp(
            searchLight.color,
            targetColor,
            colorLerpSpeed * Time.deltaTime);
    }

    private Light CreateSearchLight()
    {
        var go = new GameObject("SearchLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        go.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = lightRange;
        light.spotAngle = lightSpotAngle;
        light.intensity = lightIntensity;
        light.shadows = LightShadows.None;
        light.color = patrolColor;
        return light;
    }
}

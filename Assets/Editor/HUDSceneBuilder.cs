using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-shot authoring utility for the GreyboxRoom HUD hierarchy.</summary>
public static class HUDSceneBuilder
{
    private const string ScenePath = "Assets/GreyboxRoom.unity";
    private const string AssetFolder = "Assets/UI/HUD";
    private const int MarkerLayer = 8;

    private static readonly Color PanelColor = new Color(0.018f, 0.055f, 0.075f, 0.92f);
    private static readonly Color BorderColor = new Color(0.04f, 0.82f, 0.76f, 0.9f);
    private static readonly Color TextColor = new Color(0.76f, 0.94f, 0.92f, 1f);

    [MenuItem("Sonar Sentinel/Build Greybox HUD")]
    public static void Build()
    {
        EnsureFolder("Assets/UI");
        EnsureFolder(AssetFolder);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // The HUD layout is authored for landscape mobile play (the supplied
        // Game view reference is widescreen). Keep both landscape rotations.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        Camera mainCamera = Camera.main;
        GameObject playerObject = GameObject.Find("Player");
        GameObject droneObject = GameObject.Find("SecurityDrone");
        if (mainCamera == null || playerObject == null || droneObject == null)
            throw new System.InvalidOperationException("GreyboxRoom must contain MainCamera, Player and SecurityDrone.");

        Transform oldHud = scene.GetRootGameObjects().Length > 0
            ? FindRootTransform("GameHUD") : null;
        if (oldHud != null)
        {
            BuildIntoExisting(scene, oldHud.gameObject, mainCamera, playerObject, droneObject);
            return;
        }

        RenderTexture mapTexture = CreateMapTexture();
        Material playerMaterial = CreateMarkerMaterial("PlayerMarker.mat", new Color(0.12f, 0.95f, 0.82f));
        Material droneMaterial = CreateMarkerMaterial("DroneMarker.mat", new Color(1f, 0.25f, 0.31f));
        Material extractionMaterial = CreateMarkerMaterial("ExtractionMarker.mat", new Color(1f, 0.78f, 0.16f));

        GameObject canvasObject = new GameObject("GameHUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<SafeAreaFitter>();
        canvas.pixelPerfect = false;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform alertPanel = CreatePanel(canvasObject.transform, "AlertPanel", new Vector2(36, -34),
            new Vector2(390, 160), new Vector2(0, 1), new Vector2(0, 1));
        TMP_Text alertTitle = CreateText(alertPanel, "AlertTitle", "SECURITY RESPONSE", 22,
            new Vector2(18, -14), new Vector2(340, 32), new Vector2(0, 1), TextColor, FontStyles.Bold);
        TMP_Text alertCaption = CreateText(alertPanel, "AlertSubtitle", "DETECTION", 13,
            new Vector2(18, -56), new Vector2(250, 22), new Vector2(0, 1), BorderColor, FontStyles.Bold);
        Image alertBackground = CreateImage(alertPanel, "AlertBarBackground", new Vector2(18, -88),
            new Vector2(354, 20), new Vector2(0, 1), new Color(0.015f, 0.025f, 0.035f, 1f));
        Image alertFill = CreateImage(alertBackground.transform, "AlertBarFill", Vector2.zero,
            new Vector2(1, 1), Vector2.zero, new Color(0.12f, 0.92f, 0.75f, 1f));
        StretchFill(alertFill.rectTransform);
        alertFill.type = Image.Type.Filled;
        alertFill.fillMethod = Image.FillMethod.Horizontal;
        alertFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        alertFill.fillAmount = 0f;
        TMP_Text detectionStatus = CreateText(alertPanel, "DetectionStatus", "LOW", 18,
            new Vector2(18, -116), new Vector2(350, 26), new Vector2(0, 1), BorderColor, FontStyles.Bold);
        detectionStatus.name = "DetectionStatus";

        Transform noisePanel = CreatePanel(canvasObject.transform, "NoisePanel", new Vector2(-36, -34),
            new Vector2(390, 160), new Vector2(1, 1), new Vector2(1, 1));
        CreateText(noisePanel, "NoiseTitle", "ACOUSTIC SIGNATURE", 22,
            new Vector2(-18, -14), new Vector2(354, 32), new Vector2(1, 1), TextColor, FontStyles.Bold,
            TextAlignmentOptions.Right);
        CreateText(noisePanel, "NoiseSubtitle", "SOUND REACH", 13,
            new Vector2(-18, -56), new Vector2(354, 22), new Vector2(1, 1), BorderColor, FontStyles.Bold,
            TextAlignmentOptions.Right);
        Image noiseBar = CreateImage(noisePanel, "NoiseBar", new Vector2(-18, -88),
            new Vector2(354, 20), new Vector2(1, 1), new Color(0.015f, 0.025f, 0.035f, 1f));
        Image noiseFill = CreateImage(noiseBar.transform, "NoiseBarFill", Vector2.zero,
            new Vector2(1, 1), Vector2.zero, BorderColor);
        StretchFill(noiseFill.rectTransform);
        noiseFill.type = Image.Type.Filled;
        noiseFill.fillMethod = Image.FillMethod.Horizontal;
        noiseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        noiseFill.fillAmount = 0f;
        TMP_Text noiseStatus = CreateText(noisePanel, "DecibelText", "LOW", 18,
            new Vector2(-18, -116), new Vector2(354, 24), new Vector2(1, 1), BorderColor, FontStyles.Bold,
            TextAlignmentOptions.Right);
        TMP_Text relativeLabel = CreateText(noisePanel, "NoiseSignalLabel", "RADIUS SIGNAL  /  RELATIVE", 11,
            new Vector2(-18, -137), new Vector2(354, 18), new Vector2(1, 1), new Color(0.47f, 0.65f, 0.67f, 1),
            FontStyles.Normal, TextAlignmentOptions.Right);

        Transform objectivePanel = CreatePanel(canvasObject.transform, "ObjectivePanel", new Vector2(36, 36),
            new Vector2(430, 142), new Vector2(0, 0), new Vector2(0, 0));
        CreateImage(objectivePanel, "ObjectiveIcon", new Vector2(20, 22), new Vector2(50, 50),
            new Vector2(0, 1), new Color(0.04f, 0.46f, 0.48f, 1f));
        CreateText(objectivePanel, "ObjectiveIconGlyph", "C", 26, new Vector2(20, -31),
            new Vector2(50, 38), new Vector2(0, 1), TextColor, FontStyles.Bold, TextAlignmentOptions.Center);
        TMP_Text objectiveText = CreateText(objectivePanel, "ObjectiveText",
            "DATA CORE   //   NOT SECURED\nEXTRACTION COORDINATES PENDING", 17,
            new Vector2(86, -22), new Vector2(326, 96), new Vector2(0, 1), TextColor, FontStyles.Bold);
        objectiveText.textWrappingMode = TextWrappingModes.Normal;

        Transform minimapPanel = CreatePanel(canvasObject.transform, "MinimapPanel", new Vector2(-36, 36),
            new Vector2(258, 258), new Vector2(1, 0), new Vector2(1, 0));
        CreateText(minimapPanel, "MinimapTitle", "TACTICAL SCAN", 14, new Vector2(0, -11),
            new Vector2(234, 24), new Vector2(0.5f, 1), BorderColor, FontStyles.Bold, TextAlignmentOptions.Center);
        RawImage mapDisplay = CreateRawImage(minimapPanel, "MinimapDisplay", new Vector2(12, 12),
            new Vector2(234, 202), new Vector2(0, 0), mapTexture);
        mapDisplay.color = Color.white;
        CreateText(minimapPanel, "MinimapLegend", "YOU     DRONE     EXTRACTION", 10,
            new Vector2(12, -232), new Vector2(234, 17), new Vector2(0, 1), TextColor,
            FontStyles.Bold, TextAlignmentOptions.Center);

        Transform missionPanel = new GameObject("MissionPanel", typeof(RectTransform)).transform;
        missionPanel.SetParent(canvasObject.transform, false);
        SetRect(missionPanel as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.one);

        GameObject winScreen = CreateMissionScreen(missionPanel, "WinScreen", "MISSION COMPLETE",
            "DATA CORE SECURED  //  EXTRACTION SUCCESSFUL");
        GameObject caughtScreen = CreateMissionScreen(missionPanel, "CaughtScreen", "PLAYER CAUGHT",
            "SECURITY DRONE INTERCEPTED THE OPERATIVE");
        winScreen.SetActive(false);
        caughtScreen.SetActive(false);

        Camera minimap = CreateMinimapCamera(mapTexture);
        GameObject playerMarker = CreateMarker("PlayerMapMarker", playerObject.transform, playerMaterial,
            new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.8f, 0.8f));
        GameObject droneMarker = CreateMarker("DroneMapMarker", droneObject.transform, droneMaterial,
            new Vector3(0, 1.2f, 0), new Vector3(1.05f, 1.05f, 1.05f));
        GameObject extractionMarker = CreateMarker("ExtractionMapMarker", null, extractionMaterial,
            Vector3.up * 0.65f, Vector3.one);
        extractionMarker.SetActive(false);

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("HUD EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        HUDController controller = canvasObject.AddComponent<HUDController>();
        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "alertBarFill", alertFill);
        SetObject(serialized, "detectionStatus", detectionStatus);
        SetObject(serialized, "noiseBarFill", noiseFill);
        SetObject(serialized, "noiseStatus", noiseStatus);
        SetObject(serialized, "noiseSignalLabel", relativeLabel);
        SetObject(serialized, "objectiveText", objectiveText);
        SetObject(serialized, "winScreen", winScreen);
        SetObject(serialized, "caughtScreen", caughtScreen);
        SetObject(serialized, "droneVision", droneObject.GetComponent<DroneVision>());
        SetObject(serialized, "player", playerObject.transform);
        SetObject(serialized, "drone", droneObject.transform);
        SetObject(serialized, "extractionPoint", null);
        SetObject(serialized, "playerMapMarker", playerMarker.transform);
        SetObject(serialized, "droneMapMarker", droneMarker.transform);
        SetObject(serialized, "extractionMapMarker", extractionMarker.transform);
        SetObject(serialized, "minimapCamera", minimap);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (Button button in missionPanel.GetComponentsInChildren<Button>(true))
            UnityEventTools.AddPersistentListener(button.onClick, controller.RestartMission);

        MainMenuSceneBuilder.ApplyTo(canvasObject);

        // The minimap-only marker layer is hidden from the gameplay camera.
        mainCamera.cullingMask &= ~(1 << MarkerLayer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("GreyboxRoom HUD built. Extraction target is intentionally unassigned until Part 1 provides it.");
    }

    private static GameObject CreateMissionScreen(Transform parent, string name, string title, string subtitle)
    {
        GameObject screen = new GameObject(name, typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(parent, false);
        screen.layer = parent.gameObject.layer;
        SetRect(screen.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero,
            Vector2.zero, Vector2.zero, Vector2.one);
        screen.GetComponent<Image>().color = new Color(0.005f, 0.018f, 0.028f, 0.9f);

        Transform card = CreatePanel(screen.transform, name + "Card", Vector2.zero,
            new Vector2(650, 350), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        CreateText(card, name + "Title", title, 42, new Vector2(0, -82), new Vector2(580, 70),
            new Vector2(0.5f, 1), TextColor, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateText(card, name + "Subtitle", subtitle, 18, new Vector2(0, -168), new Vector2(580, 42),
            new Vector2(0.5f, 1), BorderColor, FontStyles.Normal, TextAlignmentOptions.Center);

        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card, false);
        buttonObject.layer = card.gameObject.layer;
        SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            Vector2.zero, new Vector2(200, 54), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.02f, 0.37f, 0.39f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        CreateText(buttonObject.transform, "RestartLabel", "RESTART", 18, Vector2.zero,
            Vector2.one, Vector2.zero, TextColor, FontStyles.Bold, TextAlignmentOptions.Center, true);
        return screen;
    }

    private static void BuildIntoExisting(Scene scene, GameObject canvasObject, Camera mainCamera,
        GameObject playerObject, GameObject droneObject)
    {
        if (FindChild(canvasObject.transform, "NoisePanel") != null ||
            canvasObject.GetComponent<HUDController>() != null)
        {
            if (canvasObject.GetComponent<SafeAreaFitter>() == null)
                Undo.AddComponent<SafeAreaFitter>(canvasObject);

            foreach (string panelName in new[] { "AlertPanel", "NoisePanel", "ObjectivePanel", "MinimapPanel" })
            {
                Transform panel = FindChild(canvasObject.transform, panelName);
                Image panelImage = panel != null ? panel.GetComponent<Image>() : null;
                if (panelImage != null)
                    panelImage.raycastTarget = true;
            }

            TMP_Text objectiveGlyph = FindNamedDescendant(canvasObject.transform, "ObjectiveIconGlyph")?.GetComponent<TMP_Text>();
            if (objectiveGlyph != null)
                objectiveGlyph.text = "C";

            MainMenuSceneBuilder.ApplyTo(canvasObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Existing mobile HUD refreshed for safe areas and landscape play; no duplicate panels were created.");
            return;
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (canvas == null || scaler == null)
            throw new System.InvalidOperationException("The existing GameHUD needs a Canvas and CanvasScaler.");

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (canvasObject.GetComponent<SafeAreaFitter>() == null)
            Undo.AddComponent<SafeAreaFitter>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Transform alertPanel = FindChild(canvasObject.transform, "AlertPanel");
        Image alertFill = FindNamedDescendant(alertPanel, "AlertBarFill")?.GetComponent<Image>();
        if (alertPanel == null || alertFill == null)
            throw new System.InvalidOperationException("The existing GameHUD must contain AlertPanel/AlertBarFill.");

        TMP_Text detectionStatus = FindChild(alertPanel, "DetectionStatus")?.GetComponent<TMP_Text>();
        if (detectionStatus == null)
            detectionStatus = CreateText(alertPanel, "DetectionStatus", "LOW", 18,
                new Vector2(10, -82), new Vector2(380, 24), new Vector2(0, 1), BorderColor,
                FontStyles.Bold);
        alertFill.type = Image.Type.Filled;
        alertFill.fillMethod = Image.FillMethod.Horizontal;
        alertFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        RenderTexture mapTexture = CreateMapTexture();
        Material playerMaterial = CreateMarkerMaterial("PlayerMarker.mat", new Color(0.12f, 0.95f, 0.82f));
        Material droneMaterial = CreateMarkerMaterial("DroneMarker.mat", new Color(1f, 0.25f, 0.31f));
        Material extractionMaterial = CreateMarkerMaterial("ExtractionMarker.mat", new Color(1f, 0.78f, 0.16f));

        Transform noisePanel = CreatePanel(canvasObject.transform, "NoisePanel", new Vector2(-36, -34),
            new Vector2(390, 160), new Vector2(1, 1), new Vector2(1, 1));
        CreateText(noisePanel, "NoiseTitle", "ACOUSTIC SIGNATURE", 22,
            new Vector2(-18, -14), new Vector2(354, 32), new Vector2(1, 1), TextColor,
            FontStyles.Bold, TextAlignmentOptions.Right);
        CreateText(noisePanel, "NoiseSubtitle", "SOUND REACH", 13,
            new Vector2(-18, -56), new Vector2(354, 22), new Vector2(1, 1), BorderColor,
            FontStyles.Bold, TextAlignmentOptions.Right);
        Image noiseBar = CreateImage(noisePanel, "NoiseBar", new Vector2(-18, -88),
            new Vector2(354, 20), new Vector2(1, 1), new Color(0.015f, 0.025f, 0.035f, 1f));
        Image noiseFill = CreateImage(noiseBar.transform, "NoiseBarFill", Vector2.zero,
            new Vector2(1, 1), Vector2.zero, BorderColor);
        StretchFill(noiseFill.rectTransform);
        noiseFill.type = Image.Type.Filled;
        noiseFill.fillMethod = Image.FillMethod.Horizontal;
        noiseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        TMP_Text noiseStatus = CreateText(noisePanel, "DecibelText", "LOW", 18,
            new Vector2(-18, -116), new Vector2(354, 24), new Vector2(1, 1), BorderColor,
            FontStyles.Bold, TextAlignmentOptions.Right);
        TMP_Text relativeLabel = CreateText(noisePanel, "NoiseSignalLabel", "RADIUS SIGNAL  /  RELATIVE", 11,
            new Vector2(-18, -137), new Vector2(354, 18), new Vector2(1, 1),
            new Color(0.47f, 0.65f, 0.67f, 1), FontStyles.Normal, TextAlignmentOptions.Right);

        Transform objectivePanel = CreatePanel(canvasObject.transform, "ObjectivePanel", new Vector2(36, 36),
            new Vector2(430, 142), new Vector2(0, 0), new Vector2(0, 0));
        CreateImage(objectivePanel, "ObjectiveIcon", new Vector2(20, 22), new Vector2(50, 50),
            new Vector2(0, 1), new Color(0.04f, 0.46f, 0.48f, 1f));
        CreateText(objectivePanel, "ObjectiveIconGlyph", "C", 26, new Vector2(20, -31),
            new Vector2(50, 38), new Vector2(0, 1), TextColor, FontStyles.Bold,
            TextAlignmentOptions.Center);
        TMP_Text objectiveText = CreateText(objectivePanel, "ObjectiveText",
            "DATA CORE   //   NOT SECURED\nEXTRACTION COORDINATES PENDING", 17,
            new Vector2(86, -22), new Vector2(326, 96), new Vector2(0, 1), TextColor,
            FontStyles.Bold);
        objectiveText.textWrappingMode = TextWrappingModes.Normal;

        Transform minimapPanel = CreatePanel(canvasObject.transform, "MinimapPanel", new Vector2(-36, 36),
            new Vector2(258, 258), new Vector2(1, 0), new Vector2(1, 0));
        CreateText(minimapPanel, "MinimapTitle", "TACTICAL SCAN", 14, new Vector2(0, -11),
            new Vector2(234, 24), new Vector2(0.5f, 1), BorderColor, FontStyles.Bold,
            TextAlignmentOptions.Center);
        RawImage mapDisplay = CreateRawImage(minimapPanel, "MinimapDisplay", new Vector2(12, 12),
            new Vector2(234, 202), new Vector2(0, 0), mapTexture);
        mapDisplay.color = Color.white;
        CreateText(minimapPanel, "MinimapLegend", "YOU     DRONE     EXTRACTION", 10,
            new Vector2(12, -232), new Vector2(234, 17), new Vector2(0, 1), TextColor,
            FontStyles.Bold, TextAlignmentOptions.Center);

        Transform missionPanel = new GameObject("MissionPanel", typeof(RectTransform)).transform;
        missionPanel.SetParent(canvasObject.transform, false);
        SetRect(missionPanel as RectTransform, Vector2.zero, Vector2.one, Vector2.zero,
            Vector2.zero, Vector2.zero, Vector2.one);
        GameObject winScreen = CreateMissionScreen(missionPanel, "WinScreen", "MISSION COMPLETE",
            "DATA CORE SECURED  //  EXTRACTION SUCCESSFUL");
        GameObject caughtScreen = CreateMissionScreen(missionPanel, "CaughtScreen", "PLAYER CAUGHT",
            "SECURITY DRONE INTERCEPTED THE OPERATIVE");
        winScreen.SetActive(false);
        caughtScreen.SetActive(false);

        Camera minimap = CreateMinimapCamera(mapTexture);
        GameObject playerMarker = CreateMarker("PlayerMapMarker", playerObject.transform,
            playerMaterial, new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.8f, 0.8f));
        GameObject droneMarker = CreateMarker("DroneMapMarker", droneObject.transform,
            droneMaterial, new Vector3(0, 1.2f, 0), new Vector3(1.05f, 1.05f, 1.05f));
        GameObject extractionMarker = CreateMarker("ExtractionMapMarker", null,
            extractionMaterial, Vector3.up * 0.65f, Vector3.one);
        extractionMarker.SetActive(false);

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("HUD EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        HUDController controller = canvasObject.GetComponent<HUDController>();
        if (controller == null)
            controller = Undo.AddComponent<HUDController>(canvasObject);
        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "alertBarFill", alertFill);
        SetObject(serialized, "detectionStatus", detectionStatus);
        SetObject(serialized, "noiseBarFill", noiseFill);
        SetObject(serialized, "noiseStatus", noiseStatus);
        SetObject(serialized, "noiseSignalLabel", relativeLabel);
        SetObject(serialized, "objectiveText", objectiveText);
        SetObject(serialized, "winScreen", winScreen);
        SetObject(serialized, "caughtScreen", caughtScreen);
        SetObject(serialized, "droneVision", droneObject.GetComponent<DroneVision>());
        SetObject(serialized, "player", playerObject.transform);
        SetObject(serialized, "drone", droneObject.transform);
        SetObject(serialized, "extractionPoint", null);
        SetObject(serialized, "playerMapMarker", playerMarker.transform);
        SetObject(serialized, "droneMapMarker", droneMarker.transform);
        SetObject(serialized, "extractionMapMarker", extractionMarker.transform);
        SetObject(serialized, "minimapCamera", minimap);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (Button button in missionPanel.GetComponentsInChildren<Button>(true))
            UnityEventTools.AddPersistentListener(button.onClick, controller.RestartMission);

        mainCamera.cullingMask &= ~(1 << MarkerLayer);
        MainMenuSceneBuilder.ApplyTo(canvasObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Existing GreyboxRoom HUD extended with the missing panels and minimap. Extraction target remains unassigned for Part 1.");
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        return null;
    }

    private static Transform FindNamedDescendant(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform nested = FindNamedDescendant(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    private static Camera CreateMinimapCamera(RenderTexture texture)
    {
        GameObject cameraObject = new GameObject("MinimapCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 35, 0);
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        camera.orthographic = true;
        camera.orthographicSize = 14f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.04f, 0.055f, 1f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 80f;
        camera.targetTexture = texture;
        camera.depth = -2f;
        camera.allowHDR = false;
        return camera;
    }

    private static GameObject CreateMarker(string name, Transform target, Material material, Vector3 offset, Vector3 scale)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.layer = MarkerLayer;
        if (marker.TryGetComponent<Collider>(out Collider collider))
            Object.DestroyImmediate(collider);
        marker.GetComponent<MeshRenderer>().sharedMaterial = material;
        marker.transform.localScale = scale;
        if (target != null)
            marker.transform.position = target.position + offset;
        else
            marker.transform.position = offset;
        return marker;
    }

    private static Transform CreatePanel(Transform parent, string name, Vector2 position, Vector2 size,
        Vector2 anchor, Vector2 pivot)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.layer = parent.gameObject.layer;
        SetRect(panel.GetComponent<RectTransform>(), anchor, anchor, position, size, pivot, pivot);
        Image image = panel.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;
        return panel.transform;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size,
        Vector2 anchor, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = parent.gameObject.layer;
        SetRect(imageObject.GetComponent<RectTransform>(), anchor, anchor, position, size, anchor, anchor);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RawImage CreateRawImage(Transform parent, string name, Vector2 position,
        Vector2 size, Vector2 anchor, Texture texture)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = parent.gameObject.layer;
        SetRect(imageObject.GetComponent<RectTransform>(), anchor, anchor, position, size, anchor, anchor);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string content, float size,
        Vector2 position, Vector2 rectSize, Vector2 anchor, Color color,
        FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft,
        bool stretch = false)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (stretch)
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        else
            SetRect(rect, anchor, anchor, position, rectSize, anchor, anchor);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchFill(RectTransform rect)
    {
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            Vector2.zero, Vector2.one);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot, Vector2 offsetsMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.pivot = pivot;
        if (anchorMin != anchorMax)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = offsetsMax == Vector2.one ? Vector2.zero : offsetsMax;
        }
    }

    private static RenderTexture CreateMapTexture()
    {
        const string path = AssetFolder + "/MinimapRT.renderTexture";
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture == null)
        {
            texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRT",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            AssetDatabase.CreateAsset(texture, path);
        }
        return texture;
    }

    private static Material CreateMarkerMaterial(string fileName, Color color)
    {
        string path = AssetFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static Transform FindRootTransform(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == objectName)
                return root.transform;
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, separator));
        AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
    }

    private static void EnsureSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == ScenePath)
                return;

        System.Array.Resize(ref scenes, scenes.Length + 1);
        scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = scenes;
    }
}

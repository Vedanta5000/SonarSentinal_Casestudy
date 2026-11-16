using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies the supplied landscape mobile HUD and art direction to GreyboxRoom.</summary>
public static class ReferenceMobileStyleBuilder
{
    private const string ScenePath = "Assets/GreyboxRoom.unity";
    private const string AssetFolder = "Assets/UI/HUD";
    private const int MarkerLayer = 8;

    private static readonly Color White = new Color(0.92f, 0.95f, 0.95f, 1f);
    private static readonly Color Cyan = new Color(0.10f, 0.85f, 0.92f, 1f);
    private static readonly Color Green = new Color(0.20f, 0.90f, 0.42f, 1f);

    [MenuItem("Sonar Sentinel/Apply Reference Mobile Style")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/UI", "HUD");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject hud = GameObject.Find("GameHUD");
        GameObject player = GameObject.Find("Player");
        GameObject drone = GameObject.Find("SecurityDrone");
        Camera mainCamera = Camera.main;
        if (hud == null || player == null || drone == null || mainCamera == null)
            throw new System.InvalidOperationException("GreyboxRoom needs GameHUD, Player, SecurityDrone and Main Camera.");

        ConfigureLandscape();
        ApplyMapPalette();
        ConfigureRipplePrefab();
        ConfigureIsometricCamera(mainCamera, player.transform);

        Sprite circleSprite = GetCircleSprite("CircleSolid.png", false);
        Sprite ringSprite = GetCircleSprite("CircleRing.png", true);
        BuildSegmentedAlert(hud.transform);
        BuildCircularNoiseGauge(hud.transform, ringSprite);
        BuildObjectiveChip(hud.transform);
        BuildCircularMinimap(hud.transform, circleSprite, ringSprite);
        BuildTouchControls(hud.transform, circleSprite, ringSprite);

        HUDController controller = hud.GetComponent<HUDController>();
        if (controller == null)
            controller = Undo.AddComponent<HUDController>(hud);
        ConfigureHudReferences(controller, hud.transform);
        ConfigureThrowTouch(player, hud.transform);
        ConfigureEventSystem();
        MainMenuSceneBuilder.ApplyTo(hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied landscape mobile reference style to GreyboxRoom. Extraction remains a Part 1 Transform hookup.");
    }

    private static void ConfigureLandscape()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    private static void ConfigureIsometricCamera(Camera camera, Transform player)
    {
        camera.orthographic = true;
        // The room is compact in the 1284-wide mobile simulator. Tighten the
        // framing slightly, and aim from the spawn toward the room's (0, 0, 0)
        // center instead of centering the camera on the spawn at (-8, 0, -5).
        camera.orthographicSize = 11.8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f, 1f);

        IsometricCameraFollow follow = camera.GetComponent<IsometricCameraFollow>();
        if (follow == null)
            follow = Undo.AddComponent<IsometricCameraFollow>(camera.gameObject);
        SerializedObject so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player;
        so.FindProperty("positionOffset").vector3Value = new Vector3(22f, 31f, -22f);
        so.FindProperty("focusOffset").vector3Value = new Vector3(8f, 0.5f, 5f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Vector3 initialFocus = player.position + new Vector3(8f, 0.5f, 5f);
        camera.transform.position = initialFocus + new Vector3(22f, 31f, -22f);
        camera.transform.rotation = Quaternion.LookRotation(initialFocus - camera.transform.position,
            Vector3.up);
        camera.cullingMask &= ~(1 << MarkerLayer);
    }

    private static void ApplyMapPalette()
    {
        Material wall = CreateFlatMaterial("ReferenceWall.mat", new Color(0.72f, 0.72f, 0.69f, 1f));
        Material floor = CreateFloorMaterial();
        Material droneMaterial = CreateTintedMaterial("ReferenceDrone.mat",
            "Assets/Materials/Drone.mat", new Color(0.67f, 0.69f, 0.70f, 1f));
        Material visionMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VisionCone.mat");
        if (visionMaterial != null)
        {
            Color coneAmber = new Color(1f, 0.48f, 0.12f, 0.38f);
            if (visionMaterial.HasProperty("_BaseColor")) visionMaterial.SetColor("_BaseColor", coneAmber);
            if (visionMaterial.HasProperty("_Color")) visionMaterial.SetColor("_Color", coneAmber);
            EditorUtility.SetDirty(visionMaterial);
        }

        GameObject floorObject = GameObject.Find("Floor");
        if (floorObject != null && floorObject.TryGetComponent<Renderer>(out Renderer floorRenderer))
            floorRenderer.sharedMaterial = floor;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.StartsWith("Wall_") || root.name.StartsWith("Obstacle_"))
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = wall;
            }

            if (root.name == "SecurityDrone" || root.name == "DroneVisual")
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.name.Contains("Drone"))
                        renderer.sharedMaterial = droneMaterial;
            }
        }

        Light key = Object.FindAnyObjectByType<Light>();
        if (key != null && key.type == LightType.Directional)
        {
            key.color = new Color(1f, 0.96f, 0.86f, 1f);
            key.intensity = 1.15f;
        }
    }

    private static Material CreateTintedMaterial(string fileName, string sourcePath, Color tint)
    {
        string path = AssetFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
                throw new System.InvalidOperationException("Missing palette source material: " + sourcePath);
            material = new Material(source) { name = Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateFlatMaterial(string fileName, Color tint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new System.InvalidOperationException("URP/Lit shader is unavailable.");

        string path = AssetFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(fileName) };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateFloorMaterial()
    {
        Material material = CreateFlatMaterial("ReferenceFloor.mat", Color.white);
        string texturePath = AssetFolder + "/ReferenceFloorGrid.png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            const int size = 64;
            Texture2D generated = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            Color32 tile = new Color32(220, 218, 204, 255);
            Color32 grid = new Color32(166, 171, 167, 255);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = x < 3 || y < 3 ? grid : tile;
            generated.SetPixels32(pixels);
            generated.Apply(true, false);
            File.WriteAllBytes(Path.GetFullPath(texturePath), generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", new Vector2(12f, 8f));
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildSegmentedAlert(Transform hud)
    {
        Transform panel = FindChild(hud, "AlertPanel");
        if (panel == null) return;
        SetRect(panel as RectTransform, new Vector2(0, 1), new Vector2(28, -24),
            new Vector2(520, 164), new Vector2(0, 1));
        StylePanel(panel, new Color(0.015f, 0.025f, 0.035f, 0.70f));

        TMP_Text title = FindNamedDescendant(panel, "AlertTitle")?.GetComponent<TMP_Text>();
        StyleText(title, "ALERT METER", 24, TextAlignmentOptions.Left, White);
        if (title != null)
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(16, -12), new Vector2(480, 34),
                new Vector2(0, 1));

        Transform background = FindNamedDescendant(panel, "AlertBarBackground");
        if (background == null)
            background = CreateImage(panel, "AlertBarBackground", new Vector2(16, -52),
                new Vector2(488, 54), new Vector2(0, 1), new Color(0.05f, 0.07f, 0.09f, 1f)).transform;
        SetRect(background as RectTransform, new Vector2(0, 1), new Vector2(16, -54),
            new Vector2(488, 54), new Vector2(0, 1));
        Image backgroundImage = background.GetComponent<Image>();
        if (backgroundImage != null) backgroundImage.color = new Color(0.06f, 0.08f, 0.10f, 1f);

        Transform oldFill = FindChild(background, "AlertBarFill");
        if (oldFill != null) oldFill.gameObject.SetActive(false);

        Transform segments = FindChild(background, "AlertSegments");
        if (segments != null) Object.DestroyImmediate(segments.gameObject);
        segments = new GameObject("AlertSegments", typeof(RectTransform)).transform;
        segments.SetParent(background, false);
        SetStretch(segments as RectTransform, 8f, 8f);

        const int count = 10;
        const float gap = 4f;
        float segmentWidth = (472f - gap * (count - 1)) / count;
        Image[] fills = new Image[count];
        for (int i = 0; i < count; i++)
        {
            GameObject slot = new GameObject("AlertSegment_" + (i + 1).ToString("00"),
                typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(segments, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            SetRect(slotRect, new Vector2(0, 0.5f), new Vector2(i * (segmentWidth + gap), 0),
                new Vector2(segmentWidth, 38), new Vector2(0, 0.5f));
            slot.GetComponent<Image>().sprite = null;
            slot.GetComponent<Image>().color = new Color(0.17f, 0.20f, 0.22f, 1f);
            slot.GetComponent<Image>().raycastTarget = false;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(slot.transform, false);
            SetStretch(fill.GetComponent<RectTransform>(), 2f, 2f);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = null;
            fillImage.color = new Color(0.17f, 0.20f, 0.22f, 1f);
            fillImage.raycastTarget = false;
            fills[i] = fillImage;
        }

        TMP_Text status = FindChild(panel, "DetectionStatus")?.GetComponent<TMP_Text>();
        if (status == null)
            status = CreateText(panel, "DetectionStatus", "DETECTION: LOW", 18,
                new Vector2(16, -116), new Vector2(360, 28), new Vector2(0, 1), Cyan,
                TextAlignmentOptions.Left);
        StyleText(status, "DETECTION: LOW", 18, TextAlignmentOptions.Left, Cyan);
        SetRect(status.rectTransform, new Vector2(0, 1), new Vector2(16, -118),
            new Vector2(320, 28), new Vector2(0, 1));

        TMP_Text percent = FindChild(panel, "DetectionPercent")?.GetComponent<TMP_Text>();
        if (percent == null)
            percent = CreateText(panel, "DetectionPercent", "0%", 19,
                Vector2.zero, new Vector2(72, 32), new Vector2(0.5f, 0.5f), White,
                TextAlignmentOptions.Center);
        StyleText(percent, "0%", 19, TextAlignmentOptions.Center, White);
        SetRect(percent.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(76, 34), new Vector2(0.5f, 0.5f));

        HUDController controller = hud.GetComponent<HUDController>();
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty array = so.FindProperty("alertSegments");
        array.arraySize = fills.Length;
        for (int i = 0; i < fills.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
        so.FindProperty("detectionPercentText").objectReferenceValue = percent;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildCircularNoiseGauge(Transform hud, Sprite ringSprite)
    {
        Transform panel = FindChild(hud, "NoisePanel");
        if (panel == null) return;
        SetRect(panel as RectTransform, new Vector2(1, 1), new Vector2(-26, -20),
            new Vector2(310, 334), new Vector2(1, 1));
        StylePanel(panel, new Color(0f, 0f, 0f, 0f));

        TMP_Text title = FindChild(panel, "NoiseTitle")?.GetComponent<TMP_Text>();
        StyleText(title, "NOISE LEVEL", 23, TextAlignmentOptions.Right, White);
        if (title != null)
            SetRect(title.rectTransform, new Vector2(1, 1), new Vector2(-16, -8),
                new Vector2(278, 32), new Vector2(1, 1));

        Transform bar = FindChild(panel, "NoiseBar");
        Image ringBackground = bar != null ? bar.GetComponent<Image>() : null;
        Image ringFill = FindNamedDescendant(panel, "NoiseBarFill")?.GetComponent<Image>();
        if (bar == null || ringFill == null) return;

        SetRect(bar as RectTransform, new Vector2(0.5f, 1), new Vector2(0, -52),
            new Vector2(226, 226), new Vector2(0.5f, 1));
        if (ringBackground != null)
        {
            ringBackground.sprite = ringSprite;
            ringBackground.type = Image.Type.Simple;
            ringBackground.color = new Color(0.45f, 0.49f, 0.52f, 0.70f);
            ringBackground.raycastTarget = false;
        }
        SetStretch(ringFill.rectTransform, 0f, 0f);
        ringFill.sprite = ringSprite;
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.fillAmount = 0f;
        ringFill.color = Green;
        ringFill.raycastTarget = false;

        Transform baseline = FindChild(bar, "NoiseBaseline");
        if (baseline == null)
            baseline = CreateImage(bar, "NoiseBaseline", Vector2.zero,
                new Vector2(178, 3), new Vector2(0.5f, 0.5f),
                new Color(0.20f, 0.90f, 0.42f, 0.9f)).transform;
        SetRect(baseline as RectTransform, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(178, 3), new Vector2(0.5f, 0.5f));

        TMP_Text status = FindChild(panel, "DecibelText")?.GetComponent<TMP_Text>();
        if (status != null)
        {
            StyleText(status, "LOW", 26, TextAlignmentOptions.Center, White);
            SetRect(status.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -151),
                new Vector2(160, 40), new Vector2(0.5f, 1));
        }

        TMP_Text relative = FindChild(panel, "NoiseSignalLabel")?.GetComponent<TMP_Text>();
        if (relative != null)
        {
            StyleText(relative, "RELATIVE SOUND REACH", 14, TextAlignmentOptions.Center, White);
            SetRect(relative.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -292),
                new Vector2(278, 24), new Vector2(0.5f, 1));
        }

        Transform icon = FindChild(panel, "NoiseIcon");
        if (icon == null)
            icon = CreateButton(panel, "NoiseIcon", ")))" , ringSprite,
                new Vector2(-30, -49), new Vector2(54, 54), new Vector2(1, 1), 14f).transform;
        icon.GetComponent<Button>().interactable = false;
        StyleText(FindNamedDescendant(icon, "NoiseIconLabel")?.GetComponent<TMP_Text>(),
            ")))" , 14, TextAlignmentOptions.Center, White);
    }

    private static void BuildObjectiveChip(Transform hud)
    {
        Transform panel = FindChild(hud, "ObjectivePanel");
        if (panel == null) return;
        SetRect(panel as RectTransform, Vector2.zero, new Vector2(28, 184),
            new Vector2(520, 96), Vector2.zero);
        StylePanel(panel, new Color(0.015f, 0.025f, 0.035f, 0.70f));

        Transform icon = FindChild(panel, "ObjectiveIcon");
        if (icon != null)
            SetRect(icon as RectTransform, new Vector2(0, 0.5f), new Vector2(14, 0),
                new Vector2(48, 48), new Vector2(0, 0.5f));
        Transform glyph = FindChild(panel, "ObjectiveIconGlyph");
        if (glyph != null)
            SetRect(glyph as RectTransform, new Vector2(0, 0.5f), new Vector2(14, 0),
                new Vector2(48, 48), new Vector2(0, 0.5f));

        TMP_Text objective = FindChild(panel, "ObjectiveText")?.GetComponent<TMP_Text>();
        if (objective != null)
        {
            StyleText(objective, "DATA CORE   //   NOT SECURED\nEXTRACTION COORDINATES PENDING", 17,
                TextAlignmentOptions.Left, White);
            objective.textWrappingMode = TextWrappingModes.Normal;
            SetRect(objective.rectTransform, new Vector2(0, 0.5f), new Vector2(70, 0),
                new Vector2(430, 76), new Vector2(0, 0.5f));
        }

        SerializedObject so = new SerializedObject(hud.GetComponent<HUDController>());
        so.FindProperty("objectivePanel").objectReferenceValue = panel.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildCircularMinimap(Transform hud, Sprite circleSprite, Sprite ringSprite)
    {
        Transform panel = FindChild(hud, "MinimapPanel");
        if (panel == null) return;
        SetRect(panel as RectTransform, new Vector2(1, 0), new Vector2(-24, 24),
            new Vector2(300, 340), new Vector2(1, 0));
        StylePanel(panel, new Color(0f, 0f, 0f, 0f));

        TMP_Text title = FindChild(panel, "MinimapTitle")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            StyleText(title, "EXTRACTION MINI-MAP", 16, TextAlignmentOptions.Center, White);
            SetRect(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -6),
                new Vector2(260, 28), new Vector2(0.5f, 1));
        }

        // Find the existing display recursively because repeated application
        // nests it below our circular mask. Detach it before removing that mask.
        Transform map = FindNamedDescendant(panel, "MinimapDisplay");
        if (map == null) return;
        RawImage raw = map.GetComponent<RawImage>();
        Transform oldMask = FindChild(panel, "MinimapCircleMask");
        if (oldMask != null)
        {
            map.SetParent(panel, false);
            Object.DestroyImmediate(oldMask.gameObject);
        }

        GameObject maskObject = new GameObject("MinimapCircleMask", typeof(RectTransform),
            typeof(Image), typeof(Mask));
        maskObject.layer = panel.gameObject.layer;
        maskObject.transform.SetParent(panel, false);
        SetRect(maskObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0),
            new Vector2(0, 12), new Vector2(246, 246), new Vector2(0.5f, 0));
        Image maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = circleSprite;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        maskObject.GetComponent<Mask>().showMaskGraphic = false;

        map.SetParent(maskObject.transform, false);
        map.localScale = Vector3.one;
        SetStretch(map as RectTransform, 0f, 0f);
        if (raw != null)
        {
            raw.color = Color.white;
            raw.raycastTarget = false;
        }

        Transform border = FindChild(panel, "MinimapCircleBorder");
        if (border == null)
        {
            Image borderImage = CreateImage(panel, "MinimapCircleBorder", new Vector2(0, 12),
                new Vector2(262, 262), new Vector2(0.5f, 0), Color.white);
            border = borderImage.transform;
        }
        SetRect(border as RectTransform, new Vector2(0.5f, 0), new Vector2(0, 12),
            new Vector2(262, 262), new Vector2(0.5f, 0));
        Image ring = border.GetComponent<Image>();
        ring.sprite = ringSprite;
        ring.color = new Color(0.73f, 0.78f, 0.78f, 0.94f);
        ring.raycastTarget = false;
        border.SetAsLastSibling();

        TMP_Text legend = FindChild(panel, "MinimapLegend")?.GetComponent<TMP_Text>();
        if (legend != null)
        {
            StyleText(legend, "YOU     DRONE     EXIT PENDING", 13,
                TextAlignmentOptions.Center, White);
            SetRect(legend.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -22),
                new Vector2(280, 20), new Vector2(0.5f, 0));
        }
    }

    private static void BuildTouchControls(Transform hud, Sprite circleSprite, Sprite ringSprite)
    {
        Transform controls = FindChild(hud, "MobileControlsPanel");
        if (controls == null)
            controls = new GameObject("MobileControlsPanel", typeof(RectTransform)).transform;
        controls.SetParent(hud, false);
        controls.gameObject.layer = hud.gameObject.layer;
        SetRect(controls as RectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero);

        Transform throwButton = FindChild(controls, "ThrowButton");
        if (throwButton == null)
            throwButton = CreateButton(controls, "ThrowButton", "THROW", circleSprite,
                new Vector2(28, 28), new Vector2(138, 138), Vector2.zero, 18f).transform;
        SetRect(throwButton as RectTransform, Vector2.zero, new Vector2(28, 28),
            new Vector2(138, 138), Vector2.zero);
        StyleButton(throwButton, ringSprite);

        Transform objectivesButton = FindChild(controls, "ObjectivesButton");
        if (objectivesButton == null)
            objectivesButton = CreateButton(controls, "ObjectivesButton", "OBJ", circleSprite,
                new Vector2(184, 30), new Vector2(110, 110), Vector2.zero, 17f).transform;
        SetRect(objectivesButton as RectTransform, Vector2.zero, new Vector2(184, 30),
            new Vector2(110, 110), Vector2.zero);
        StyleButton(objectivesButton, ringSprite);

        HUDController controller = hud.GetComponent<HUDController>();
        Button objectives = objectivesButton.GetComponent<Button>();
        objectives.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(objectives.onClick, controller.ToggleObjectivePanel);
    }

    private static GameObject CreateButton(Transform parent, string name, string label, Sprite circle,
        Vector2 position, Vector2 size, Vector2 anchor, float fontSize)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);
        SetRect(buttonObject.GetComponent<RectTransform>(), anchor, position, size, anchor);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = circle;
        image.color = new Color(0.045f, 0.07f, 0.08f, 0.92f);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Image ring = CreateImage(buttonObject.transform, name + "Ring", Vector2.zero, Vector2.zero,
            Vector2.zero, new Color(0.75f, 0.81f, 0.81f, 0.92f));
        SetStretch(ring.rectTransform, 0f, 0f);
        ring.sprite = GetCircleSprite("CircleRing.png", true);

        CreateText(buttonObject.transform, name + "Label", label, fontSize,
            Vector2.zero, Vector2.one, Vector2.zero, White, TextAlignmentOptions.Center, true);
        return buttonObject;
    }

    private static void StyleButton(Transform button, Sprite ringSprite)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = GetCircleSprite("CircleSolid.png", false);
            image.color = new Color(0.045f, 0.07f, 0.08f, 0.92f);
            image.raycastTarget = true;
        }

        Transform ring = FindChild(button, button.name + "Ring");
        if (ring != null)
        {
            ring.GetComponent<Image>().sprite = ringSprite;
            SetStretch(ring as RectTransform, 0f, 0f);
        }
    }

    private static void ConfigureHudReferences(HUDController controller, Transform hud)
    {
        SerializedObject so = new SerializedObject(controller);
        Transform panel = FindChild(hud, "AlertPanel");
        Transform bar = FindNamedDescendant(panel, "AlertBarFill");
        if (bar != null) so.FindProperty("alertBarFill").objectReferenceValue = bar.GetComponent<Image>();
        SetReference(so, "detectionStatus", FindChild(panel, "DetectionStatus")?.GetComponent<TMP_Text>());
        SetReference(so, "detectionPercentText", FindChild(panel, "DetectionPercent")?.GetComponent<TMP_Text>());
        SetReference(so, "noiseBarFill", FindNamedDescendant(FindChild(hud, "NoisePanel"), "NoiseBarFill")?.GetComponent<Image>());
        SetReference(so, "noiseStatus", FindChild(FindChild(hud, "NoisePanel"), "DecibelText")?.GetComponent<TMP_Text>());
        SetReference(so, "noiseSignalLabel", FindChild(FindChild(hud, "NoisePanel"), "NoiseSignalLabel")?.GetComponent<TMP_Text>());
        SetReference(so, "objectivePanel", FindChild(hud, "ObjectivePanel")?.gameObject);
        SetReference(so, "objectiveText", FindChild(FindChild(hud, "ObjectivePanel"), "ObjectiveText")?.GetComponent<TMP_Text>());

        SerializedProperty segments = so.FindProperty("alertSegments");
        Transform track = FindChild(FindNamedDescendant(panel, "AlertBarBackground"), "AlertSegments");
        segments.arraySize = track != null ? track.childCount : 0;
        for (int i = 0; i < segments.arraySize; i++)
            segments.GetArrayElementAtIndex(i).objectReferenceValue =
                track.GetChild(i).Find("Fill")?.GetComponent<Image>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureThrowTouch(GameObject player, Transform hud)
    {
        ThrowController throwController = player.GetComponent<ThrowController>();
        Transform button = FindNamedDescendant(hud, "ThrowButton");
        if (throwController == null || button == null) return;
        SerializedObject so = new SerializedObject(throwController);
        SetReference(so, "mobileThrowButton", button.GetComponent<RectTransform>());
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("Mobile EventSystem", typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void ConfigureRipplePrefab()
    {
        const string prefabPath = "Assets/Prefabs/VFX/SoundRipple.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SoundRipple ripple = prefabRoot.GetComponent<SoundRipple>();
            if (ripple == null) return;

            SerializedObject so = new SerializedObject(ripple);
            so.FindProperty("maxRadius").floatValue = 6f;
            so.FindProperty("duration").floatValue = 1.2f;
            so.FindProperty("segments").intValue = 64;
            so.FindProperty("lineWidth").floatValue = 0.07f;
            so.FindProperty("ringCount").intValue = 3;
            so.FindProperty("ringDelay").floatValue = 0.18f;
            so.FindProperty("rippleColor").colorValue = new Color(0.18f, 1f, 0.36f, 0.78f);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Sprite GetCircleSprite(string fileName, bool ring)
    {
        string path = AssetFolder + "/" + fileName;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.name = Path.GetFileNameWithoutExtension(fileName);
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float outer = size * 0.47f;
        float inner = ring ? size * 0.42f : 0f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - center;
            float dy = y - center;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            byte alpha = (byte)(distance <= outer && (!ring || distance >= inner) ? 255 : 0);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name)
                return parent.GetChild(i);
        return null;
    }

    private static Transform FindNamedDescendant(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindNamedDescendant(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 position,
        Vector2 size, Vector2 anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), anchor, position, size, anchor);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string content,
        float size, Vector2 position, Vector2 rectSize, Vector2 anchor, Color color,
        TextAlignmentOptions alignment, bool stretch = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        if (stretch)
            SetStretch(rect, 8f, 8f);
        else
            SetRect(rect, anchor, position, rectSize, anchor);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void StyleText(TMP_Text text, string content, float size,
        TextAlignmentOptions alignment, Color color)
    {
        if (text == null) return;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static void StylePanel(Transform panel, Color color)
    {
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
            image.raycastTarget = true;
        }
    }

    private static void SetRect(RectTransform rect, Vector2 anchor,
        Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rect, float horizontalInset, float verticalInset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(horizontalInset, verticalInset);
        rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        rect.localScale = Vector3.one;
    }

    private static void SetReference(SerializedObject so, string name, Object reference)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.objectReferenceValue = reference;
    }
}

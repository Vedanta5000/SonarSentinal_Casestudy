using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the mobile title screen in the existing GameHUD canvas.</summary>
public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/GreyboxRoom.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly Color Cyan = new Color(0.12f, 0.92f, 0.82f, 1f);
    private static readonly Color White = new Color(0.91f, 0.96f, 0.96f, 1f);

    [MenuItem("Sonar Sentinel/Build Title Screen")]
    public static void BuildCurrentScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject hud = GameObject.Find("GameHUD");
        if (hud == null)
            throw new System.InvalidOperationException("GreyboxRoom needs its GameHUD canvas before the title screen can be built.");

        ApplyTo(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Sonar Sentinel title screen added to GreyboxRoom.");
    }

    public static void ApplyTo(GameObject canvasObject)
    {
        Transform oldPanel = canvasObject.transform.Find("MainMenuPanel");
        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);

        MainMenuController controller = canvasObject.GetComponent<MainMenuController>();
        if (controller == null)
            controller = Undo.AddComponent<MainMenuController>(canvasObject);

        GameObject panelObject = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
        panelObject.layer = canvasObject.layer;
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        SetStretch(panelRect);
        Image backdrop = panelObject.GetComponent<Image>();
        backdrop.color = new Color(0.015f, 0.035f, 0.052f, 0.94f);
        backdrop.raycastTarget = true;

        CreateAccent(panelObject.transform, "TopAccent", new Vector2(0.5f, 0.5f),
            new Vector2(0f, 156f), new Vector2(250f, 3f), Cyan);
        TMP_Text title = CreateText(panelObject.transform, "GameTitle", "SONAR SENTINEL",
            82f, new Vector2(0f, 92f), new Vector2(1000f, 122f), White,
            TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 6f;

        TMP_Text subtitle = CreateText(panelObject.transform, "TitleSubtitle",
            "ACOUSTIC STEALTH  /  TACTICAL INFILTRATION", 25f,
            new Vector2(0f, 22f), new Vector2(850f, 44f), Cyan,
            TextAlignmentOptions.Center);
        subtitle.characterSpacing = 2f;

        GameObject buttonObject = new GameObject("PlayButton", typeof(RectTransform),
            typeof(Image), typeof(Button));
        buttonObject.layer = canvasObject.layer;
        buttonObject.transform.SetParent(panelObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetCentered(buttonRect, new Vector2(0f, -105f), new Vector2(360f, 92f));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.83f, 0.76f, 1f);
        buttonImage.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.83f, 0.76f, 1f);
        colors.highlightedColor = new Color(0.25f, 1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.04f, 0.57f, 0.55f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text playLabel = CreateText(buttonObject.transform, "PlayLabel", "PLAY",
            34f, Vector2.zero, new Vector2(330f, 70f), new Color(0.01f, 0.08f, 0.09f, 1f),
            TextAlignmentOptions.Center);
        playLabel.fontStyle = FontStyles.Bold;
        playLabel.characterSpacing = 5f;
        UnityEventTools.AddPersistentListener(button.onClick,
            canvasObject.GetComponent<MainMenuController>().StartGame);

        CreateAccent(panelObject.transform, "BottomAccent", new Vector2(0.5f, 0.5f),
            new Vector2(0f, -184f), new Vector2(150f, 2f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.48f));
        TMP_Text hint = CreateText(panelObject.transform, "StartHint", "TAP PLAY TO BEGIN",
            16f, new Vector2(0f, -226f), new Vector2(400f, 28f),
            new Color(0.64f, 0.76f, 0.77f, 1f), TextAlignmentOptions.Center);
        hint.characterSpacing = 3f;

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("titleScreen").objectReferenceValue = panelObject;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("Mobile EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }

        panelObject.transform.SetAsLastSibling();
    }

    private static TMP_Text CreateText(Transform parent, string name, string value,
        float fontSize, Vector2 position, Vector2 size, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        SetCentered(textObject.GetComponent<RectTransform>(), position, size);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateAccent(Transform parent, string name, Vector2 anchor,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject accent = new GameObject(name, typeof(RectTransform), typeof(Image));
        accent.layer = parent.gameObject.layer;
        accent.transform.SetParent(parent, false);
        SetAnchored(accent.GetComponent<RectTransform>(), anchor, position, size);
        Image image = accent.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        SetAnchored(rect, new Vector2(0.5f, 0.5f), position, size);
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}

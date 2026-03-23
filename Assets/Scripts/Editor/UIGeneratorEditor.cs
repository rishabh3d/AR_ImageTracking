using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIGeneratorEditor : EditorWindow
{
    [MenuItem("AR Tracker/Generate New UI")]
    public static void ShowWindow()
    {
        GetWindow<UIGeneratorEditor>("UI Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("New UI Setup", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Generate Standard AR UI"))
        {
            GenerateUI();
        }
    }

    private void GenerateUI()
    {
        // 1. Ensure EventSystem exists
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 2. Create the Canvas
        GameObject canvasObj = new GameObject("AR_MainCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Create Main Menu Panel
        GameObject mainMenuPanel = CreatePanel("MainMenuPanel", canvasObj.transform, new Color(0.1f, 0.1f, 0.1f, 0.9f));
        GameObject titleText = CreateText("TitleText", mainMenuPanel.transform, "AR Experience", 80, new Vector2(0, 300));
        GameObject startBtnObj = CreateButton("StartButton", mainMenuPanel.transform, "Start Experience", new Vector2(0, -100));

        // 4. Create AR View Panel
        GameObject arPanel = CreatePanel("ARViewPanel", canvasObj.transform, new Color(0, 0, 0, 0f)); // Transparent
        arPanel.SetActive(false); // Hidden by default
        GameObject backBtnObj = CreateButton("BackButton", arPanel.transform, "Back to Menu", new Vector2(-400, 800)); // Top leftish
        
        // 5. Create UIManager and hook it up
        GameObject managerObj = new GameObject("AR_UIManager");
        UIManager uiManager = managerObj.AddComponent<UIManager>();
        uiManager.firstCanvas = mainMenuPanel;
        uiManager.arCanvas = arPanel;
        uiManager.startButton = startBtnObj.GetComponent<Button>();
        uiManager.backButton = backBtnObj.GetComponent<Button>();
        
        Debug.Log("Successfully customized and implemented the New UI setup! Attached UIManager successfully.");
    }

    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = color;

        return panel;
    }

    private GameObject CreateText(string name, Transform parent, string text, int fontSize, Vector2 anchoredPosition)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        Text t = textObj.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800, 150);
        rect.anchoredPosition = anchoredPosition;
        return textObj;
    }

    private GameObject CreateButton(string name, Transform parent, string textStr, Vector2 anchoredPosition)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 1f); // Nice blue
        
        Button btn = btnObj.AddComponent<Button>();

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 100);
        rect.anchoredPosition = anchoredPosition;

        GameObject textObj = CreateText(name + "_Text", btnObj.transform, textStr, 40, Vector2.zero);
        textObj.GetComponent<RectTransform>().sizeDelta = rect.sizeDelta;

        return btnObj;
    }
}

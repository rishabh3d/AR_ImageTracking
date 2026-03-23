using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Canvases / Panels")]
    public GameObject firstCanvas; // E.g., Main Menu Canvas
    public GameObject arCanvas;    // E.g., AR View Canvas

    [Header("Buttons")]
    public Button contectButton;
    public Button startButton;
    public Button backButton;

    void Start()
    {
        // Initialize listeners
        if (contectButton != null)
            contectButton.onClick.AddListener(OpenURL);
            
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
            
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // Initial state
        ShowMainCanvas();
    }

    void OnStartClicked()
    {
        // Hide Main Menu, Show AR View
        if (firstCanvas != null) firstCanvas.SetActive(false);
        if (arCanvas != null) arCanvas.SetActive(true);
    }

    void OnBackClicked()
    {
        // Hide AR View, Show Main Menu
        ShowMainCanvas();
    }

    void ShowMainCanvas()
    {
        if (firstCanvas != null) firstCanvas.SetActive(true);
        if (arCanvas != null) arCanvas.SetActive(false);
    }

    void OpenURL()
    {
        Application.OpenURL("https://rionick.com/contact-us");
    }
}

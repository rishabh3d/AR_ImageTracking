using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Imagine.WebAR.Samples
{
    public class DemoSceneLoader : MonoBehaviour
    {
        public GameObject firstPanel;
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public void PanelLoad(string panelName)
        {
           if(firstPanel != null)
           {
               firstPanel.SetActive(panelName == "FirstPanel");
           }
        }
    }
}


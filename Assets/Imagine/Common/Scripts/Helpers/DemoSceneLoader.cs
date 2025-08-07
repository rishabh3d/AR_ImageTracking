using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DemoSceneLoader : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CleanupWebGLMemory();
#endif

    public void LoadScene(string sceneName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CleanupWebGLMemory(); // JS-side cleanup
#endif
        SceneManager.LoadScene(sceneName);
    }
}
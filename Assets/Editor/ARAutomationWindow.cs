using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Imagine.WebAR;
using System.Collections.Generic;

public class ARAutomationWindow : EditorWindow
{
    private string targetId = "NewTarget";
    private Texture2D imageTexture;
    private string cdnVideoUrl = "https://";
    private string sceneName = "AutoScene_1";
    
    private Mesh customMesh;
    private bool isGreenScreen = false;
    private Texture2D firstFrameTexture;
    private Mesh videoMesh;           // separate mesh for the green screen video layer
    private Vector2 videoSize = new Vector2(1f, 1f); // manual scale for video layer if no videoMesh

    [MenuItem("Tools/AR Setup Automation")]
    public static void ShowWindow()
    {
        GetWindow<ARAutomationWindow>("AR Setup Wizard");
    }

    private void OnGUI()
    {
        GUILayout.Label("AR Video Scene Generator", EditorStyles.boldLabel);
        GUILayout.Label("This tool duplicates the Demo-Video scene, sets up the CDN link,\nregisters the Image Target, and adds the scene to Build Settings.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        sceneName = EditorGUILayout.TextField("New Scene Name", sceneName);
        targetId = EditorGUILayout.TextField("Target ID (No Spaces)", targetId);
        imageTexture = (Texture2D)EditorGUILayout.ObjectField("Target Image", imageTexture, typeof(Texture2D), false);
        cdnVideoUrl = EditorGUILayout.TextField("CDN Video URL", cdnVideoUrl);
        
        GUILayout.Space(10);
        GUILayout.Label("Advanced Video Settings", EditorStyles.boldLabel);
        customMesh = (Mesh)EditorGUILayout.ObjectField("Custom Mesh (Optional)", customMesh, typeof(Mesh), false);
        
        isGreenScreen = EditorGUILayout.Toggle("Is Green Screen Video?", isGreenScreen);
        if (isGreenScreen)
        {
            firstFrameTexture = (Texture2D)EditorGUILayout.ObjectField("First Frame Image", firstFrameTexture, typeof(Texture2D), false);
            
            EditorGUILayout.Space(4);
            GUILayout.Label("Video Layer Size (independent from tracking image)", EditorStyles.miniLabel);
            videoMesh = (Mesh)EditorGUILayout.ObjectField("  Video Mesh (Optional)", videoMesh, typeof(Mesh), false);
            if (videoMesh == null)
            {
                videoSize = EditorGUILayout.Vector2Field("  Video Scale (W x H)", videoSize);
                EditorGUILayout.HelpBox("If no Video Mesh is set, the video layer will use these W/H scale values on a default Quad.", MessageType.Info);
            }
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Create Scene & Setup AR", GUILayout.Height(40)))
        {
            SetupScene();
        }
    }

    private void SetupScene()
    {
        if (string.IsNullOrEmpty(targetId) || imageTexture == null || string.IsNullOrEmpty(cdnVideoUrl) || string.IsNullOrEmpty(sceneName))
        {
            EditorUtility.DisplayDialog("Error", "Please fill in all fields (Scene Name, Target ID, Image, and CDN URL).", "OK");
            return;
        }

        // 1. Add/Update Target in Global Settings
        var globalSettings = Resources.Load<ImageTrackerGlobalSettings>("ImageTrackerGlobalSettings");
        if (globalSettings != null)
        {
            bool found = false;
            if (globalSettings.imageTargetInfos == null)
            {
                globalSettings.imageTargetInfos = new List<ImageTargetInfo>();
            }
            foreach (var info in globalSettings.imageTargetInfos)
            {
                if (info.id == targetId)
                {
                    info.texture = imageTexture;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                globalSettings.imageTargetInfos.Add(new ImageTargetInfo { id = targetId, texture = imageTexture });
            }
            EditorUtility.SetDirty(globalSettings);
            AssetDatabase.SaveAssets();
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", "Could not find ImageTrackerGlobalSettings in Resources. You may need to register the image manually.", "OK");
        }

        // 2. Duplicate Demo-Video.unity
        string templatePath = "Assets/Scenes_1/Demo-Video.unity";
        string newScenePath = "Assets/Scenes_1/" + sceneName + ".unity";

        if (!System.IO.File.Exists(templatePath))
        {
            EditorUtility.DisplayDialog("Error", "Template scene not found at: " + templatePath + "\nPlease create a working template scene first.", "OK");
            return;
        }

        bool copySuccess = AssetDatabase.CopyAsset(templatePath, newScenePath);
        if (!copySuccess)
        {
            EditorUtility.DisplayDialog("Error", "Failed to duplicate template scene.", "OK");
            return;
        }

        // Open the new scene
        Scene newScene = EditorSceneManager.OpenScene(newScenePath, OpenSceneMode.Single);

        // 3. Find ImageTracker and update Target ID
#if UNITY_2023_1_OR_NEWER
        var trackerScript = Object.FindFirstObjectByType<ImageTracker>(FindObjectsInactive.Include);
#else
        var trackerScript = Object.FindObjectOfType<ImageTracker>(true);
#endif

        if (trackerScript != null)
        {
            var prop = new SerializedObject(trackerScript);
            var imageTargetsProp = prop.FindProperty("imageTargets");
            if (imageTargetsProp != null && imageTargetsProp.arraySize > 0)
            {
                Transform templateTransform = (Transform)imageTargetsProp.GetArrayElementAtIndex(0).FindPropertyRelative("transform").objectReferenceValue;

                // Delete all other target transforms
                for (int i = 1; i < imageTargetsProp.arraySize; i++)
                {
                    Transform t = (Transform)imageTargetsProp.GetArrayElementAtIndex(i).FindPropertyRelative("transform").objectReferenceValue;
                    if (t != null && t.gameObject != null)
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }

                if (templateTransform != null)
                {
                    templateTransform.name = targetId;
                }

                imageTargetsProp.arraySize = 1;
                var element = imageTargetsProp.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("id").stringValue = targetId;
                prop.ApplyModifiedProperties();
            }
        }
        else
        {
            Debug.LogWarning("Could not find ImageTracker in the scene. Make sure your template has one.");
        }

        // 4. Find CDNARVideoController AFTER deleting others, so we get the remaining one
#if UNITY_2023_1_OR_NEWER
        var cdnController = Object.FindFirstObjectByType<CDNARVideoController>(FindObjectsInactive.Include);
#else
        var cdnController = Object.FindObjectOfType<CDNARVideoController>(true);
#endif

        if (cdnController != null)
        {
            // The controller is attached to the CHILD object (e.g. "Modi vid")
            GameObject childObj = cdnController.gameObject;
            GameObject parentObj = childObj.transform.parent.gameObject;

            // Enforce correct naming format
            parentObj.name = targetId;
            childObj.name = targetId + " vid";

            var prop = new SerializedObject(cdnController);
            prop.FindProperty("cdnVideoUrl").stringValue = cdnVideoUrl;
            prop.FindProperty("webGLSoundTargetKey").stringValue = targetId;
            prop.ApplyModifiedProperties();
            
            // Adjust the aspect ratio and setup custom mesh on PARENT
            MeshFilter parentMf = parentObj.GetComponent<MeshFilter>();
            if (parentMf == null) parentMf = parentObj.AddComponent<MeshFilter>();

            if (customMesh != null)
            {
                parentMf.sharedMesh = customMesh;
                parentObj.transform.localScale = Vector3.one;
            }
            else if (imageTexture != null)
            {
                float aspect = (float)imageTexture.width / imageTexture.height;
                parentObj.transform.localScale = new Vector3(aspect, 1, 1);
            }

            Renderer parentRenderer = parentObj.GetComponent<Renderer>();
            if (parentRenderer == null) parentRenderer = parentObj.AddComponent<MeshRenderer>();
            VideoPlayer vp = cdnController.GetComponent<VideoPlayer>();

            string matFolder = "Assets/AR_Assets/Materials";
            if (!System.IO.Directory.Exists(matFolder))
            {
                System.IO.Directory.CreateDirectory(matFolder);
            }

            if (!isGreenScreen)
            {
                // NORMAL VIDEO
                // Create 1 Material for the parent, target image assigned to it
                Material newMat = new Material(Shader.Find("Unlit/Texture"));
                newMat.name = targetId + "_Mat";
                newMat.mainTexture = imageTexture;
                AssetDatabase.CreateAsset(newMat, matFolder + "/" + newMat.name + ".mat");
                
                parentRenderer.sharedMaterial = newMat;

                // Ensure child has NO renderer (if duplicating from a green screen template)
                MeshRenderer childRend = childObj.GetComponent<MeshRenderer>();
                if (childRend != null) DestroyImmediate(childRend);
                MeshFilter childFilt = childObj.GetComponent<MeshFilter>();
                if (childFilt != null) DestroyImmediate(childFilt);

                // Video plays directly on the parent's material
                if (vp != null) vp.targetMaterialRenderer = parentRenderer;
            }
            else
            {
                // GREEN SCREEN VIDEO
                // Material 1: Parent (Target Image Background)
                Material bgMat = new Material(Shader.Find("Unlit/Texture"));
                bgMat.name = targetId + "_BGMat";
                bgMat.mainTexture = imageTexture;
                AssetDatabase.CreateAsset(bgMat, matFolder + "/" + bgMat.name + ".mat");
                parentRenderer.sharedMaterial = bgMat;

                // Material 2: Child (Chroma Key with First Frame Image)
                Shader chromaShader = Shader.Find("Imagine/ChromaKeyCutout");
                Material chromaMat = chromaShader != null ? new Material(chromaShader) : new Material(Shader.Find("Unlit/Transparent"));
                chromaMat.name = targetId + "_ChromaMat";
                if (firstFrameTexture != null) chromaMat.mainTexture = firstFrameTexture;
                
                if (chromaShader != null)
                {
                    chromaMat.SetColor("_MaskCol", Color.green);
                    chromaMat.SetFloat("_Sensitivity", 0.35f);
                    chromaMat.SetFloat("_Cutoff", 0.134f);
                    chromaMat.SetFloat("_Feather", 1f);
                }
                AssetDatabase.CreateAsset(chromaMat, matFolder + "/" + chromaMat.name + ".mat");

                // Ensure child HAS a renderer
                MeshFilter childFilt = childObj.GetComponent<MeshFilter>();
                if (childFilt == null) childFilt = childObj.AddComponent<MeshFilter>();

                // Decide mesh for the video layer independently from the tracking image layer
                if (videoMesh != null)
                {
                    // User provided a separate mesh specifically for the video layer
                    childFilt.sharedMesh = videoMesh;
                    childObj.transform.localScale = Vector3.one; // mesh has baked size
                }
                else
                {
                    // Use the same default Quad as parent but scale it independently
                    childFilt.sharedMesh = parentMf.sharedMesh;
                    // Scale the child relative to the PARENT's local space.
                    // Parent world scale = (aspect, 1, 1). Child localScale divides into that.
                    // So we compute what localScale gives us the desired WORLD size.
                    float parentScaleX = parentObj.transform.localScale.x > 0 ? parentObj.transform.localScale.x : 1f;
                    float parentScaleY = parentObj.transform.localScale.y > 0 ? parentObj.transform.localScale.y : 1f;
                    childObj.transform.localScale = new Vector3(
                        videoSize.x / parentScaleX,
                        videoSize.y / parentScaleY,
                        1f);
                }

                MeshRenderer childRend = childObj.GetComponent<MeshRenderer>();
                if (childRend == null) childRend = childObj.AddComponent<MeshRenderer>();
                childRend.sharedMaterial = chromaMat;

                // Position child slightly in front to avoid Z-fighting
                childObj.transform.localPosition = new Vector3(0, 0, -0.001f);
                childObj.transform.localRotation = Quaternion.identity;

                // Video plays on the child's chroma material
                if (vp != null) vp.targetMaterialRenderer = childRend;
            }
        }
        else
        {
            Debug.LogWarning("Could not find CDNARVideoController in the scene. Make sure your template has one.");
        }

        // Save Scene
        EditorSceneManager.SaveScene(newScene);

        // Add to Build Settings if not already there
        var original = EditorBuildSettings.scenes;
        bool sceneExistsInBuild = false;
        foreach (var s in original)
        {
            if (s.path == newScenePath)
            {
                sceneExistsInBuild = true;
                break;
            }
        }

        if (!sceneExistsInBuild)
        {
            var newScenes = new EditorBuildSettingsScene[original.Length + 1];
            System.Array.Copy(original, newScenes, original.Length);
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(newScenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }

        EditorUtility.DisplayDialog("Success", "AR Scene created successfully!\n\nIt has been added to the Build Settings.\nYou can now hit Build and play.", "Awesome");
    }
}

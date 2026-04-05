using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

#if UNITY_EDITOR
public class CustomPlaneGenerator : EditorWindow
{
    public enum ExportFormat
    {
        OBJ,
        FBX,
        Asset
    }

    public enum PlaneOrientation
    {
        XZ_Horizontal,
        XY_Vertical
    }

    private string objectName = "";
    private float width = 1080f;
    private float length = 1920f;
    private float scaleFactor = 0.001f;
    
    private ExportFormat format = ExportFormat.FBX;
    private PlaneOrientation orientation = PlaneOrientation.XZ_Horizontal;

    [MenuItem("Tools/Custom Plane Generator")]
    public static void ShowWindow()
    {
        GetWindow<CustomPlaneGenerator>("Plane Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Plane Mesh Settings", EditorStyles.boldLabel);

        objectName = EditorGUILayout.TextField(new GUIContent("Name", "Leave empty to auto-generate based on dimensions"), objectName);
        width = EditorGUILayout.FloatField("Width (X)", width);
        length = EditorGUILayout.FloatField("Length/Height (Y or Z)", length);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Halve Size (/ 2)"))
        {
            width /= 2f;
            length /= 2f;
        }
        if (GUILayout.Button("Double Size (* 2)"))
        {
            width *= 2f;
            length *= 2f;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        scaleFactor = EditorGUILayout.FloatField("Scale Multiplier", scaleFactor);
        EditorGUILayout.HelpBox("Scale Multiplier converts your input values into Unity units (meters). Default 0.001 turns 1080 into 1.08 units.", MessageType.Info);

        EditorGUILayout.Space();
        format = (ExportFormat)EditorGUILayout.EnumPopup("Export Format", format);
        orientation = (PlaneOrientation)EditorGUILayout.EnumPopup("Orientation", orientation);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Custom Plane"))
        {
            GeneratePlane();
        }
    }

    private void GeneratePlane()
    {
        string finalName = string.IsNullOrEmpty(objectName) ? $"Size_{width}x{length}" : objectName;
        
        string extension = format == ExportFormat.FBX ? "fbx" : (format == ExportFormat.OBJ ? "obj" : "asset");
        string savePath = EditorUtility.SaveFilePanelInProject("Save Plane Mesh", finalName, extension, "Save the generated plane");
        
        if (string.IsNullOrEmpty(savePath))
        {
            return;
        }

        float finalWidth = width * scaleFactor;
        float finalLength = length * scaleFactor;

        if (format == ExportFormat.OBJ)
        {
            GenerateObjFile(savePath, finalName, finalWidth, finalLength);
        }
        else if (format == ExportFormat.FBX)
        {
            GenerateFbxFile(savePath, finalName, finalWidth, finalLength);
        }
        else
        {
            GenerateAssetFile(savePath, finalName, finalWidth, finalLength);
        }

        AssetDatabase.Refresh();
        Debug.Log($"Generated Plane successfully at: {savePath}");
    }

    private Mesh CreatePlaneMesh(string name, float w, float l)
    {
        float extW = w * 0.5f;
        float extL = l * 0.5f;

        Mesh mesh = new Mesh();
        mesh.name = name;

        Vector3[] vertices;
        Vector3 normal;

        if (orientation == PlaneOrientation.XZ_Horizontal)
        {
            vertices = new Vector3[4]
            {
                new Vector3(-extW, 0, -extL),
                new Vector3(extW, 0, -extL),
                new Vector3(-extW, 0, extL),
                new Vector3(extW, 0, extL)
            };
            normal = new Vector3(0, 1, 0);
        }
        else
        {
            // XY Vertical
            vertices = new Vector3[4]
            {
                new Vector3(-extW, -extL, 0),
                new Vector3(extW, -extL, 0),
                new Vector3(-extW, extL, 0),
                new Vector3(extW, extL, 0)
            };
            normal = new Vector3(0, 0, -1);
        }

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        // Triangles for a single quad
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        
        Vector3[] normals = new Vector3[4] { normal, normal, normal, normal };
        mesh.normals = normals;
        
        mesh.RecalculateBounds();
        return mesh;
    }

    private void GenerateFbxFile(string path, string name, float w, float l)
    {
        Mesh mesh = CreatePlaneMesh(name, w, l);
        
        // Create a temporary GameObject to hold the mesh so we can export it as FBX
        GameObject tempObj = new GameObject(name);
        MeshFilter mf = tempObj.AddComponent<MeshFilter>();
        MeshRenderer mr = tempObj.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        
        // Default material so the FBX isn't completely bare
        mr.sharedMaterial = new Material(Shader.Find("Standard"));

        // Use UnityEditor.Formats.Fbx.Exporter via reflection to avoid hard compile dependency errors if package is still importing
        System.Type exporterType = System.Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
        
        if (exporterType != null)
        {
            System.Reflection.MethodInfo exportMethod = exporterType.GetMethod("ExportObject", new System.Type[] { typeof(string), typeof(UnityEngine.Object) });
            if (exportMethod != null)
            {
                exportMethod.Invoke(null, new object[] { path, tempObj });
            }
            else
            {
                Debug.LogError("FBX Exporter method not found! Try generating OBJ or Asset instead.");
            }
        }
        else
        {
            Debug.LogError("FBX Exporter package is not installed or not loaded yet! Please wait for Unity to finish importing the package, or use OBJ format instead.");
        }

        DestroyImmediate(tempObj);
    }

    private void GenerateAssetFile(string path, string name, float w, float l)
    {
        Mesh mesh = CreatePlaneMesh(name, w, l);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }

    private void GenerateObjFile(string path, string name, float w, float l)
    {
        float extW = w * 0.5f;
        float extL = l * 0.5f;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# Generated by Custom Plane Generator");
        sb.AppendLine($"g {name}");
        sb.AppendLine($"o {name}");

        if (orientation == PlaneOrientation.XZ_Horizontal)
        {
            sb.AppendLine($"v {-extW} 0 {-extL}");
            sb.AppendLine($"v {extW} 0 {-extL}");
            sb.AppendLine($"v {-extW} 0 {extL}");
            sb.AppendLine($"v {extW} 0 {extL}");
            sb.AppendLine("vn 0 1 0");
        }
        else
        {
            // XY Vertical
            sb.AppendLine($"v {-extW} {-extL} 0");
            sb.AppendLine($"v {extW} {-extL} 0");
            sb.AppendLine($"v {-extW} {extL} 0");
            sb.AppendLine($"v {extW} {extL} 0");
            sb.AppendLine("vn 0 0 -1");
        }

        sb.AppendLine("vt 0 0");
        sb.AppendLine("vt 1 0");
        sb.AppendLine("vt 0 1");
        sb.AppendLine("vt 1 1");

        // Face uses 1-based indexing
        sb.AppendLine("f 1/1/1 3/3/1 4/4/1 2/2/1");

        File.WriteAllText(path, sb.ToString());
    }
}
#endif

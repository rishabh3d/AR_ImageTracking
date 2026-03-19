using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Imagine.WebAR.Editor
{
    public class PostProcessBuild : MonoBehaviour
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            Debug.Log(buildPath);
            var targetsHtml = "";

            if(!Directory.Exists(buildPath + "/targets"))
            {
                Directory.CreateDirectory(buildPath + "/targets");
            }

            foreach (var info in ImageTrackerGlobalSettings.Instance.imageTargetInfos)
            {
                var src = AssetDatabase.GetAssetPath(info.texture);
                var fileName = Path.GetFileName(src);
                Debug.Log(info.id + "->" + src);
                Debug.Log(fileName + "->" + src);

                File.Copy(src, buildPath + "/targets/" + fileName, true);

                targetsHtml += ("\t\t<imagetarget id='" + info.id + "' src='targets/" + fileName + "'></imagetarget>\n");
            }

            Debug.Log(targetsHtml);

            var lines = File.ReadAllLines(buildPath + "/index.html").ToList();
            var html = "";
            foreach(var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("<imagetarget") && trimmed.EndsWith("</imagetarget>"))
                    continue;
                
                // v1.8.0: Support remote opencv.js
                if (ImageTrackerGlobalSettings.Instance.useRemoteOpenCV && 
                    trimmed.Contains("opencv.js") && trimmed.Contains("<script"))
                {
                    var remoteUrl = ImageTrackerGlobalSettings.Instance.remoteOpenCVUrl;
                    html += "    <script async src=\"" + remoteUrl + "\" type=\"text/javascript\"></script>\n";
                    Debug.Log("Using remote opencv.js: " + remoteUrl);
                    
                    // Remove local opencv.js file to reduce build size
                    var localOpenCVPath = buildPath + "/opencv.js";
                    if(File.Exists(localOpenCVPath))
                    {
                        File.Delete(localOpenCVPath);
                        Debug.Log("Removed local opencv.js to save space");
                    }
                    continue;
                }
                
                html += line + "\n";
            }
            html = html.Replace("<!--IMAGETARGETS-->", "<!--IMAGETARGETS-->\n" + targetsHtml);
            File.WriteAllText(buildPath + "/index.html", html);
        }
    }
}


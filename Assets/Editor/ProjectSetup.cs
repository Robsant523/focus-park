#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FocusPark.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Scene="Assets/Scenes/Park.unity";
        static ProjectSetup() { EditorApplication.delayCall+=Configure; }
        [MenuItem("Focus Park/Configure project")]
        public static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            PlayerSettings.companyName="FocusPark";
            PlayerSettings.productName="Focus Park";
            PlayerSettings.bundleVersion="0.1.0";
            foreach(var group in new[]{BuildTargetGroup.iOS,BuildTargetGroup.Standalone})
            {
                string identifier=PlayerSettings.GetApplicationIdentifier(group);
                if(string.IsNullOrEmpty(identifier) || identifier.Contains("DefaultCompany") || identifier.Contains("unity.template"))
                    PlayerSettings.SetApplicationIdentifier(group,"com.focuspark.game");
            }
            PlayerSettings.iOS.targetOSVersionString="16.0";
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait=true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=true;
            PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.macOS.buildNumber="1";
            // Explicit consent is collected in setup; this relay only operates on the user's LAN.
            PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
            EnsureStandardShader();
        }
        static void EnsureStandardShader()
        {
            var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if(settings.Length==0) return;
            var serialized=new SerializedObject(settings[0]);
            var shaders=serialized.FindProperty("m_AlwaysIncludedShaders");
            if(shaders==null) return;
            var standard=Shader.Find("Standard");
            for(int i=0;i<shaders.arraySize;i++) if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==standard) return;
            shaders.InsertArrayElementAtIndex(shaders.arraySize);
            shaders.GetArrayElementAtIndex(shaders.arraySize-1).objectReferenceValue=standard;
            serialized.ApplyModifiedProperties();
        }
        [MenuItem("Focus Park/Build/Mac game")]
        public static void BuildMac()
        {
            Configure();
            var path=Path.GetFullPath("Builds/macOS/Focus Park.app");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Check(BuildPipeline.BuildPlayer(new[]{Scene},path,BuildTarget.StandaloneOSX,BuildOptions.None));
        }
        [MenuItem("Focus Park/Build/iPhone Xcode project")]
        public static void BuildPhone()
        {
            Configure();
            var path=Path.GetFullPath("Builds/iOS"); Directory.CreateDirectory(path);
            Check(BuildPipeline.BuildPlayer(new[]{Scene},path,BuildTarget.iOS,BuildOptions.None));
        }
        static void Check(BuildReport report)
        {
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Focus Park build failed: "+report.summary.result);
            Debug.Log("Focus Park built: "+report.summary.outputPath);
        }
    }
}
#endif

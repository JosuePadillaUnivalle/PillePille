using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PillePille;

public static class ProjectBuilder
{
    [MenuItem("Pille Pille/Crear o reconstruir escena")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Resources");
        if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Graybox.mat")==null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/Graybox.mat");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var app=new GameObject("PillePille_App");app.AddComponent<PilleGame>();
        ArenaBuilder.Build();
        // Persist graybox materials and geometry so the delivered scene is editable immediately.
        Directory.CreateDirectory("Assets/Materials");
        var saved=new Dictionary<Material,Material>();
        foreach(var renderer in GameObject.Find("Arena_Graybox").GetComponentsInChildren<Renderer>()) {
            var material=renderer.sharedMaterial;
            if(material==null||AssetDatabase.Contains(material)||renderer.GetComponent<TextMesh>()!=null)continue;
            if(!saved.ContainsKey(material)) {
                string path="Assets/Materials/Graybox_"+saved.Count+".mat";
                var asset=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(asset==null){asset=new Material(material);AssetDatabase.CreateAsset(asset,path);}
                else EditorUtility.CopySerialized(material,asset);
                saved.Add(material,asset);
            }
            renderer.sharedMaterial=saved[material];
        }
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<AudioListener>();
        camera.transform.position=new Vector3(28,33,-32);camera.transform.LookAt(Vector3.zero);
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/PillePille.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/PillePille.unity",true)};
        PlayerSettings.companyName="Pille Pille Prototype";PlayerSettings.productName="Pille Pille";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.resizableWindow=true;PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_4_6);
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Pille Pille/Mostrar graybox en editor")]
    public static void PreviewArena(){ArenaBuilder.Build();}

    [MenuItem("Pille Pille/Compilar Windows")]
    public static void Build()
    {
        // Preserve edits to the existing arena when producing an executable.
        if(!File.Exists("Assets/Scenes/PillePille.unity"))CreateScene();
        else EditorSceneManager.OpenScene("Assets/Scenes/PillePille.unity",OpenSceneMode.Single);
        var args=Environment.GetCommandLineArgs();int pathIndex=Array.IndexOf(args,"-pilleBuildPath");
        var output=Path.GetFullPath(pathIndex>=0&&pathIndex+1<args.Length?args[pathIndex+1]:"../PillePille_Windows/PillePille.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/PillePille.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        Debug.Log("PILLE_BUILD: "+report.summary.result+" errors="+report.summary.totalErrors);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Falló la compilación de Pille Pille.");
    }
}

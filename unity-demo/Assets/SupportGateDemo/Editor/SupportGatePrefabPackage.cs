using System;
using System.IO;
using System.Linq;
using Hooligapps.SupportGate.Prefabs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hooligapps.SupportGate.Demo.Editor
{
    public static class SupportGatePrefabPackage
    {
        private const string Root = "Assets/SupportGateUGUI";
        [MenuItem("Tools/Support Gate/Export uGUI package")]
        public static void Export()
        {
            foreach (var path in new[] { Root + "/SupportGateForm.prefab", Root + "/SupportGateFormDark.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<SupportGateForm>(path);
                if (prefab == null) throw new InvalidOperationException("Missing form: " + path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        if (component == null) throw new InvalidOperationException("Missing script: " + path);
                    var fields = new SerializedObject(root.GetComponent<SupportGateForm>());
                    var field = fields.GetIterator();
                    while (field.NextVisible(true))
                        if (field.name.StartsWith("_") && field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue == null)
                            throw new InvalidOperationException("Missing binding: " + field.name);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                {
                    if (dependency.StartsWith("Assets/") && !dependency.StartsWith(Root + "/"))
                        throw new InvalidOperationException("Project dependency in prefab: " + dependency);
                }
            }
            var files = AssetDatabase.FindAssets("", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path)).ToArray();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../dist/SupportGate-UGUI.unitypackage"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            AssetDatabase.ExportPackage(files, output, ExportPackageOptions.Default);
            Debug.Log("Support Gate uGUI package exported: " + output);
        }

        public static void PrepareDemoAndExport()
        {
            var scene = EditorSceneManager.OpenScene("Assets/SupportGateDemo/Scenes/SupportGateDemo.unity");
            var app = UnityEngine.Object.FindObjectOfType<SupportGateDemoApp>();
            var fields = new SerializedObject(app);
            fields.FindProperty("_lightPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SupportGateForm>(Root + "/SupportGateForm.prefab");
            fields.FindProperty("_darkPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SupportGateForm>(Root + "/SupportGateFormDark.prefab");
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Export();
        }
    }
}

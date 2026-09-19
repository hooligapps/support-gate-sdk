using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hooligapps.SupportGate.Demo.Editor
{
    /// <summary>
    /// Собирает демо-сцену: сам стенд, UIDocument с PanelSettings и ссылку на USS
    /// пакета. Всё остальное стенд строит в рантайме, поэтому сцена пересоздаётся
    /// в один клик и её не нужно чинить руками после правок кода.
    /// </summary>
    public static class SupportGateDemoSceneBuilder
    {
        private const string PackageUss = "Packages/com.hooligapps.supportgate/Runtime/UI.UIToolkit/Uss/SupportGate.uss";
        private const string SceneFolder = "Assets/SupportGateDemo/Scenes";
        private const string ScenePath = SceneFolder + "/SupportGateDemo.unity";
        private const string PanelSettingsPath = SceneFolder + "/SupportGateDemoPanelSettings.asset";

        /// <summary>
        /// Первое открытие проекта: сцены ещё нет, и Play показал бы пустой мир.
        /// Собираем её сами, чтобы стенд запускался без лишних шагов.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureSceneExists()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath))
                {
                    return;
                }

                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    Debug.Log("Support Gate demo: в открытой сцене есть несохранённые изменения. " +
                              "Соберите демо-сцену вручную: Tools → Support Gate → Собрать демо-сцену.");
                    return;
                }

                Build(false);

                Debug.Log("Support Gate demo: собрана сцена " + ScenePath + ". Нажмите Play.");
            };
        }

        [MenuItem("Tools/Support Gate/Собрать демо-сцену")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;

                EditorUtility.DisplayDialog("Support Gate",
                    "Сначала выйдите из Play mode, затем соберите сцену ещё раз.", "Ок");

                return;
            }

            Build(true);
        }

        private static void Build(bool interactive)
        {
            if (!Directory.Exists(SceneFolder))
            {
                Directory.CreateDirectory(SceneFolder);
                AssetDatabase.Refresh();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var documentGo = new GameObject("SupportGateUIDocument", typeof(UIDocument));
            var document = documentGo.GetComponent<UIDocument>();
            document.panelSettings = LoadOrCreatePanelSettings();

            var appGo = new GameObject("SupportGateDemoApp");
            var app = appGo.AddComponent<SupportGateDemoApp>();

            var serialized = new SerializedObject(app);
            serialized.FindProperty("_document").objectReferenceValue = document;
            serialized.FindProperty("_styleSheet").objectReferenceValue = Load<StyleSheet>(PackageUss);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            if (interactive)
            {
                EditorUtility.DisplayDialog("Support Gate", "Демо-сцена собрана: " + ScenePath + "\nНажмите Play.", "Ок");
            }
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 1920);

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning("Support Gate demo: не найден ассет " + path);
            }

            return asset;
        }
    }
}

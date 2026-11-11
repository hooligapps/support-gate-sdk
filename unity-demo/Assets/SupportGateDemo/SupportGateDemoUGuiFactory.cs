using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Demo
{
    public static class SupportGateDemoUGuiFactory
    {
        public static GameObject CreateBlocker(Transform parent)
        {
            var go = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            go.SetActive(false);
            return go;
        }

        public static Button CreateEntryButton(Transform parent)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources());
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24, -24);
            rect.sizeDelta = new Vector2(220, 80);
            var text = go.GetComponentInChildren<Text>();
            text.text = "Support";
            text.fontSize = 26;
            return go.GetComponent<Button>();
        }
    }
}

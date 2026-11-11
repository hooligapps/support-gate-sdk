using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Prefabs
{
    public sealed class SupportGateUGuiGridHeight : MonoBehaviour
    {
        private void LateUpdate()
        {
            var grid = GetComponent<GridLayoutGroup>();
            var width = ((RectTransform)transform).rect.width;
            if (width <= 0f)
            {
                return;
            }

            var active = 0;
            for (var i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).gameObject.activeSelf) active++;
            }

            var perRow = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));
            var rows = Mathf.CeilToInt(active / (float)perRow);
            var height = rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y + 16f;

            var element = GetComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, height))
            {
                element.preferredHeight = height;
            }
        }
    }
}

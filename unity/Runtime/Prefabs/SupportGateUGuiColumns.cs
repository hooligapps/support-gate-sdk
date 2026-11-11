using UnityEngine;
using UnityEngine.UI;

namespace Hooligapps.SupportGate.Prefabs
{
    public sealed class SupportGateUGuiColumns : HorizontalOrVerticalLayoutGroup
    {
        public float NarrowWidth = 1000f;

        private bool Narrow
        {
            get
            {
                var width = rectTransform.rect.width;
                return width > 0f && width < NarrowWidth;
            }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, Narrow);
        }

        public override void CalculateLayoutInputVertical()
        {
            CalcAlongAxis(1, Narrow);
        }

        public override void SetLayoutHorizontal()
        {
            SetChildrenAlongAxis(0, Narrow);
        }

        public override void SetLayoutVertical()
        {
            SetChildrenAlongAxis(1, Narrow);
        }
    }
}

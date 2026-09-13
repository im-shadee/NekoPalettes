using UnityEngine.UIElements;

namespace NekoPalettes.Editor
{
    public static class UIElementExtensions
    {
        public static void SetBorderColor(this VisualElement element, StyleColor color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }
    }
}

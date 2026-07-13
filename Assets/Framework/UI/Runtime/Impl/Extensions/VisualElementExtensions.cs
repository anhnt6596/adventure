using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public static class VisualElementExtensions
    {
        public static void ToAbsoluteFullScreen(this VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }

        public static void Show(this VisualElement element, bool isShow)
        {
            element.style.display = isShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void SetIndex(this VisualElement element, int index)
        {
            if (element == null) return;

            var parent = element.parent;
            if (parent == null) return;

            int childCount = parent.childCount;

            index = Mathf.Clamp(index, 0, childCount - 1);

            int currentIndex = parent.IndexOf(element);
            if (currentIndex == index) return;

            element.RemoveFromHierarchy();
            parent.Insert(index, element);
        }

        public static void Clear(ref IVisualElementScheduledItem schedule)
        {
            schedule?.Pause();
            schedule = null;
        }

        public static VisualElement QueryPath(this VisualElement root, params string[] path)
        {
            var current = root;
            foreach (var name in path)
            {
                current = current.Q(name);
                if (current == null) return null;
            }
            return current;
        }
    }
}
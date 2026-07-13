using UnityEngine.UIElements;

namespace Core.UI
{
    public interface IUIElement
    {
        VisualElement Root { get; }
        void Bind(IUISystem uiSystem);
    }
}

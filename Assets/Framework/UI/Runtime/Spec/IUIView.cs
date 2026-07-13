using UnityEngine.UIElements;

namespace Core.UI
{
    public interface IUIView : IUIElement
    {
        UILayer DefaultLayer { get; }
        void OnShow();
        void OnHide();
    }
}

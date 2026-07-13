using UnityEngine.UIElements;

namespace Core.UI
{
    public abstract class UIView : UIElement, IUIView
    {
        public virtual UILayer DefaultLayer { get; } = default;

        public UIView(VisualElement root) : base(root) { }

        public virtual void OnShow()
        {

        }

        public virtual void OnHide()
        {

        }
    }

}
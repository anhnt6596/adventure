using UnityEngine.UIElements;

namespace Core.UI
{
    public abstract class UIElement : IUIElement
    {
        public VisualElement Root { get; }

        protected IUISystem _uiSystem;

        public UIElement(VisualElement root)
        {
            Root = root;
            Root.dataSource = this;
        }

        public void Bind(IUISystem uiSystem)
        {
            _uiSystem = uiSystem;
        }
    }
}

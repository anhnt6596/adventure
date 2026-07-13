using UnityEngine.UIElements;

namespace Core.UI
{
    public enum TooltipAnchor { Auto, Above, Below, Left, Right }

    public interface ITooltip : IUIView
    {
        void SetAnchor(VisualElement anchor, TooltipAnchor position = TooltipAnchor.Auto);
        void SetAnchor(float x, float y, TooltipAnchor position = TooltipAnchor.Auto);
        // set auto hide after an amount of seconds, call after show the tooltip
        void AutoHideIn(float seconds);
    }
}
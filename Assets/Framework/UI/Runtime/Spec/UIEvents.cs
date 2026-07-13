namespace Core.UI
{
    public readonly struct UIShownEvent
    {
        public readonly IUIView UI;
        public UIShownEvent(IUIView ui) => UI = ui;
    }

    public readonly struct UIHiddenEvent
    {
        public readonly IUIView UI;
        public UIHiddenEvent(IUIView ui) => UI = ui;
    }
}

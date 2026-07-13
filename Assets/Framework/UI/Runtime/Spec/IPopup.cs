namespace Core.UI
{
    public interface IPopup : IUIView
    {
        bool CloseOnEscape { get; }
        void Close();
    }
}
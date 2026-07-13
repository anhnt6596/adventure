namespace Core.UI
{
    public enum ScreenTapDismiss
    {
        None,
        DismissOnTapBlank,
    }
    public interface ISheet : IUIView
    {
        // Sheet do not auto call show fx because it may be used in navigation (display a chain of sheet without animation)
        public void DoShowFx();
        // Hide the Sheet with animation
        public void Close();
    }
}
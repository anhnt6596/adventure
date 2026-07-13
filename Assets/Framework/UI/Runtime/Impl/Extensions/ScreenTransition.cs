using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Core.UI
{
    /// <summary>
    /// Fullscreen transition overlay with 2 async calls: Cover() and Reveal().
    ///
    /// Usage:
    ///   await transition.Cover();
    ///   await LoadSceneAsync("NewScene");
    ///   await transition.Reveal();
    ///
    /// USS classes applied to overlay (cascade to children):
    ///   .transition-enter    — covering the screen
    ///   .transition-loading  — holding while loading
    ///   .transition-exit     — revealing the screen
    ///
    /// UXML requires: root &gt; overlay (name="overlay")
    /// The overlay is a container — real animation happens on its children via USS.
    /// TransitionEndEvent bubbles from children, so duration is driven by USS.
    /// </summary>
    public class ScreenTransition : UIView
    {
        public override UILayer DefaultLayer => UILayer.Blocking;

        private VisualElement _overlay;
        private bool _covering;
        private bool _covered;

        public bool IsCovered => _covered;
        public bool IsTransitioning => _covering || _covered;

        public ScreenTransition(VisualElement root) : base(root)
        {
            root.pickingMode = PickingMode.Ignore;
            _overlay = root.Q<VisualElement>("overlay");
            _overlay.pickingMode = PickingMode.Ignore;
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Cover the screen. Awaits until fully covered.
        /// </summary>
        public async UniTask Cover()
        {
            if (_covering || _covered) return;
            _covering = true;

            _overlay.style.display = DisplayStyle.Flex;
            _overlay.pickingMode = PickingMode.Position;
            _overlay.BringToFront();

            _overlay.RemoveFromClassList("transition-enter");
            _overlay.RemoveFromClassList("transition-loading");
            _overlay.RemoveFromClassList("transition-exit");

            var tcs = new UniTaskCompletionSource();

            _overlay.schedule.Execute(() =>
            {
                _overlay.AddToClassList("transition-enter");

                _overlay.RegisterCallback<TransitionEndEvent>(OnCoverEnd);

                void OnCoverEnd(TransitionEndEvent evt)
                {
                    _overlay.UnregisterCallback<TransitionEndEvent>(OnCoverEnd);
                    _covering = false;
                    _covered = true;
                    _overlay.AddToClassList("transition-loading");
                    tcs.TrySetResult();
                }
            }).ExecuteLater(10);

            await tcs.Task;
        }

        /// <summary>
        /// Reveal the screen. Awaits until fully revealed.
        /// </summary>
        public async UniTask Reveal()
        {
            if (!_covered) return;

            _overlay.RemoveFromClassList("transition-enter");
            _overlay.RemoveFromClassList("transition-loading");

            var tcs = new UniTaskCompletionSource();

            _overlay.schedule.Execute(() =>
            {
                _overlay.AddToClassList("transition-exit");

                _overlay.RegisterCallback<TransitionEndEvent>(OnRevealEnd);

                void OnRevealEnd(TransitionEndEvent evt)
                {
                    _overlay.UnregisterCallback<TransitionEndEvent>(OnRevealEnd);
                    _overlay.RemoveFromClassList("transition-exit");
                    _overlay.style.display = DisplayStyle.None;
                    _overlay.pickingMode = PickingMode.Ignore;
                    _covered = false;
                    tcs.TrySetResult();
                }
            }).ExecuteLater(10);

            await tcs.Task;
        }
    }
}

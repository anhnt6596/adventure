using System;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Core.UI
{
    [UxmlElement]
    public partial class DynamicLabel : Label
    {
        private Func<string> _textProvider;
        private bool _attached;

        public Func<string> TextProvider
        {
            get => _textProvider;
            set
            {
                _textProvider = value;
                UpdateText();
            }
        }

        public DynamicLabel()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying) return;
#endif
            _attached = true;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (!_attached) return;
            _attached = false;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            UpdateText();
        }

        public void UpdateText()
        {
            if (_textProvider != null)
                text = _textProvider();
        }
    }
}

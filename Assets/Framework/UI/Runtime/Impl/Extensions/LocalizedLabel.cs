using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Core.UI
{
    [UxmlElement]
    public partial class LocalizedLabel : Label
    {
        private string _key;
        private string _table;
        private LocalizedString _localizedString;
        private bool _attached;
        private bool _initialized;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>
        /// Set localization key at runtime. Use "table/key" for non-default table.
        /// </summary>
        public void SetKey(string key)
        {
            ParseKey(key);
            _initialized = true;
            Bind();
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying) return;
#endif
            _attached = true;

            if (!_initialized)
            {
                ParseKey(text);
                _initialized = true;
            }

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            Bind();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (!_attached) return;
            _attached = false;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

            if (_localizedString != null)
                _localizedString.StringChanged -= OnStringChanged;
        }

        private void ParseKey(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                _table = null;
                _key = null;
                return;
            }

            var slashIndex = raw.IndexOf('/');
            if (slashIndex > 0)
            {
                _table = raw.Substring(0, slashIndex);
                _key = raw.Substring(slashIndex + 1);
            }
            else
            {
                _table = L.DefaultTable;
                _key = raw;
            }
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            UpdateText();
        }

        private void OnStringChanged(string value)
        {
            text = value;
        }

        private void Bind()
        {
            if (_localizedString != null)
                _localizedString.StringChanged -= OnStringChanged;

            if (string.IsNullOrEmpty(_key))
            {
                _localizedString = null;
                return;
            }

            _localizedString = new LocalizedString
            {
                TableReference = _table,
                TableEntryReference = _key
            };

            UpdateText();
        }

        private void UpdateText()
        {
            if (_localizedString == null) return;
            text = _localizedString.GetLocalizedString();
        }
    }
}

using System.Collections.Generic;
using System;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

namespace Core.UI
{
    public class UISystem : MonoBehaviour, IUISystem
    {
        public event Action<Vector2> OnScreenTapped;

        private UIDocument _document;
        [SerializeField] private UIRegistry _registry;
        private Dictionary<UILayer, VisualElement> _layers = new();
        private VisualElement _root;

        private readonly Dictionary<Type, VisualTreeAsset> _assetMap = new();
        private readonly Dictionary<Type, IUIView> _uiMap = new();
        private readonly List<(IPopup popup, UILayer layer)> _popupStack = new();

        private IEventBus _eventBus;
        private IDependencyInjector _injector;
        private bool _initialized;

        public void Initialize(IEventBus eventBus, IDependencyInjector injector = null)
        {
            if (_initialized) return;
            _eventBus = eventBus;
            _injector = injector;

            if (_document == null) _document = GetComponent<UIDocument>();
            _root = _document.rootVisualElement;
#if UNITY_EDITOR
            UIRegistryGenerator.Regenerate(_registry);
#endif
            BuildAssetMap();
            InitLayers();
            _initialized = true;
        }

        private void BuildAssetMap()
        {
            _assetMap.Clear();
            if (_registry == null) return;

            foreach (var e in _registry.entries)
            {
                if (e == null || e.asset == null || string.IsNullOrEmpty(e.viewTypeName))
                    continue;

                var t = Type.GetType(e.viewTypeName);
                if (t == null) continue;

                _assetMap[t] = e.asset;
            }
        }

        #region Layers
        private void InitLayers()
        {
            var root = _document.rootVisualElement;

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var ve = new VisualElement();
                ve.name = layer.ToString();

                // full screen container
                ve.style.position = Position.Absolute;
                ve.style.left = 0;
                ve.style.right = 0;
                ve.style.top = 0;
                ve.style.bottom = 0;
                ve.pickingMode = PickingMode.Ignore;

                root.Add(ve);

                _layers[layer] = ve;
            }
        }

        public VisualElement GetLayer(UILayer layer) => _layers[layer];

        public UILayer GetUILayer(IUIView ui)
        {
            var parent = ui.Root.parent;
            foreach (var kvp in _layers)
            {
                if (kvp.Value == parent) return kvp.Key;
            }
            return default;
        }
        #endregion Layers

        private T ShowInternal<T>() where T : IUIView
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    $"{nameof(UISystem)}.{nameof(Initialize)}() must be called before showing UI.");

            var t = typeof(T);
            var ui = GetOrCreate<T>(t);

            if (ui == null)
                throw new Exception($"{t.FullName} should implement IUIView to be pooled safely.");

            if (ui.Root.parent != null)
            {
                // already shown, Hide it first to reset state, then show again
                Hide(ui);
            }

            // attach to layer
            _layers[ui.DefaultLayer].Add(ui.Root);
            ui.Root.BringToFront();
            ui.Root.style.display = DisplayStyle.Flex;
            ui.Root.ToAbsoluteFullScreen();
            return ui;
        }

        public T Show<T>() where T : IUIView
        {
            var ui = ShowInternal<T>();
            if (ui is IPopup popup) PushPopup(popup);
            ui.OnShow();
            _eventBus.Publish(new UIShownEvent(ui));
            return ui;
        }

        public void Hide<T>() where T : IUIView
        {
            var t = typeof(T);

            if (!_uiMap.TryGetValue(t, out var ui) || ui == null)
                return;

            Hide(ui);
        }

        public void Hide(IUIView ui)
        {
            if (ui == null) return;

            if (ui is IPopup popup) RemovePopup(popup);
            ui.OnHide();
            ui.Root.RemoveFromHierarchy();
            ui.Root.style.display = DisplayStyle.None;
            _eventBus.Publish(new UIHiddenEvent(ui));
        }

        public void SetUILayer(IUIView ui, UILayer layer)
        {
            _layers[layer].Add(ui.Root);
            ui.Root.BringToFront();

            if (ui is IPopup popup)
            {
                for (int i = 0; i < _popupStack.Count; i++)
                {
                    if (_popupStack[i].popup == popup)
                    {
                        _popupStack[i] = (popup, layer);
                        break;
                    }
                }
            }
        }

        public T Get<T>() where T : IUIView
        {
            if (_uiMap.TryGetValue(typeof(T), out IUIView ui))
            {
                if (ui.Root.style.display == DisplayStyle.Flex) return (T)ui;
            }
            return default;
        }

        public List<T> GetActiveUIs<T>() where T : IUIView
        {
            var result = new List<T>();
            foreach (var kvp in _uiMap)
            {
                if (kvp.Value is T ui && ui.Root.style.display == DisplayStyle.Flex)
                {
                    result.Add(ui);
                }
            }
            return result;
        }

        #region Popup Stack
        private void PushPopup(IPopup popup)
        {
            var layer = GetUILayer(popup);
            RemovePopup(popup);
            _popupStack.Add((popup, layer));
        }

        private void RemovePopup(IPopup popup)
        {
            _popupStack.RemoveAll(e => e.popup == popup);
        }

        public IPopup GetTopPopup()
        {
            return GetTopPopupWithLayer().popup;
        }

        public (IPopup popup, UILayer layer) GetTopPopupWithLayer()
        {
            if (_popupStack.Count == 0) return default;

            var top = _popupStack[0];
            for (int i = 1; i < _popupStack.Count; i++)
            {
                var entry = _popupStack[i];
                if (entry.layer >= top.layer)
                    top = entry;
            }
            return top;
        }
        #endregion Popup Stack

        private T GetOrCreate<T>(Type t) where T : IUIView
        {
            if (_uiMap.TryGetValue(t, out var ui)) return (T)ui;

            var created = CreateUIElement<T>();
            _uiMap[t] = created;
            return created;
        }

        public T CreateUIElement<T>() where T : IUIElement
        {
            var t = typeof(T);
            if (!_assetMap.TryGetValue(t, out var asset) || asset == null)
                throw new Exception($"No UXML registered for {t.FullName}.");

            var root = asset.CloneTree();
            root.name = t.Name;
            var created = (T)Activator.CreateInstance(t, root);
            created.Bind(this);
            _injector?.Inject(created);
            return created;
        }

        public bool IsPointerOverUI()
        {
            if (_root?.panel == null) return false;

            var pointer = Pointer.current;
            if (pointer == null) return false;

            var pos = pointer.position.ReadValue();
            var screenPos = new Vector2(pos.x, Screen.height - pos.y);
            var panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, screenPos);
            var picked = _root.panel.Pick(panelPos);

            return picked != null && picked.pickingMode != PickingMode.Ignore;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame && _root?.panel != null)
            {
                var m = pointer.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                    _root.panel,
                    new Vector2(m.x, Screen.height - m.y)
                );
                OnScreenTapped?.Invoke(panelPos);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                var top = GetTopPopup();
                if (top != null && top.CloseOnEscape)
                    top.Close();
            }
        }
    }
}

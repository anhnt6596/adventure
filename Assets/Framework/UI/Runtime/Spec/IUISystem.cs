using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public enum UILayer
    {
        HUD = 0,
        Sheet = 1,
        Popup = 2,
        Overlay = 3,
        Toast = 4,
        Blocking = 5,
        Debug = 6,
    }

    public interface IUISystem
    {
        event Action<Vector2> OnScreenTapped;
        T Show<T>() where T : IUIView;
        T Show<T, TData>(TData data) where T : IUIView, IWithData<TData>;
        void Hide<T>() where T: IUIView;
        void Hide(IUIView ui);
        T Get<T>() where T : IUIView;
        void SetUILayer(IUIView ui, UILayer layer);
        List<T> GetActiveUIs<T>() where T : IUIView;
        VisualElement GetLayer(UILayer layer);
        UILayer GetUILayer(IUIView ui);
        T CreateUIElement<T>() where T : IUIElement;
        IPopup GetTopPopup();
        (IPopup popup, UILayer layer) GetTopPopupWithLayer();
        bool IsPointerOverUI();
    }
}

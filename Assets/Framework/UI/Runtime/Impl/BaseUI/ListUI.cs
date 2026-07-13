using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public class ListUI<TElement, TElementData> : UIElement
        where TElement : UIElement, IWithData<TElementData>
    {
        private readonly Dictionary<VisualElement, TElement> _elementMap = new();
        private readonly List<TElementData> _itemDatas = new();
        private ListView _listView;
        private Action<TElement, int> _onBind;

        public ListUI(ListView root, Action<TElement, int> onBind = null) : base(root)
        {
            _listView = root;
            _onBind = onBind;
            _listView.itemsSource = _itemDatas;
            _listView.selectionType = SelectionType.None;
            var scrollView = _listView.Q<ScrollView>();
            scrollView.AddManipulator(new DragScrollManipulator(scrollView));
            ShowScrollBar(false); // do not show scrollbar by default
            _listView.makeItem = () =>
            {
                var item = _uiSystem.CreateUIElement<TElement>();
                _elementMap[item.Root] = item;
                return item.Root;
            };

            _listView.bindItem = (element, index) =>
            {
                if (_elementMap.TryGetValue(element, out var item))
                {
                    item.Data = _itemDatas[index];
                    _onBind?.Invoke(item, index);
                }
            };
        }

        public void SetItems(IEnumerable<TElementData> dataList)
        {
            _itemDatas.Clear();
            _itemDatas.AddRange(dataList);
            _listView.RefreshItems();
        }

        public void AddItem(TElementData data)
        {
            _itemDatas.Add(data);
            _listView.RefreshItems();
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _itemDatas.Count) return;
            _itemDatas.RemoveAt(index);
            _listView.RefreshItems();
        }

        public void RemoveItem(TElementData data)
        {
            if (_itemDatas.Remove(data))
            {
                _listView.RefreshItems();
            }
        }

        public void ShowScrollBar(bool show)
        {
            _listView.Q<ScrollView>().verticalScrollerVisibility =
                show ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    [CreateAssetMenu(menuName = "UI/UI Registry")]
    public class UIRegistry : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string viewTypeName; // typeof(T).AssemblyQualifiedName
            public VisualTreeAsset asset;
        }

        public List<Entry> entries = new();
    }
}
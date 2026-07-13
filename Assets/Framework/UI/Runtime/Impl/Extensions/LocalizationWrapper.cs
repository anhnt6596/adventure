using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Core.UI
{

    public static class L
    {
        public const string DefaultTable = "Main";

        private static readonly Dictionary<string, LocalizedString> _cache = new();

        /// <summary>
        /// Get localized string (no param)
        /// </summary>
        public static string Text(string key)
        {
            return Get(DefaultTable, key).GetLocalizedString();
        }

        /// <summary>
        /// Get localized string with params
        /// </summary>
        public static string Text(string key, params object[] args)
        {
            var ls = Get(DefaultTable, key);
            ls.Arguments = args;
            var result = ls.GetLocalizedString();
            ls.Arguments = null;
            return result;
        }

        /// <summary>
        /// Get localized string from a specific table
        /// </summary>
        public static string TextFrom(string table, string key)
        {
            return Get(table, key).GetLocalizedString();
        }

        /// <summary>
        /// Get localized string from a specific table with params
        /// </summary>
        public static string TextFrom(string table, string key, params object[] args)
        {
            var ls = Get(table, key);
            ls.Arguments = args;
            var result = ls.GetLocalizedString();
            ls.Arguments = null;
            return result;
        }

        /// <summary>
        /// Change current language (e.g: "en", "vi", "ja")
        /// </summary>
        public static void SetLanguage(string localeCode)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            if (locale != null)
            {
                _cache.Clear();
                LocalizationSettings.SelectedLocale = locale;
            }
        }

        /// <summary>
        /// (Optional) Get raw LocalizedString for advanced usage
        /// </summary>
        public static LocalizedString GetRaw(string key)
        {
            return Get(DefaultTable, key);
        }

        // ================= INTERNAL =================

        private static LocalizedString Get(string table, string key)
        {
            var id = table + "_" + key;

            if (!_cache.TryGetValue(id, out var ls))
            {
                ls = new LocalizedString
                {
                    TableReference = table,
                    TableEntryReference = key
                };

                _cache[id] = ls;
            }

            return ls;
        }
    }
}
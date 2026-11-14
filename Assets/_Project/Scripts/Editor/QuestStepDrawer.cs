using System.Collections.Generic;
using GameLimon.Story;
using UnityEditor;
using UnityEngine;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Рисует строковое поле requiredStep как выпадающий список шагов всех квестов проекта.
    /// Это убирает опечатки в именах флагов и ускоряет настройку сцен.
    /// </summary>
    [CustomPropertyDrawer(typeof(QuestStepAttribute))]
    public sealed class QuestStepDrawer : PropertyDrawer
    {
        private const double CacheLifetime = 1.5d;

        private sealed class Entry
        {
            public string Flag;
            public string Label;
        }

        private static List<Entry> _cache;
        private static double _cacheTime;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var attribute = (QuestStepAttribute)this.attribute;
            List<Entry> entries = GetEntries();

            var labels = new List<string> { attribute.EmptyLabel };
            int selected = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                labels.Add(entries[i].Label);
                if (entries[i].Flag == property.stringValue)
                    selected = i + 1;
            }

            if (!string.IsNullOrEmpty(property.stringValue) && selected == 0)
            {
                labels.Add($"[нет в ассетах] {property.stringValue}");
                selected = labels.Count - 1;
            }

            int picked = EditorGUI.Popup(position, label.text, selected, labels.ToArray());

            if (picked == 0)
                property.stringValue = string.Empty;
            else if (picked > 0 && picked <= entries.Count)
                property.stringValue = entries[picked - 1].Flag;
        }

        private static List<Entry> GetEntries()
        {
            if (_cache != null && EditorApplication.timeSinceStartup - _cacheTime < CacheLifetime)
                return _cache;

            _cache = new List<Entry>();
            _cacheTime = EditorApplication.timeSinceStartup;

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (quest == null)
                    continue;

                for (int i = 0; i < quest.Count; i++)
                {
                    QuestStep step = quest.GetStep(i);
                    if (step == null || string.IsNullOrEmpty(step.CompletionFlag))
                        continue;

                    _cache.Add(new Entry
                    {
                        Flag = step.CompletionFlag,
                        Label = $"{quest.DisplayName} / {step.Title}  [{step.CompletionFlag}]"
                    });
                }
            }

            return _cache;
        }

        [InitializeOnLoadMethod]
        private static void ResetCacheOnLoad()
        {
            EditorApplication.projectChanged += () =>
            {
                _cache = null;
                _cacheTime = 0d;
            };
        }
    }
}

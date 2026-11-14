using System.Collections.Generic;
using System.Text;
using GameLimon.Story;
using UnityEditor;
using UnityEngine;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Проверка всех квестов проекта: пустые шаги, дубли флагов, совпадения id.
    /// Меню: Tools → GameLimon → Validate Quests.
    /// </summary>
    public static class QuestValidator
    {
        [MenuItem("Tools/GameLimon/Validate Quests")]
        public static void ValidateFromMenu()
        {
            var report = new StringBuilder();
            var usedFlags = new Dictionary<string, QuestDefinition>();
            var usedIds = new HashSet<string>();
            int questCount = 0;
            int errors = 0;

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (quest == null)
                    continue;

                questCount++;

                if (!usedIds.Add(quest.Id))
                {
                    report.AppendLine($"ОШИБКА: id квеста '{quest.Id}' повторяется ({path}).");
                    errors++;
                }

                if (!quest.IsValid(out string error))
                {
                    report.AppendLine($"ОШИБКА: {error} ({path})");
                    errors++;
                    continue;
                }

                for (int i = 0; i < quest.Count; i++)
                {
                    QuestStep step = quest.GetStep(i);
                    string flag = step.CompletionFlag;

                    if (usedFlags.TryGetValue(flag, out QuestDefinition owner) && owner != quest)
                    {
                        report.AppendLine(
                            $"ОШИБКА: флаг '{flag}' используется в квестах '{owner.DisplayName}' и '{quest.DisplayName}'.");
                        errors++;
                    }
                    else
                    {
                        usedFlags[flag] = quest;
                    }
                }
            }

            if (errors == 0)
                Debug.Log($"[QuestValidator] OK: проверено квестов — {questCount}, ошибок нет.");
            else
                Debug.LogError($"[QuestValidator] Найдено ошибок: {errors}\n{report}");
        }
    }
}

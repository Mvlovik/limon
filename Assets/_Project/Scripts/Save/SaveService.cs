using System;
using System.IO;
using UnityEngine;

namespace GameLimon.Save
{
    /// <summary>
    /// Когда сохранять решает - GameFlow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveService : MonoBehaviour
    {
        public bool HasSave(int slot = 0) => File.Exists(GetSavePath(slot));

        public void Save(GameStateSnapshot snapshot, int slot = 0)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[SaveService] Пустой снапшот — сохранение пропущено.");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(snapshot, true);
                File.WriteAllText(GetSavePath(slot), json);
                Debug.Log($"[SaveService] Сохранено: {GetSavePath(slot)}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Ошибка записи: {exception.Message}");
            }
        }

        public GameStateSnapshot Load(int slot = 0)
        {
            if (!HasSave(slot))
                return null;

            try
            {
                string json = File.ReadAllText(GetSavePath(slot));
                return JsonUtility.FromJson<GameStateSnapshot>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Ошибка чтения: {exception.Message}");
                return null;
            }
        }

        public void DeleteSave(int slot = 0)
        {
            string path = GetSavePath(slot);
            if (File.Exists(path))
                File.Delete(path);
        }

        public static string GetSavePath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"gamelimon_save_{slot}.json");
    }
}

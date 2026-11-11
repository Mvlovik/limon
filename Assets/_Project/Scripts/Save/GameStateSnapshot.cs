using System;
using System.Collections.Generic;

namespace GameLimon.Save
{
    /// <summary>
    /// Сериализуемая модель сохранения (JsonUtility: публичные поля, без словарей).
    /// Хватает, чтобы продолжить с начала отрезка, а не с середины катсцены:
    /// id сцены, id чекпоинта, флаги, выборы.
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        public string sceneId;
        public string checkpointId;
        public List<string> flags = new List<string>();
        public List<ChoiceEntry> choices = new List<ChoiceEntry>();
    }

    [Serializable]
    public class ChoiceEntry
    {
        public string id;
        public string value;

        public ChoiceEntry() { }

        public ChoiceEntry(string id, string value)
        {
            this.id = id;
            this.value = value;
        }
    }
}

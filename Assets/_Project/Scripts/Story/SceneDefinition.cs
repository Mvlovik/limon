using UnityEngine;

namespace GameLimon.Story
{
    /// <summary> Одна сцена туннеля </summary>
    [CreateAssetMenu(fileName = "SCN_NewScene", menuName = "GameLimon/Story/Scene Definition")]
    public sealed class SceneDefinition : ScriptableObject
    {
        [Tooltip("Уникальный id сцены в сюжете, например A1_S01.")]
        [SerializeField] private string id;

        [Tooltip("Имя сцены как в Build Settings, например A1_S01_Apartment.")]
        [SerializeField] private string sceneName;

        [Tooltip("Человеческое название — для меню загрузки и сейвов.")]
        [SerializeField] private string displayName;

        [Tooltip("Id стартового чекпоинта сцены.")]
        [SerializeField] private string checkpointId;

        public string Id => id;
        public string SceneName => sceneName;
        public string DisplayName => displayName;
        public string CheckpointId => checkpointId;
    }
}

using System;
using UnityEngine;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Описание одной сцены на экране выбора сцен.
    /// </summary>
    [Serializable]
    public sealed class MenuSceneEntry
    {
        [Tooltip("Название акта, которое пишется поверх картинки, например 'Акт 1'")]
        public string actTitle = "Акт 1";

        [Tooltip("Отображаемое название сцены под картинкой.")]
        public string displayName = "Новая сцена";

        [Tooltip("Id сцены из GameLimon/Story/SceneDefinition")]
        public string sceneId;

        [Tooltip("Имя сцены как в Build Settings")]
        public string sceneName;

        [Tooltip("Картинка-превью сцен")]
        public Sprite preview;
    }
}

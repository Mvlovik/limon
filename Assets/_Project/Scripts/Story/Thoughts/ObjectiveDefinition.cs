using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Указание игроку: иконка-метка и текст в правом верхнем углу.
    /// </summary>
    [CreateAssetMenu(fileName = "OBJ_NewObjective", menuName = "GameLimon/Story/Objective")]
    public sealed class ObjectiveDefinition : ScriptableObject
    {
        [Tooltip("Иконка-метка слева от текста. Например, PNG-пин.")]
        [SerializeField] private Sprite icon;

        [TextArea(2, 4)]
        [Tooltip("Текст указания")]
        [SerializeField] private string text;

        [Tooltip("Флаг (указание выполнено)")]
        [SerializeField] private string completedFlag;

        public Sprite Icon => icon;
        public string Text => text;
        public string CompletedFlag => completedFlag;
    }
}

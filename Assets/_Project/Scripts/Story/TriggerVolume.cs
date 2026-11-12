using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Зона-триггер: проигрывает StoryAction при входе/выходе игрока.
    /// Вешается на объект с Collider (isTrigger включается автоматически).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TriggerVolume : MonoBehaviour
    {
        [Tooltip("Тег игрока.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Действия")]
        [Tooltip("Действия при входе игрока в зону.")]
        [SerializeField] private List<StoryAction> onEnter = new List<StoryAction>();

        [Tooltip("Действия при выходе игрока из зоны.")]
        [SerializeField] private List<StoryAction> onExit = new List<StoryAction>();

        [Header("Повторы")]
        [Tooltip("Сработать только один раз за загрузку сцены.")]
        [SerializeField] private bool once = true;

        [Tooltip("Не срабатывать, если этот флаг уже стоит.")]
        [SerializeField] private string skipIfFlag;

        [Tooltip("Флаг, который ставится после срабатывания.")]
        [SerializeField] private string completedFlag;

        [Header("Постановка")]
        [Tooltip("Блокировать управление, пока проигрываются действия (скриптовый бит).")]
        [SerializeField] private bool lockPlayerWhileRunning;

        private bool _enterConsumed;
        private bool _exitConsumed;

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            if (volume != null)
                volume.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other) || _enterConsumed)
                return;

            if (once)
                _enterConsumed = true;

            Fire(onEnter);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other) || _exitConsumed)
                return;

            if (once)
                _exitConsumed = true;

            Fire(onExit);
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && other.CompareTag(playerTag);
        }

        private void Fire(List<StoryAction> actions)
        {
            if (actions == null || actions.Count == 0)
                return;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;

            if (state != null && !string.IsNullOrEmpty(skipIfFlag) && state.GetFlag(skipIfFlag))
                return;

            if (state != null && !string.IsNullOrEmpty(completedFlag))
                state.SetFlag(completedFlag, true);

            if (lockPlayerWhileRunning)
                StartCoroutine(RunLocked(actions));
            else
                StoryDirector.Ensure().Run(actions, transform.position);
        }

        private IEnumerator RunLocked(List<StoryAction> actions)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            PlayerMotor motor = player != null ? player.GetComponent<PlayerMotor>() : null;
            PlayerLook look = player != null ? player.GetComponent<PlayerLook>() : null;

            if (motor != null)
                motor.SetControlLocked(true);

            if (look != null)
                look.PoseLocked = true;

            yield return StoryDirector.Ensure().RunBlocking(actions, transform.position);

            if (motor != null)
                motor.SetControlLocked(false);

            if (look != null)
                look.PoseLocked = false;
        }
    }
}

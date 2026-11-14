using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Зона-триггер: проигрывает StoryAction при входе/выходе игрока.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TriggerVolume : MonoBehaviour
    {
        [Tooltip("Тег игрока")]
        [SerializeField] private string playerTag = "Player";

        [Header("Квест")]
        [Tooltip("Разрешить срабатывание только пока квест ждёт этот флаг")]
        [QuestStep]
        [SerializeField] private string requiredStep;

        [Header("Действия")]
        [Tooltip("Действия при входе игрока в зону")]
        [SerializeField] private List<StoryAction> onEnter = new List<StoryAction>();

        [Tooltip("Действия при выходе игрока из зоны")]
        [SerializeField] private List<StoryAction> onExit = new List<StoryAction>();

        [Header("Повторы")]
        [Tooltip("Сработать только один раз за загрузку сцены")]
        [SerializeField] private bool once = true;

        [Tooltip("Скипать, если этот флаг уже стоит")]
        [SerializeField] private string skipIfFlag;

        [Tooltip("Флаг, который ставится после срабатывания")]
        [SerializeField] private string completedFlag;

        [Header("Постановка")]
        [Tooltip("Блокировать управление, пока проигрываются действия (скриптовый бит)")]
        [SerializeField] private bool lockPlayerWhileRunning;

#if UNITY_EDITOR
        [Header("Отладка (Scene View)")]
        [Tooltip("Рисовать границы зоны в Scene View.")]
        [SerializeField] private bool showGizmo = true;

        [Tooltip("Цвет границ зоны.")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.75f, 0.2f, 0.9f);
#endif

        private bool _enterConsumed;
        private bool _exitConsumed;
        private bool _stayConsumed;

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            if (volume != null)
                volume.isTrigger = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmo)
                return;

            Collider volume = GetComponent<Collider>();
            if (volume == null)
                return;

            bool selected = UnityEditor.Selection.activeGameObject == gameObject;

            Color color = gizmoColor;
            if (selected)
                color.a = 1f;

            Gizmos.color = color;

            if (volume is BoxCollider box)
            {
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);

                if (selected)
                {
                    Color fill = color;
                    fill.a = 0.12f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(box.center, box.size);
                }

                Gizmos.matrix = previous;
            }
            else
            {
                Bounds bounds = volume.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);

                if (selected)
                {
                    Color fill = color;
                    fill.a = 0.12f;
                    Gizmos.color = fill;
                    Gizmos.DrawCube(bounds.center, bounds.size);
                }
            }

            if (selected)
                UnityEditor.Handles.Label(volume.bounds.center + Vector3.up * (volume.bounds.extents.y + 0.3f), name);
        }
#endif

        public string RequiredStep => requiredStep;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other) || _enterConsumed) return;

            if (Fire(onEnter)) {
                _stayConsumed = true;
                if (once) _enterConsumed = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other) || _exitConsumed) return;
            if (Fire(onExit) && once) _exitConsumed = true;
            _stayConsumed = false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsPlayer(other) || _enterConsumed || _stayConsumed)
                return;

            if (Fire(onEnter)) {
                _stayConsumed = true;
                if (once) _enterConsumed = true;
            }
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && other.CompareTag(playerTag);
        }

        /// <summary>Возвращает true, если триггер был использован и его можно пометить как использованный</summary>
        private bool Fire(List<StoryAction> actions)
        {
            if (!string.IsNullOrEmpty(requiredStep) && !QuestDirector.IsStepActive(requiredStep))
                return false;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;

            if (state != null && !string.IsNullOrEmpty(skipIfFlag) && state.GetFlag(skipIfFlag))
                return true;

            if (state != null && !string.IsNullOrEmpty(completedFlag))
                state.SetFlag(completedFlag, true);

            if (actions == null || actions.Count == 0)
                return true;

            if (lockPlayerWhileRunning)
                StartCoroutine(RunLocked(actions));
            else
                StoryDirector.Ensure().Run(actions, transform.position);
            return true;
        }

        private IEnumerator RunLocked(List<StoryAction> actions)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            PlayerMotor motor = player != null ? player.GetComponent<PlayerMotor>() : null;
            PlayerLook look = player != null ? player.GetComponent<PlayerLook>() : null;

            if (motor != null) motor.SetControlLocked(true);

            if (look != null) look.PoseLocked = true;

            yield return StoryDirector.Ensure().RunBlocking(actions, transform.position);

            if (motor != null) motor.SetControlLocked(false);
            if (look != null) look.PoseLocked = false;
        }
    }
}

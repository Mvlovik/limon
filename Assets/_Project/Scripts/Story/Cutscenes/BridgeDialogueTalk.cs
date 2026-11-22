using System;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Лёгкая «речь» статиста: во время реплики медленно кивает и слегка
    /// поворачивает голову, имитируя движение рта без blendShape. Работает
    /// как в первой/третьей сценах — но для моделей, где blendShape нет.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeDialogueTalk : MonoBehaviour
    {
        [Serializable]
        public sealed class Speaker
        {
            [SerializeField] private string key = string.Empty;
            [SerializeField] private GameObject actor;

            public string Key => key;
            public GameObject Actor => actor;
            public Transform Head { get; private set; }
            public Transform Neck { get; private set; }
            public Quaternion HeadRest { get; private set; }
            public Quaternion NeckRest { get; private set; }

            public void Bind()
            {
                Head = null;
                Neck = null;
                if (actor == null) return;
                foreach (var t in actor.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "mixamorig:Head") Head = t;
                    else if (t.name == "mixamorig:Neck") Neck = t;
                }
                if (Head != null) HeadRest = Head.localRotation;
                if (Neck != null) NeckRest = Neck.localRotation;
            }
        }

        [SerializeField] private Speaker[] speakers = Array.Empty<Speaker>();

        private Speaker activeSpeaker;
        private float startedAt;

        public void Speak(string key, float duration)
        {
            if (string.IsNullOrEmpty(key) || duration <= 0f) return;
            var speaker = FindSpeaker(key);
            if (speaker == null) return;
            BindIfNecessary(speaker);
            activeSpeaker = speaker;
            startedAt = Time.time;
            speechEnd = Time.time + duration;
        }

        public void Silence()
        {
            Restore(activeSpeaker);
            activeSpeaker = null;
        }

        private float speechEnd;

        private void LateUpdate()
        {
            if (activeSpeaker == null) return;
            if (Time.time > speechEnd)
            {
                Silence();
                return;
            }

            float elapsed = Time.time - startedAt;
            float nod = Mathf.Sin(elapsed * 9.2f) * 0.06f + Mathf.Sin(elapsed * 3.4f) * 0.03f;
            float yaw = Mathf.Sin(elapsed * 5.6f) * 0.05f;
            var head = activeSpeaker.Head;
            var neck = activeSpeaker.Neck;
            if (head != null) head.localRotation = activeSpeaker.HeadRest * Quaternion.Euler(nod * 26f, yaw * 18f, 0f);
            if (neck != null) neck.localRotation = activeSpeaker.NeckRest * Quaternion.Euler(nod * 12f, yaw * 8f, 0f);
        }

        private Speaker FindSpeaker(string key)
        {
            foreach (var speaker in speakers)
                if (speaker != null && string.Equals(speaker.Key, key, StringComparison.OrdinalIgnoreCase))
                    return speaker;
            return null;
        }

        private void BindIfNecessary(Speaker speaker)
        {
            if (speaker.Head != null || speaker.Neck != null) return;
            speaker.Bind();
        }

        private void Restore(Speaker speaker)
        {
            if (speaker == null) return;
            if (speaker.Head != null) speaker.Head.localRotation = speaker.HeadRest;
            if (speaker.Neck != null) speaker.Neck.localRotation = speaker.NeckRest;
        }

        private void OnDisable()
        {
            Silence();
        }
    }
}

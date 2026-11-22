using UnityEngine;

namespace GameLimon.Companions
{
    /// <summary>
    /// Проигрывает один «сырой» idle-клип (Legacy) на персонаже сцены 4.
    /// Пока катсцена идёт до перестрелки — клип зациклен; перед выстрелом
    /// останавливается и возвращает скелет в исходную позу.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animation))]
    public sealed class BridgeIdleActor : MonoBehaviour
    {
        [SerializeField] private AnimationClip clip;

        private Animation _animation;
        private Transform[] _bones;
        private Vector3[] _restPositions;
        private Quaternion[] _restRotations;
        private bool _playing;

        public AnimationClip Clip => clip;

        private void Awake()
        {
            _animation = GetComponent<Animation>();
            CacheRestPose();
        }

        public void Play()
        {
            if (_animation == null)
                _animation = GetComponent<Animation>();

            if (_animation == null || clip == null)
                return;

            if (_animation.GetClip(clip.name) == null)
                _animation.AddClip(clip, clip.name);

            _animation.playAutomatically = false;
            _animation.wrapMode = WrapMode.Loop;
            _animation.clip = clip;
            _animation.Play(clip.name);

            AnimationState state = _animation[clip.name];
            if (state != null)
            {
                state.wrapMode = WrapMode.Loop;
                state.time = 0f;
                state.speed = 1f;
            }

            _playing = true;
        }

        public void Stop()
        {
            if (!_playing)
                return;

            _playing = false;

            if (_animation != null)
                _animation.Stop();

            RestoreRestPose();
        }

        /// <summary>Повторно запоминает текущую позу как исходную.</summary>
        public void Rebase()
        {
            CacheRestPose();
        }

        private void CacheRestPose()
        {
            // Корень персонажа не трогаем: его позицию/поворот задаёт сцена
            // (и укладка тел после перестрелки).
            var bones = GetComponentsInChildren<Transform>(true);
            int count = 0;
            foreach (Transform bone in bones)
                if (bone != transform)
                    count++;

            _bones = new Transform[count];
            _restPositions = new Vector3[count];
            _restRotations = new Quaternion[count];

            int index = 0;
            foreach (Transform bone in bones)
            {
                if (bone == transform)
                    continue;

                _bones[index] = bone;
                _restPositions[index] = bone.localPosition;
                _restRotations[index] = bone.localRotation;
                index++;
            }
        }

        private void RestoreRestPose()
        {
            if (_bones == null || _restPositions == null || _restRotations == null)
                return;

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null)
                    continue;

                _bones[i].localPosition = _restPositions[i];
                _bones[i].localRotation = _restRotations[i];
            }
        }
    }
}

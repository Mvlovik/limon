using UnityEngine;

namespace GameLimon.Player
{
    /// <summary>
    /// First-person тело игрока: скрывает голову, управляет параметрами
    /// аниматора (скорость/присед) и подгибает ноги в прыжке.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMotor motor;

        [Tooltip("Отношение скорости спринта к скорости шага. Нужно для нормализации Speed.")]
        [SerializeField] private float sprintToWalkRatio = 1.625f;

        private Transform _head;
        private Transform _spine;
        private Transform _spine1;
        private Transform _spine2;
        private Transform _neck;
        private Transform _leftUpLeg;
        private Transform _rightUpLeg;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private float _airTime;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (motor == null)
                motor = GetComponentInParent<PlayerMotor>();

            _head = FindBone("Head");
            _spine = FindBone("Spine");
            _spine1 = FindBone("Spine1");
            _spine2 = FindBone("Spine2");
            _neck = FindBone("Neck");
            _leftUpLeg = FindBone("LeftUpLeg");
            _rightUpLeg = FindBone("RightUpLeg");
            _leftLeg = FindBone("LeftLeg");
            _rightLeg = FindBone("RightLeg");
        }

        private void Update()
        {
            if (animator == null || motor == null)
                return;

            float ratio = Mathf.Max(1f, sprintToWalkRatio);
            float blend = Mathf.Clamp01(motor.Speed01 / ratio);
            bool crouching = motor.CrouchAmount > 0.5f;

            animator.SetFloat("Speed", blend, 0.08f, Time.deltaTime);
            animator.SetFloat("PlaybackSpeed", Mathf.Lerp(0.9f, 1.0f, blend));
            animator.SetFloat("CrouchSpeed", Mathf.Lerp(0.1f, 1f, blend));
            animator.SetBool("Crouching", crouching);

            _airTime = motor.IsGrounded ? 0f : _airTime + Time.deltaTime;
        }

        private void LateUpdate()
        {
            if (animator == null)
                return;

            // Голова не должна попадать в камеру от первого лица.
            if (_head != null)
                _head.localScale = Vector3.one * 0.001f;

            if (motor == null)
                return;

            Vector3 axis = motor.transform.right;

            // Idle-поза статичная (первый кадр ItemPickup), поэтому добавляем
            // лёгкое дыхание корпусом, иначе персонаж выглядит статуей.
            bool idle = motor.Speed01 < 0.05f && motor.CrouchAmount < 0.5f && motor.IsGrounded;
            if (idle)
            {
                float breath = Mathf.Sin(Time.time * 1.8f);
                RotateWorld(_spine, breath * 0.45f, axis);
                RotateWorld(_spine1, breath * 0.65f, axis);
                RotateWorld(_spine2, -breath * 0.35f, axis);
                RotateWorld(_neck, -breath * 0.45f, axis);
            }

            if (motor.IsGrounded || _airTime <= 0f)
                return;

            // В воздухе Animator продолжает локомоцию; ноги подгибаем поверх.
            float tuck = Mathf.Clamp01(_airTime / 0.12f);
            RotateWorld(_leftUpLeg, -38f * tuck, axis);
            RotateWorld(_rightUpLeg, -38f * tuck, axis);
            RotateWorld(_leftLeg, 55f * tuck, axis);
            RotateWorld(_rightLeg, 55f * tuck, axis);
        }

        private Transform FindBone(string boneName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            foreach (Transform bone in all)
            {
                if (bone.name == boneName ||
                    bone.name.EndsWith(":" + boneName, System.StringComparison.OrdinalIgnoreCase))
                    return bone;
            }

            return null;
        }

        private static void RotateWorld(Transform bone, float angle, Vector3 axis)
        {
            if (bone != null)
                bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }
    }
}

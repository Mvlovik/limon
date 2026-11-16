using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Staging
{
    /// <summary>Visual traffic along straight lanes. All points are local to each lane's container.
    /// Existing child cars form a pool; an optional prefab supplies additional cars up to poolLimit.
    /// Put each direction in a separate lane and reverse its start/end points.</summary>
    [AddComponentMenu("GameLimon/Staging/Traffic Controller")]
    [DisallowMultipleComponent]
    public sealed class TrafficController : MonoBehaviour
    {
        [Serializable]
        public sealed class Lane
        {
            public Transform container;
            public GameObject carPrefab;
            public Vector3 start = new Vector3(0, 0, -162);
            public Vector3 end = new Vector3(0, 0, -12);
            [Min(0.1f)] public float speed = 12f;
            [Min(0f)] public float initialDelay;
            [Min(1)] public int poolLimit = 22;
        }

        [SerializeField] private Lane[] lanes = Array.Empty<Lane>();
        [Tooltip("Time between the starts of successive batches on each lane.")]
        [SerializeField, Min(0.1f)] private float batchInterval = 6f;
        [SerializeField, Range(0f, 1f)] private float groupChance = 0.5f;
        [SerializeField, Range(2, 6)] private int maxGroupSize = 3;
        [SerializeField] private Vector2 groupGap = new Vector2(1f, 2f);

        private sealed class Car
        {
            public Transform transform;
            public Vector3 scale;
            public bool moving;
        }
        private sealed class LaneState
        {
            public Lane settings;
            public readonly List<Car> cars = new List<Car>();
        }
        private readonly List<LaneState> _states = new List<LaneState>();

        private void OnEnable()
        {
            foreach (var lane in lanes)
            {
                if (lane == null || lane.container == null || (lane.end - lane.start).sqrMagnitude < 0.001f)
                    continue;
                var state = new LaneState { settings = lane };
                foreach (Transform child in lane.container)
                {
                    var car = new Car { transform = child,
                        scale = child.localScale.sqrMagnitude > 0.001f ? child.localScale : Vector3.one };
                    child.gameObject.SetActive(false);
                    state.cars.Add(car);
                }
                _states.Add(state);
                StartCoroutine(SpawnLane(state));
            }
        }

        private IEnumerator SpawnLane(LaneState state)
        {
            if (state.settings.initialDelay > 0f)
                yield return new WaitForSeconds(state.settings.initialDelay);
            while (true)
            {
                float started = Time.time;
                int count = UnityEngine.Random.value < groupChance ? UnityEngine.Random.Range(2, maxGroupSize + 1) : 1;
                for (int i = 0; i < count; i++)
                {
                    SpawnCar(state);
                    if (i + 1 < count)
                        yield return new WaitForSeconds(UnityEngine.Random.Range(groupGap.x, groupGap.y));
                }
                yield return new WaitForSeconds(Mathf.Max(0.1f, batchInterval - (Time.time - started)));
            }
        }

        private static void SpawnCar(LaneState state)
        {
            Car car = state.cars.Find(candidate => !candidate.moving && candidate.transform != null);
            var lane = state.settings;
            if (car == null && lane.carPrefab != null && state.cars.Count < lane.poolLimit)
            {
                var instance = Instantiate(lane.carPrefab, lane.container).transform;
                car = new Car { transform = instance, scale = instance.localScale };
                state.cars.Add(car);
            }
            if (car == null) return; // An exhausted pool never interrupts a car already on the road.
            car.transform.localPosition = lane.start;
            car.transform.localRotation = Quaternion.LookRotation(lane.end - lane.start, Vector3.up);
            car.transform.localScale = car.scale;
            car.transform.gameObject.SetActive(true);
            car.moving = true;
        }

        private void Update()
        {
            foreach (var state in _states)
                foreach (var car in state.cars)
                {
                    if (!car.moving || car.transform == null) continue;
                    // Speed is measured in world units, including when the lane container is scaled.
                    Vector3 end = state.settings.container.TransformPoint(state.settings.end);
                    car.transform.position = Vector3.MoveTowards(car.transform.position, end,
                        Mathf.Max(0.1f, state.settings.speed) * Time.deltaTime);
                    if ((car.transform.position - end).sqrMagnitude > 0.0001f) continue;
                    car.moving = false;
                    car.transform.gameObject.SetActive(false);
                }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (var state in _states)
                foreach (var car in state.cars)
                    if (car.transform != null) car.transform.gameObject.SetActive(false);
            _states.Clear();
        }

        private void OnValidate()
        {
            batchInterval = Mathf.Max(0.1f, batchInterval);
            maxGroupSize = Mathf.Clamp(maxGroupSize, 2, 6);
            groupGap.x = Mathf.Max(0.1f, groupGap.x);
            groupGap.y = Mathf.Max(groupGap.x, groupGap.y);
        }
    }
}

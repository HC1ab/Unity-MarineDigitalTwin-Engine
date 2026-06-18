using System.Collections.Generic;
using UnityEngine;

namespace MarineDigitalTwin.Boat
{
    /// <summary>
    /// RL 학습용 절차적 장애물 배치.
    /// 프리미티브 콜라이더(Box/Sphere)만 사용 — 시각 메쉬 없음.
    /// ResetCourse()를 매 에피소드 시작 시 호출.
    /// </summary>
    public class CourseGenerator : MonoBehaviour
    {
        [Header("Course Bounds")]
        public Vector3 courseCenter  = Vector3.zero;
        public float   courseWidth   = 100f;   // Z 축
        public float   courseLength  = 200f;   // X 축

        [Header("Obstacles")]
        public int   obstacleCount   = 30;
        public float minSize         = 2f;
        public float maxSize         = 6f;
        [Tooltip("이 범위 안은 장애물 생성 제외 (보트 스폰 보호)")]
        public float spawnClearRadius = 15f;

        [Header("Boundary Walls")]
        public bool  buildWalls      = true;
        public float wallHeight      = 5f;
        public float wallThickness   = 2f;

        [Header("Seed")]
        [Tooltip("0 = 매번 랜덤")]
        public int seed = 0;

        // Layer 8 = Obstacle (TagManager 기준)
        const int ObstacleLayer = 8;

        readonly List<GameObject> _spawned = new();

        // ── 외부 호출 ──────────────────────────────────────────────────────

        public void ResetCourse()
        {
            Clear();
            Generate();
        }

        void Start() => Generate();

        // ── 생성 ──────────────────────────────────────────────────────────

        void Generate()
        {
            var rng = seed == 0 ? new System.Random() : new System.Random(seed);

            if (buildWalls) BuildWalls();
            PlaceObstacles(rng);
        }

        void BuildWalls()
        {
            float halfLen = courseLength * 0.5f;
            float halfW   = courseWidth  * 0.5f;

            // 좌우 벽 (Z ±)
            SpawnBox("Wall_Port",
                courseCenter + new Vector3(0, wallHeight * 0.5f,  halfW + wallThickness * 0.5f),
                new Vector3(courseLength, wallHeight, wallThickness));

            SpawnBox("Wall_Stbd",
                courseCenter + new Vector3(0, wallHeight * 0.5f, -halfW - wallThickness * 0.5f),
                new Vector3(courseLength, wallHeight, wallThickness));

            // 전후 벽 (X ±)
            SpawnBox("Wall_Fore",
                courseCenter + new Vector3( halfLen + wallThickness * 0.5f, wallHeight * 0.5f, 0),
                new Vector3(wallThickness, wallHeight, courseWidth + wallThickness * 2f));

            SpawnBox("Wall_Aft",
                courseCenter + new Vector3(-halfLen - wallThickness * 0.5f, wallHeight * 0.5f, 0),
                new Vector3(wallThickness, wallHeight, courseWidth + wallThickness * 2f));
        }

        void PlaceObstacles(System.Random rng)
        {
            float halfLen = courseLength * 0.5f;
            float halfW   = courseWidth  * 0.5f;

            int placed   = 0;
            int attempts = 0;
            int maxTry   = obstacleCount * 20;

            while (placed < obstacleCount && attempts < maxTry)
            {
                attempts++;

                float x = Lerp(rng, -halfLen, halfLen);
                float z = Lerp(rng, -halfW,   halfW);
                var   pos = courseCenter + new Vector3(x, 0f, z);

                // 스폰 보호 구역
                if (Vector3.Distance(pos, courseCenter) < spawnClearRadius) continue;

                float size = Lerp(rng, minSize, maxSize);

                // 50/50 박스 or 구
                GameObject go = rng.NextDouble() < 0.5f
                    ? SpawnBox($"Obs_B_{placed}", pos + Vector3.up * size * 0.5f, Vector3.one * size)
                    : SpawnSphere($"Obs_S_{placed}", pos + Vector3.up * size * 0.5f, size * 0.5f);

                placed++;
            }

            if (placed < obstacleCount)
                Debug.LogWarning($"[CourseGenerator] {placed}/{obstacleCount} 배치 (공간 부족)");
        }

        // ── 프리미티브 헬퍼 ───────────────────────────────────────────────

        GameObject SpawnBox(string goName, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(goName);
            go.layer = ObstacleLayer;
            go.transform.SetParent(transform);
            go.transform.position = pos;

            var col = go.AddComponent<BoxCollider>();
            col.size = size;

            _spawned.Add(go);
            return go;
        }

        GameObject SpawnSphere(string goName, Vector3 pos, float radius)
        {
            var go = new GameObject(goName);
            go.layer = ObstacleLayer;
            go.transform.SetParent(transform);
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.radius = radius;

            _spawned.Add(go);
            return go;
        }

        void Clear()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }

        static float Lerp(System.Random rng, float min, float max)
            => min + (float)rng.NextDouble() * (max - min);

        // ── 기즈모 ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.DrawCube(courseCenter, new Vector3(courseLength, 1f, courseWidth));
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(courseCenter, spawnClearRadius);
        }
#endif
    }
}

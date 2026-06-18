using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarineDigitalTwin.Boat
{
    /// <summary>
    /// Perlin noise 기반 섬 메쉬 + 해안선 암초 클러스터 절차 생성.
    /// HDRP WaterSurface Y=0 고정 기준 — 수면 위 부분만 시각적으로 노출됨.
    /// </summary>
    [ExecuteAlways]
    public class IslandGenerator : MonoBehaviour
    {
        [Header("Island Mesh")]
        public int   resolution    = 64;        // 높이맵 해상도 (64~128 권장)
        public float islandRadius  = 40f;       // 섬 반경 (m)
        public float maxHeight     = 15f;       // 최대 높이 (m)
        public float noiseScale    = 0.08f;     // Perlin 노이즈 스케일 (작을수록 완만)
        public float noiseOffsetX  = 0f;        // 노이즈 오프셋 X (다른 형태 생성)
        public float noiseOffsetZ  = 0f;        // 노이즈 오프셋 Z
        [Range(0f, 1f)]
        public float shoreBlend    = 0.35f;     // 해안선 블렌드 비율 (클수록 완만한 해안)

        [Header("Material")]
        public Material islandMaterial;         // null이면 기본 흰색

        [Header("Reefs")]
        public int   reefCount         = 20;
        public float reefMinRadius     = 45f;   // 섬 외곽에서 (islandRadius + 5)
        public float reefMaxRadius     = 70f;   // 섬 외곽 최대 (islandRadius + 30)
        public float reefMinSize       = 1f;
        public float reefMaxSize       = 4f;
        public int   reefSeed          = 0;    // 0 = 매번 랜덤

        [Header("Seabed (수중 지형)")]
        public float seabedMaxDepth   = 20f;   // 섬 외곽 최대 수심 (m)
        public float seabedFadeRadius = 1.4f;  // islandRadius 대비 수중 지형 범위 배수
        public int   seabedLayer      = 9;

        [Header("Layer")]
        public int obstacleLayer = 8;

        readonly List<GameObject> _spawned = new();
        GameObject                _islandGo;

        void Start() => Generate();

        public void Regenerate()
        {
            ClearInEditor();
            Generate();
        }

        void Generate()
        {
            _islandGo = BuildIslandMesh();
            BuildSeabedMesh();
            PlaceReefs();
        }

        // ── 섬 메쉬 생성 ──────────────────────────────────────────────────

        GameObject BuildIslandMesh()
        {
            var go = new GameObject("Island_Mesh");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.layer = obstacleLayer;

            int verts = resolution + 1;
            var vertices  = new Vector3[verts * verts];
            var triangles = new int[resolution * resolution * 6];
            var uvs       = new Vector2[verts * verts];

            float step = islandRadius * 2f / resolution;
            float origin = -islandRadius;

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float wx = origin + x * step;
                    float wz = origin + z * step;

                    // 중심 거리 기반 마스크 (원형 섬)
                    float dist   = Mathf.Sqrt(wx * wx + wz * wz);
                    float mask   = Mathf.Clamp01(1f - dist / islandRadius);
                    // 해안선 블렌드: 가장자리를 더 완만하게
                    mask = Mathf.SmoothStep(0f, 1f, mask / Mathf.Max(shoreBlend, 0.01f));
                    mask = Mathf.Clamp01(mask);

                    float noise = Mathf.PerlinNoise(
                        wx * noiseScale + noiseOffsetX,
                        wz * noiseScale + noiseOffsetZ);

                    float height = noise * maxHeight * mask;

                    int idx = z * verts + x;
                    vertices[idx] = new Vector3(wx, height, wz);
                    uvs[idx]      = new Vector2((float)x / resolution, (float)z / resolution);
                }
            }

            int ti = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bl = z * verts + x;
                    int br = bl + 1;
                    int tl = bl + verts;
                    int tr = tl + 1;

                    triangles[ti++] = bl; triangles[ti++] = tl; triangles[ti++] = tr;
                    triangles[ti++] = bl; triangles[ti++] = tr; triangles[ti++] = br;
                }
            }

            var mesh = new Mesh { name = "IslandMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = vertices;
            mesh.triangles   = triangles;
            mesh.uv          = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = islandMaterial != null
                ? islandMaterial
                : new Material(Shader.Find("HDRP/Lit"));

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.isTrigger = true; // 섬 도형은 시각 전용 — 실제 충돌은 해안 암초 SphereCollider 담당

            _spawned.Add(go);
            return go;
        }

        // ── 수중 지형 메쉬 ────────────────────────────────────────────────

        void BuildSeabedMesh()
        {
            var go = new GameObject("Island_Seabed");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.layer = seabedLayer;

            float seabedRadius = islandRadius * seabedFadeRadius;
            int verts = resolution + 1;
            var vertices  = new Vector3[verts * verts];
            var triangles = new int[resolution * resolution * 6];
            var uvs       = new Vector2[verts * verts];

            float step   = seabedRadius * 2f / resolution;
            float origin = -seabedRadius;

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float wx = origin + x * step;
                    float wz = origin + z * step;

                    float dist = Mathf.Sqrt(wx * wx + wz * wz);

                    float height;
                    if (dist <= islandRadius)
                    {
                        // 섬 아래 — 중심은 얕고(0) 해안으로 갈수록 깊어짐
                        float t = dist / islandRadius;
                        height = -Mathf.SmoothStep(0f, seabedMaxDepth * 0.3f, t);
                    }
                    else
                    {
                        // 섬 외곽 — islandRadius에서 seabedRadius까지 최대 수심으로
                        float t = (dist - islandRadius) / (seabedRadius - islandRadius);
                        height = -Mathf.SmoothStep(seabedMaxDepth * 0.3f, seabedMaxDepth, t);
                    }

                    int idx = z * verts + x;
                    vertices[idx] = new Vector3(wx, height, wz);
                    uvs[idx]      = new Vector2((float)x / resolution, (float)z / resolution);
                }
            }

            int ti = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bl = z * verts + x;
                    int br = bl + 1;
                    int tl = bl + verts;
                    int tr = tl + 1;
                    // 법선이 위를 향하도록 (Raycast 위에서 아래로)
                    triangles[ti++] = bl; triangles[ti++] = tr; triangles[ti++] = tl;
                    triangles[ti++] = bl; triangles[ti++] = br; triangles[ti++] = tr;
                }
            }

            var mesh = new Mesh { name = "SeabedMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = vertices;
            mesh.triangles   = triangles;
            mesh.uv          = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            // MeshRenderer 비활성 — 충돌만 필요
            var mr = go.AddComponent<MeshRenderer>();
            mr.enabled = false;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.isTrigger = true; // 물리 충돌 없이 Raycast만 감지

            _spawned.Add(go);
        }

        // ── 암초 배치 ─────────────────────────────────────────────────────

        void PlaceReefs()
        {
            var rng = reefSeed == 0 ? new System.Random() : new System.Random(reefSeed);

            for (int i = 0; i < reefCount; i++)
            {
                float angle  = (float)(rng.NextDouble() * 360f);
                float radius = Lerp(rng, reefMinRadius, reefMaxRadius);
                float rad    = angle * Mathf.Deg2Rad;
                var   pos    = transform.position + new Vector3(
                    Mathf.Cos(rad) * radius,
                    0f,
                    Mathf.Sin(rad) * radius);

                float size = Lerp(rng, reefMinSize, reefMaxSize);
                SpawnReef($"Reef_{i}", pos, size);
            }
        }

        void SpawnReef(string goName, Vector3 pos, float size)
        {
            var go = new GameObject(goName);
            go.layer = obstacleLayer;
            go.transform.SetParent(transform);
            go.transform.position  = pos + Vector3.up * (size * 0.15f);
            go.transform.localScale = new Vector3(size, size * 0.3f, size);

            // SphereCollider radius=0.5 → transform scale로 납작 타원형 콜라이더
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildReefMesh();

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = islandMaterial != null
                ? islandMaterial
                : new Material(Shader.Find("HDRP/Lit"));

            _spawned.Add(go);
        }

        Mesh BuildReefMesh()
        {
            var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = Mesh.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            DestroyImmediate(go);
            return mesh;
        }

        // ── 정리 ──────────────────────────────────────────────────────────

        void Clear()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            _islandGo = null;
        }

        public void ClearInEditor()
        {
            foreach (var go in _spawned)
                if (go != null) DestroyImmediate(go);
            _spawned.Clear();
            _islandGo = null;
        }

        static float Lerp(System.Random rng, float min, float max)
            => min + (float)rng.NextDouble() * (max - min);

        // ── 기즈모 ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 0.2f, 0.3f);
            Gizmos.DrawSphere(transform.position, islandRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, reefMaxRadius);
        }
#endif
    }
}

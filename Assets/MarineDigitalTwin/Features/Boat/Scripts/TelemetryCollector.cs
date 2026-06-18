using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MarineDigitalTwin.Boat
{
    // ── Request DTOs ─────────────────────────────────────────────────────────

    [Serializable]
    class CreateSessionRequest
    {
        public string clientId;     // UUID-v4
        public string scenarioName;
    }

    [Serializable]
    class EndSessionRequest
    {
        public string status; // "COMPLETED" | "ABORTED"
    }

    [Serializable]
    class VesselLogDto
    {
        public string recordedAt;
        public double latitude;
        public double longitude;
        public double speedKn;
        public double headingDeg;
        public double rudderAngle;
        public double throttle;
        public double roll;
        public double pitch;
        public double yaw;
    }

    [Serializable]
    class EnvironmentLogDto
    {
        public string recordedAt;
        public double windSpeed;
        public double windDirection;
        public double waveHeight;
        public double currentSpeed;
        public double currentDirection;
        public double tideLevel;
        public double visibility;
    }

    [Serializable]
    class EventDto
    {
        public string eventType;
        public string eventTime;
        public string severity;
        public string description;
        public double latitude;
        public double longitude;
    }

    [Serializable]
    class BulkTelemetryRequest
    {
        public VesselLogDto[]      vesselLogs;
        public EnvironmentLogDto[] environmentLogs;
        public EventDto[]          events;
    }

    // ── Response DTOs ────────────────────────────────────────────────────────

    [Serializable]
    class CreateSessionData
    {
        public long sessionId;
    }

    [Serializable]
    class ApiResponse_CreateSession
    {
        public bool              success;
        public CreateSessionData data;
        public string            error;
    }

    // ── TelemetryCollector ───────────────────────────────────────────────────

    /// <summary>
    /// 세션 시작/종료 + 1초 샘플링 + 5초 배치 전송.
    /// POST /api/v1/sessions → POST /api/v1/sessions/{id}/telemetry → PATCH /api/v1/sessions/{id}/end
    /// </summary>
    public class TelemetryCollector : MonoBehaviour
    {
        [Header("Backend")]
        public string baseUrl      = "http://59.22.109.145:8082/api/v1";
        public string scenarioName = "기본 시나리오";

        [Header("Collection")]
        [Tooltip("선박 상태 샘플링 주기 (s)")]
        public float sampleInterval = 1f;
        [Tooltip("백엔드 전송 주기 (s)")]
        public float sendInterval   = 5f;

        [Header("Coordinate Base — 부산 앞바다")]
        public double baseLat = 35.1;
        public double baseLon = 129.0;

        [Header("Auto Start")]
        public bool autoStart = false;

        [Header("Debug")]
        public bool showDebugUI = true;

        // ── 내부 참조 ─────────────────────────────────────────────────────────

        BoatMMGController _mmg;
        EventDetector     _eventDetector;

        // ── 세션 상태 ─────────────────────────────────────────────────────────

        string _clientId   = "";
        long   _sessionId  = -1;
        bool   _collecting = false;

        // ── 배치 버퍼 ─────────────────────────────────────────────────────────

        readonly List<VesselLogDto>      _vesselBuf = new();
        readonly List<EnvironmentLogDto> _envBuf    = new();
        readonly List<EventDto>          _eventBuf  = new();

        // ── UI 상태 ───────────────────────────────────────────────────────────

        GUIStyle _boxStyle;
        GUIStyle _labelStyle;
        int      _sentBatches;
        int      _totalVesselLogs;
        string   _lastStatus = "대기 중";

        // ── 라이프사이클 ──────────────────────────────────────────────────────

        void Awake()
        {
            _mmg           = GetComponent<BoatMMGController>();
            _eventDetector = GetComponentInChildren<EventDetector>(true);

            // 클라이언트 UUID — PlayerPrefs에 영구 보관
            _clientId = PlayerPrefs.GetString("safesail_client_id", "");
            if (string.IsNullOrEmpty(_clientId))
            {
                _clientId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("safesail_client_id", _clientId);
                PlayerPrefs.Save();
            }
        }

        void Start()
        {
            if (autoStart) StartSession();
        }

        void OnApplicationQuit()
        {
            if (_collecting) StartCoroutine(CoEndSession("ABORTED"));
        }

        // ── 공개 API ──────────────────────────────────────────────────────────

        public void StartSession()
        {
            if (_collecting)
            {
                Debug.LogWarning("[TelemetryCollector] 이미 세션 진행 중");
                return;
            }
            StartCoroutine(CoStartSession());
        }

        public void EndSession() => EndSession("COMPLETED");

        public void EndSession(string status)
        {
            if (!_collecting) return;
            StartCoroutine(CoEndSession(status));
        }

        // ── 세션 시작 ─────────────────────────────────────────────────────────

        IEnumerator CoStartSession()
        {
            _lastStatus = "세션 시작 중...";

            var req = new CreateSessionRequest
            {
                clientId     = _clientId,
                scenarioName = scenarioName,
            };

            yield return StartCoroutine(CoPost(
                $"{baseUrl}/sessions",
                JsonUtility.ToJson(req),
                (text, err) =>
                {
                    if (err != null)
                    {
                        _lastStatus = $"세션 시작 실패: {err}";
                        Debug.LogError($"[TelemetryCollector] {_lastStatus}");
                        return;
                    }

                    var resp = JsonUtility.FromJson<ApiResponse_CreateSession>(text);
                    if (!resp.success || resp.data == null)
                    {
                        _lastStatus = $"세션 시작 실패: {resp.error}";
                        Debug.LogError($"[TelemetryCollector] {_lastStatus}");
                        return;
                    }

                    _sessionId       = resp.data.sessionId;
                    _collecting      = true;
                    _sentBatches     = 0;
                    _totalVesselLogs = 0;
                    _lastStatus      = $"수집 중 (sessionId={_sessionId})";
                    Debug.Log($"[TelemetryCollector] 세션 시작 — id={_sessionId}");

                    StartCoroutine(CoSample());
                    StartCoroutine(CoSend());
                }
            ));
        }

        // ── 샘플링 (sampleInterval마다) ───────────────────────────────────────

        IEnumerator CoSample()
        {
            while (_collecting)
            {
                yield return new WaitForSeconds(sampleInterval);
                if (!_collecting) break;

                string ts = DateTime.UtcNow.ToString("o");
                (double lat, double lon) = WorldToLatLon(transform.position);

                Vector3 euler  = transform.eulerAngles;
                float speedKn  = _mmg != null ? _mmg.GetSpeedKn()     : 0f;
                float rudder   = _mmg != null ? _mmg.rudderAngleDeg   : 0f;
                float throttle = _mmg != null ? _mmg.ThrottleInput    : 0f;
                float roll     = euler.z > 180f ? euler.z - 360f : euler.z;
                float pitch    = euler.x > 180f ? euler.x - 360f : euler.x;

                _vesselBuf.Add(new VesselLogDto
                {
                    recordedAt  = ts,
                    latitude    = lat,
                    longitude   = lon,
                    speedKn     = speedKn,
                    headingDeg  = euler.y,
                    rudderAngle = rudder,
                    throttle    = throttle,
                    roll        = roll,
                    pitch       = pitch,
                    yaw         = euler.y,
                });

                // 환경 (EnvironmentSystem 미구현 — 기본값)
                _envBuf.Add(new EnvironmentLogDto
                {
                    recordedAt       = ts,
                    windSpeed        = 0,
                    windDirection    = 0,
                    waveHeight       = 0,
                    currentSpeed     = 0,
                    currentDirection = 0,
                    tideLevel        = 0,
                    visibility       = 10,
                });

                // 이벤트 큐 소비
                if (_eventDetector != null)
                {
                    while (_eventDetector.EventQueue.Count > 0)
                    {
                        var ev = _eventDetector.EventQueue.Dequeue();
                        (double eLat, double eLon) = WorldToLatLon(ev.position);
                        _eventBuf.Add(new EventDto
                        {
                            eventType   = ev.eventType.ToString(),
                            eventTime   = ev.eventTime.ToString("o"),
                            severity    = ev.severity.ToString(),
                            description = ev.description,
                            latitude    = eLat,
                            longitude   = eLon,
                        });
                    }
                }
            }
        }

        // ── 전송 (sendInterval마다) ───────────────────────────────────────────

        IEnumerator CoSend()
        {
            while (_collecting)
            {
                yield return new WaitForSeconds(sendInterval);
                if (!_collecting) break;
                if (_vesselBuf.Count == 0) continue;
                yield return StartCoroutine(CoPostTelemetry());
            }
        }

        IEnumerator CoPostTelemetry()
        {
            int count = _vesselBuf.Count;

            var payload = new BulkTelemetryRequest
            {
                vesselLogs      = _vesselBuf.ToArray(),
                environmentLogs = _envBuf.ToArray(),
                events          = _eventBuf.ToArray(),
            };
            _vesselBuf.Clear();
            _envBuf.Clear();
            _eventBuf.Clear();

            yield return StartCoroutine(CoPost(
                $"{baseUrl}/sessions/{_sessionId}/telemetry",
                JsonUtility.ToJson(payload),
                (text, err) =>
                {
                    if (err != null)
                    {
                        _lastStatus = $"전송 실패: {err}";
                        Debug.LogError($"[TelemetryCollector] {_lastStatus}");
                    }
                    else
                    {
                        _sentBatches++;
                        _totalVesselLogs += count;
                        Debug.Log($"[TelemetryCollector] 배치 {_sentBatches} 전송 — vesselLogs={count}");
                    }
                }
            ));
        }

        // ── 세션 종료 ─────────────────────────────────────────────────────────

        IEnumerator CoEndSession(string status)
        {
            _collecting = false;
            _lastStatus = "세션 종료 중...";

            if (_vesselBuf.Count > 0)
                yield return StartCoroutine(CoPostTelemetry());

            var body = JsonUtility.ToJson(new EndSessionRequest { status = status });

            yield return StartCoroutine(CoPatch(
                $"{baseUrl}/sessions/{_sessionId}/end",
                body,
                (text, err) =>
                {
                    if (err != null)
                        Debug.LogError($"[TelemetryCollector] 세션 종료 실패: {err}");
                    else
                        Debug.Log($"[TelemetryCollector] 세션 종료({status}) — id={_sessionId}, 총 {_totalVesselLogs}개");
                }
            ));

            _lastStatus = $"종료 완료 ({status}, 총 {_totalVesselLogs}개 로그)";
            _sessionId  = -1;
        }

        // ── HTTP 헬퍼 ─────────────────────────────────────────────────────────

        IEnumerator CoPost(string url, string json, Action<string, string> callback)
        {
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            callback(
                req.downloadHandler.text,
                req.result == UnityWebRequest.Result.Success ? null : req.error
            );
        }

        IEnumerator CoPatch(string url, string json, Action<string, string> callback)
        {
            using var req = new UnityWebRequest(url, "PATCH");
            req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            callback(
                req.downloadHandler.text,
                req.result == UnityWebRequest.Result.Success ? null : req.error
            );
        }

        // ── 좌표 변환 ─────────────────────────────────────────────────────────

        (double lat, double lon) WorldToLatLon(Vector3 pos)
        {
            return (baseLat + pos.z * 0.000009, baseLon + pos.x * 0.0000111);
        }

        // ── 디버그 UI ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!showDebugUI || !Application.isPlaying) return;

            if (_boxStyle == null)
            {
                _boxStyle   = new GUIStyle(GUI.skin.box)   { padding = new RectOffset(8,8,6,6) };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            }

            const float panelW = 340f;
            const float rowH   = 20f;
            float panelH = 26f + rowH * 2 + 36f;
            float px = Screen.width * 0.5f - panelW * 0.5f;
            float py = Screen.height - panelH - 10f;

            GUI.color = new Color(0,0,0,0.65f);
            GUI.Box(new Rect(px-4, py-4, panelW+8, panelH+8), GUIContent.none, _boxStyle);
            GUI.color = Color.white;

            float x = px, y = py;
            _labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, panelW, 26f), "■ TELEMETRY COLLECTOR", _labelStyle);
            y += 26f;

            _labelStyle.normal.textColor = _collecting
                ? new Color(0.3f, 1f, 0.5f)
                : new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(x, y, panelW, rowH), $"  {_lastStatus}", _labelStyle);
            y += rowH;

            _labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(x, y, panelW, rowH),
                      $"  배치={_sentBatches}  총로그={_totalVesselLogs}  버퍼={_vesselBuf.Count}", _labelStyle);
            y += rowH + 4f;

            if (!_collecting)
            {
                if (GUI.Button(new Rect(x, y, 130f, 28f), "▶ 세션 시작"))
                    StartSession();
            }
            else
            {
                if (GUI.Button(new Rect(x, y, 130f, 28f), "■ 세션 종료"))
                    EndSession("COMPLETED");
                if (GUI.Button(new Rect(x + 140f, y, 130f, 28f), "✕ 중단(ABORTED)"))
                    EndSession("ABORTED");
            }
        }
    }
}

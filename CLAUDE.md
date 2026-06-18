# SafeSail — Marine Digital Twin Engine

## 프로젝트 개요

공공 API(기상청·국립해양조사원)로 실제 해상 기상을 근사 재현하는 Unity 해양 운항 시뮬레이터.
운항 데이터를 백엔드(safesail)에 수집하여 자율운항 AI 학습용 ML-ready 데이터셋을 구축한다.

**백엔드 경로:** `~/Desktop/project/safesail/` (Spring Boot 3 / Java 21 / PostgreSQL+PostGIS / Redis / Docker)  
**Unity 엔진:** Unity 6 LTS + HDRP / Crest Water 4 HDRP (v4.22.x) / Unity Sentis (ONNX) / UnityWebRequest

---

## 전체 데이터 플로우

```
┌─────────────────────────────────────────────────────┐
│  공공 API (1시간 배치)                                │
│  기상청 해양기상 부이 → 파고·풍속·풍향              │
│  국립해양조사원 조위관측 → tideLevel                │
│  기상청 단기예보 → 시정(visibility)                 │
└──────────────────────┬──────────────────────────────┘
                       ↓  WeatherScheduler (백엔드)
┌─────────────────────────────────────────────────────┐
│  백엔드 Redis                                        │
│  weather:cache (TTL 1h)  ←  배치 수집값             │
│  weather:manual (TTL 없음) ← 수동 오버라이드 우선   │
└──────────────────────┬──────────────────────────────┘
                       ↓  GET /api/v1/environment/marine?lat=&lon=
┌─────────────────────────────────────────────────────┐
│  Unity — EnvironmentSystem.cs (30초 폴링)   [미구현] │
│  waveHeight  → Crest waveAmplitude = h * 0.5f       │
│  windSpeed   → Rigidbody AddForce (항력 공식)        │
│  windDir     → 힘 방향 벡터 (Sin/Cos)               │
│  tideLevel   → BuoyancySystem.fallbackWaterLevel     │
│  visibility  → RenderSettings.fogDensity             │
└──────────────────────┬──────────────────────────────┘
                       ↓  기상 파라미터 실시간 반영
┌─────────────────────────────────────────────────────┐
│  Unity — 선박 물리 (완성)                            │
│  BoatMMGController  — MMG 모델 추진/조타             │
│  BuoyancySystem     — HDRP 수면 부력                │
│  BoatInputHandler   — W/S/A/D/F/Q/E/Space 조작      │
│  BoatCamera / BoatDebugUI                           │
└──────────────────────┬──────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Unity — EventDetector.cs               [미구현]    │
│  SPEEDING          — speedKn > 임계값               │
│  COLLISION_WARNING — OnCollisionEnter / 전방 레이캐스트│
│  ROUTE_DEVIATION   — Waypoint 이탈 거리 체크        │
│  GROUNDING_WARNING — 얕은 수심 레이캐스트           │
└──────────────────────┬──────────────────────────────┘
                       ↓  이벤트 큐 적재
┌─────────────────────────────────────────────────────┐
│  Unity — TelemetryCollector.cs          [미구현]    │
│  게임 시작  → POST /api/v1/sessions                 │
│  5초 배치   → POST /api/v1/sessions/{id}/telemetry  │
│             (vesselLogs + environmentLogs + events)  │
│  게임 종료  → PATCH /api/v1/sessions/{id}/end        │
└──────────────────────┬──────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  백엔드 PostgreSQL                                   │
│  vessel_logs / environment_logs / event_logs 저장   │
│  세션 종료 트리거 → EvaluationService    [미구현]    │
│  → evaluation_results 자동 생성                     │
└──────────────────────┬──────────────────────────────┘
                       ↓  GET /api/v1/sessions/{id}/report
┌─────────────────────────────────────────────────────┐
│  Unity — 결과 화면 UI                   [미구현]    │
│  totalScore / 충돌·이탈·과속 횟수 / passed 표시     │
└──────────────────────┬──────────────────────────────┘
                       ↓  데이터 충분히 쌓인 후
┌─────────────────────────────────────────────────────┐
│  (병렬) Unity — 레이더 시스템           [미구현]    │
│  RadarSensor  — 선수 Empty GO에 부착                │
│                 Physics.OverlapSphere 360° 탐지     │
│                 LayerMask: 암초·선박·부두 구분       │
│                 → RadarContact (거리·방위각·타입)    │
│  RadarDisplay — radar.png 배경 HUD                  │
│                 스윕 라인 회전 애니메이션            │
│                 탐지 blip 표시 (Cross.png)           │
│  → EventDetector에 COLLISION_WARNING 트리거 연결    │
└──────────────────────┬──────────────────────────────┘
                       ↓  데이터 충분히 쌓인 후
┌─────────────────────────────────────────────────────┐
│  ML 파이프라인                          [미구현]    │
│  Python 배치 추출 → 전처리(슬라이딩 윈도우 30프레임)│
│  LSTM (입력: 30×14피처, 출력: 위험도 0.0~1.0)       │
│  → ONNX Export (opset 17)                           │
│  → Unity Sentis 온디바이스 추론 → 위험도 HUD        │
└─────────────────────────────────────────────────────┘
```

---

## 구현 현황

### 완성
| 위치 | 파일 | 내용 |
|---|---|---|
| Unity | BoatMMGController.cs | MMG 물리 모델 (추진·조타·항력·Prop Walk) |
| Unity | BoatInputHandler.cs | W/S/A/D/F/Q/E/Space 조작, 기어 시스템 |
| Unity | BuoyancySystem.cs | HDRP WaterSurface 부력·댐핑 |
| Unity | BoatDebugUI.cs | 속도·타각·RPS·기어 디버그 HUD |
| Unity | BoatCamera.cs | 선박 추적 카메라 |
| 백엔드 | 전체 API | 세션·텔레메트리·환경·리포트 엔드포인트 |
| 백엔드 | Docker Compose | PostgreSQL+PostGIS, Redis, Spring Boot |
| 백엔드 | Flyway 마이그레이션 | DB 스키마 (V1__init.sql) |

### 미구현 (우선순위 순)
| 우선순위 | 담당 | 작업 |
|---|---|---|
| **P1** | 백엔드 | WeatherScheduler — 공공 API 실연동 (기상청 부이 + 국립해양조사원 조위) |
| **P2** | Unity | EnvironmentSystem.cs — 기상 폴링 + Crest/Wind/Fog 파라미터 적용 |
| **P3** | Unity | RadarSensor.cs + RadarDisplay.cs — 선수 탑재 가상 레이더, 360° 탐지 + HUD |
| **P4** | Unity | EventDetector.cs — RadarSensor 연동, 충돌·과속·항로이탈·좌초 감지 |
| **P5** | Unity | TelemetryCollector.cs — 세션 시작/종료 + 5초 Bulk 전송 |
| **P6** | 백엔드 | EvaluationService — 세션 종료 시 evaluation_results 자동 생성 |
| **P7** | Unity | 결과 화면 UI — totalScore·이벤트 카운트·passed 표시 |
| **P8** | Unity | Rule-based NPC — 자동 항해로 데이터 대량 수집 |
| **P9** | ML | LSTM 학습 파이프라인 → ONNX → Unity Sentis 탑재 |

---

## 공공 API → Unity 매핑

| API 데이터 | 필드 | Unity 적용 방법 |
|---|---|---|
| 파고 (m) | waveHeight | `waveAmplitude = waveHeight * 0.5f` → Crest OceanRenderer |
| 풍속 (m/s) | windSpeed | `Rigidbody.AddForce()` 항력 공식: F = 0.5 * ρ_air * Cd * A * v² |
| 풍향 (°) | windDirection | 벡터 변환: `new Vector3(Sin(deg), 0, Cos(deg))` |
| 조위 (cm) | tideLevel | `BuoyancySystem.fallbackWaterLevel = baseline + tideOffset` |
| 시정 (km) | visibility | `RenderSettings.fogDensity = 1f / (visibility * 100f)` |

---

## 텔레메트리 데이터 구조 (Unity → 백엔드)

```json
POST /api/v1/sessions/{sessionId}/telemetry
{
  "vesselLogs": [{
    "recordedAt": "ISO8601",
    "latitude": 35.1, "longitude": 129.0,
    "speedKn": 5.2, "headingDeg": 90.0,
    "rudderAngle": -5.0, "throttle": 0.6,
    "roll": 1.2, "pitch": 0.3, "yaw": 90.0
  }],
  "environmentLogs": [{
    "recordedAt": "ISO8601",
    "windSpeed": 5.0, "windDirection": 270.0,
    "waveHeight": 1.5, "currentSpeed": 0.0,
    "currentDirection": 0.0, "tideLevel": 120.0,
    "visibility": 8.0
  }],
  "events": [{
    "eventType": "COLLISION_WARNING",
    "eventTime": "ISO8601",
    "severity": "HIGH",
    "description": "전방 50m 암초",
    "latitude": 35.1, "longitude": 129.0
  }]
}
```

---

## 데이터 역할 분리

| 테이블 | ML 용도 | 게임 용도 |
|---|---|---|
| `vessel_logs` | LSTM 입력 피처 (X) | - |
| `environment_logs` | LSTM 입력 피처 (X) | - |
| `event_logs` | LSTM 레이블 (Y) 도출 | - |
| `evaluation_results` | **ML과 무관** | 결과 화면 요약 점수 표시용 |

`evaluation_results.total_score`는 규칙 기반 점수(충돌×20 + 과속×5 ...)로,
주관적 가중치라 ML 학습 신호로 부적합. 게임 피드백 전용으로만 사용.

---

## ML 파이프라인 (P9)

### 레이블링 방식
```python
# vessel_log 타임스탬프 기준으로 슬라이딩 윈도우
# → 이후 30초 안에 event_logs 이벤트 존재 → label = 1 (위험)
# → 이벤트 없음                           → label = 0 (안전)
```

### 학습 파이프라인
```
DB → Python 추출 → 전처리 (정규화 / 슬라이딩 윈도우 30프레임 / 레이블 불균형 처리)
  → LSTM (입력: 30×14피처, 출력: 위험도 0.0~1.0)
  → ONNX Export (opset 17) → Unity Sentis 온디바이스 추론
```

14피처: speed_kn, heading_deg, rudder_angle, throttle, roll, pitch, yaw,
        wind_speed, wind_direction, wave_height, current_speed, tide_level, visibility + 이벤트 유무

---

## 주요 제약사항

- 완전한 디지털 트윈 불가 → 공공 API 기상값 기반 근사 환경
- 백엔드 ML 추론 없음 → Unity Sentis 온디바이스 추론만 사용
- 1차 목표: 위험도 예측 경고 / 자율운항 강화학습은 2차 목표

---

## 라이센스

MIT

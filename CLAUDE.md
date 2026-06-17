# SafeSail — MarineDigitalTwin-Engine CLAUDE.md

공공 API(기상청·국립해양조사원)로 실제 해상 기상을 근사 재현하는 Unity 해양 운항 시뮬레이터.
운항 데이터를 SafeSail 백엔드에 수집하여 **LSTM 위험도 예측 모델 + PPO 자율운항 에이전트**를 구축하는 것이 최종 목표다.

- **백엔드 경로**: `~/Desktop/project/safesail/` (Spring Boot 3 / Java 21 / PostgreSQL+PostGIS / Redis / Docker)
- **Unity**: 6 LTS · HDRP 17.0.4 · New Input System · UnityWebRequest · Unity Sentis(ONNX) · Unity ML-Agents
- **프로젝트명**: 004_MarineDigitalTwin-Engine
- **주요 씬**: `Assets/Scenes/BoatSimulatorMainScene.unity`
- **네임스페이스**: `MarineDigitalTwin.Boat`, `MarineDigitalTwin.Environment`

---

## 최종 아키텍처 (두 트랙 병렬)

```
┌──────────────────────────────────────────────────────────────────┐
│  TRACK A: LSTM 위험도 모델 (HUD·평가용)                           │
│                                                                  │
│  수집 데이터(DB) → Python 전처리 → LSTM 학습 → ONNX             │
│  → Unity Sentis 온디바이스 추론 → 실시간 위험도 HUD             │
│                                                                  │
│  입력 피처 (30프레임 슬라이딩 윈도우):                            │
│  선박: speed_kn, heading_deg, rudder_angle, throttle,            │
│        roll, pitch, yaw                                          │
│  환경: wave_height, wind_speed, wind_direction, tide_level       │
│  센서: radar_0 ~ radar_8 (레이다 9개 거리값)                     │
│  수심: under_keel_clearance (선저 여유수심)                       │
│  합계: 7 + 4 + 9 + 1 = 21피처                                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  TRACK B: PPO 자율운항 에이전트 (Unity ML-Agents)                 │
│                                                                  │
│  Observation: 레이다 9개 거리값 + 선박 상태 + 목표 방향          │
│  Action: rudder_angle (continuous) + throttle (continuous)       │
│  Reward: +전진 거리, -충돌, -좌초, -과속, +목표 도달             │
│  → ONNX Export → Unity Sentis 탑재 → 자율운항 데모              │
│                                                                  │
│  ※ LSTM 위험도와 RL은 독립 운영 (LSTM을 reward로 쓰지 않음)     │
└──────────────────────────────────────────────────────────────────┘
```

---

## 전체 데이터 파이프라인

```
┌─────────────────────────────────────────────────────────────┐
│  공공 API (1시간 배치)                        [백엔드 완료]  │
│  기상청 해양기상 부이 → 파고·풍속·풍향                        │
│  국립해양조사원 조위관측 → tideLevel                          │
└──────────────────────────┬──────────────────────────────────┘
                           ↓  WeatherScheduler → Redis 캐시
┌─────────────────────────────────────────────────────────────┐
│  Unity — MarineEnvironmentClients.cs         [구현 완료]    │
│  60초 폴링 + Manual Override (Inspector 슬라이더)            │
│  → OceanEnvironmentController → HDRP WaterSurface          │
│  + BathymetryClient → DepthMapManager (수심)                │
└──────────────────────────┬──────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│  Unity — 선박 물리                           [구현 완료]    │
│  BoatMMGController / BuoyancySystem / AiryWaveTheory        │
│  BoatInputHandler / BoatCamera / BoatDebugUI                │
└──────────────────────────┬──────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│  Unity — 레이다 센서 9개 (RadarSensorArray)  [P3 미구현]    │
│  선수 기준 40° 간격으로 배치 (RayPerceptionSensor)           │
│  장애물(암초·부두·타선) 거리 측정 → 피처 + RL Observation   │
└──────────────────────────┬──────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│  Unity — EventDetector.cs                    [P4 미구현]    │
│  SPEEDING / COLLISION_WARNING / GROUNDING_WARNING           │
│  → ML 학습 레이블 생성                                       │
└──────────────────────────┬──────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│  Unity — TelemetryCollector.cs               [P5 미구현]    │
│  세션 시작 → POST /api/v1/sessions                          │
│  5초 배치  → POST /api/v1/sessions/{id}/telemetry           │
│  세션 종료 → PATCH /api/v1/sessions/{id}/end                │
└──────────────────────────┬──────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│  백엔드 PostgreSQL                           [구현 완료]    │
│  vessel_logs / environment_logs / event_logs                │
│  EvaluationService (세션 종료 시 자동 평가)  [P6 미구현]    │
└──────────────────────────┬──────────────────────────────────┘
           ┌───────────────┴────────────────┐
           ↓                                ↓
┌──────────────────────┐      ┌──────────────────────────────┐
│  TRACK A: LSTM 학습  │      │  TRACK B: PPO RL 학습        │
│  [P7 미구현]         │      │  [P8 미구현]                  │
│  Python → ONNX       │      │  Unity ML-Agents → ONNX      │
│  → Sentis → HUD      │      │  → Sentis → 자율운항 데모    │
└──────────────────────┘      └──────────────────────────────┘
```

---

## 개발 일정 (공모전 최종 발표까지 약 6주)

| 주차 | 작업 | 산출물 |
|------|------|--------|
| **1주** | 레이다 센서 9개 (RayPerceptionSensor) + EventDetector | `RadarSensorArray.cs`, `EventDetector.cs` |
| **2주** | TelemetryCollector + 데이터 수집 (밤새 시뮬) | `TelemetryCollector.cs`, DB 로그 |
| **3주** | EvaluationService (백엔드) + LSTM 학습 + ONNX | Python 학습 스크립트, `.onnx` 파일 |
| **4주** | Unity Sentis 위험도 HUD + ML-Agents RL 세팅 | `RiskHUD.cs`, ML-Agents 환경 구성 |
| **5주** | PPO 학습 + 튜닝 + 결과 화면 UI | 자율운항 `.onnx`, 결과 UI |
| **6주** | 통합·버그픽스·데모 polish | 최종 데모 |

---

## 구현 현황

### 완료

| 위치 | 파일/모듈 | 내용 |
|------|-----------|------|
| Unity | `BoatMMGController.cs` | MMG 물리 모델 (추진·조타·항력·Prop Walk) |
| Unity | `BoatInputHandler.cs` | W/S/A/D/Q/E/R/Space 조작, 기어 시스템 |
| Unity | `BuoyancySystem.cs` | HDRP WaterSurface 10pt 부력·댐핑, 물리 안전장치 |
| Unity | `MarineEnvironmentClients.cs` | 환경 API 60초 폴링 + Manual Override |
| Unity | `OceanEnvironmentController.cs` | HDRP WaterSurface 파라미터 제어, 조위·좌초판단 |
| Unity | `BathymetryClient.cs` | 수심 API 단발 요청 및 캐시 |
| Unity | `DepthMapManager.cs` | 선박 위치별 차트수심·유효수심 계산 |
| Unity | `AiryWaveTheory.cs` | Airy 파동 이론 수면높이·수직속도 계산 |
| Unity | `BoatDebugUI.cs` / `BoatCamera.cs` | 디버그 HUD (레이다 표시 포함), 추적 카메라 |
| Unity | `RadarSensorArray.cs` | 레이다 센서 9개 (40° 간격, Physics.Raycast, 정규화 거리 제공) |
| 백엔드 | 전체 API | 세션·텔레메트리·환경·리포트 엔드포인트 |
| 백엔드 | Docker Compose | PostgreSQL+PostGIS, Redis, Spring Boot |
| 백엔드 | Flyway 마이그레이션 | DB 스키마 (V1__init.sql) |
| 백엔드 | WeatherScheduler | 공공 API 실연동 (기상청 부이 + 국립해양조사원 조위) → Redis 캐시 |

### 미구현 (우선순위 순)

| 우선순위 | 담당 | 작업 |
|----------|------|------|
| ~~P3~~ | Unity | ~~`RadarSensorArray.cs`~~ — **구현 완료** |
| **P4** | Unity | `EventDetector.cs` — 과속·충돌·좌초 감지, ML 레이블 생성 |
| **P5** | Unity | `TelemetryCollector.cs` — 세션 시작/종료 + 5초 Bulk 전송 |
| **P6** | 백엔드 | `EvaluationService` — 세션 종료 시 evaluation_results 자동 생성 |
| **P7** | ML | LSTM 위험도 모델 학습 → ONNX → Unity Sentis HUD (Track A) |
| **P8** | Unity+ML | ML-Agents PPO 자율운항 학습 → ONNX → 자율운항 데모 (Track B) |
| **P9** | Unity | 세션 결과 화면 UI (totalScore·이벤트 카운트·passed) |

---

## 레이다 센서 설계 (P3)

```
선수(0°) 기준 배치 — 총 9개
  센서 0:   0° (정면)
  센서 1:  40° (우전방)
  센서 2:  80° (우측)
  센서 3: 120° (우후방)
  센서 4: 160° (우후)
  센서 5: 200° (좌후)
  센서 6: 240° (좌후방)
  센서 7: 280° (좌측)
  센서 8: 320° (좌전방)

감지 대상: 암초, 부두, 타 선박 (레이어 마스크로 구분)
최대 감지 거리: 100m
반환값: 정규화 거리 0.0(접촉)~1.0(감지 안됨)
```

---

## ML 피처 (21개)

| 그룹 | 피처 | 수 |
|------|------|----|
| 선박 상태 | speed_kn, heading_deg, rudder_angle, throttle, roll, pitch, yaw | 7 |
| 해양 환경 | wave_height, wind_speed, wind_direction, tide_level | 4 |
| 레이다 센서 | radar_0 ~ radar_8 (정규화 거리) | 9 |
| 수심 | under_keel_clearance (선저 여유수심 m) | 1 |
| **합계** | | **21** |

**LSTM 입력**: 30프레임 × 21피처 → 위험도 0.0~1.0 출력  
**PPO Observation**: 레이다 9 + 선박 상태 4(speed·heading·roll·pitch) + 목표방향 2 = 15

---

## 텔레메트리 데이터 구조 (P5 구현 기준)

```json
POST /api/v1/sessions/{sessionId}/telemetry
{
  "vesselLogs": [{
    "recordedAt": "ISO8601",
    "latitude": 35.1, "longitude": 129.0,
    "speedKn": 5.2, "headingDeg": 90.0,
    "rudderAngle": -5.0, "throttle": 0.6,
    "roll": 1.2, "pitch": 0.3, "yaw": 90.0,
    "radar": [1.0, 0.8, 1.0, 1.0, 1.0, 1.0, 1.0, 0.6, 1.0],
    "underKeelClearance": 3.5
  }],
  "environmentLogs": [{
    "recordedAt": "ISO8601",
    "windSpeed": 5.0, "windDirection": 270.0,
    "waveHeight": 1.5, "tideLevel": 1.2
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

## 백엔드 API 엔드포인트

| 엔드포인트 | 방식 | 설명 | 연동 상태 |
|-----------|------|------|----------|
| `/api/v1/environment/marine?latitude=&longitude=` | GET | 파랑·바람·조위 | 완료 |
| `/api/v1/environment/bathymetry` | GET | 수심 포인트 배열 | 완료 |
| `/api/v1/sessions` | POST | 세션 시작 | Unity 미구현(P5) |
| `/api/v1/sessions/{id}/telemetry` | POST | 5초 배치 로그 전송 | Unity 미구현(P5) |
| `/api/v1/sessions/{id}/end` | PATCH | 세션 종료 | Unity 미구현(P5) |
| `/api/v1/sessions/{id}/report` | GET | 평가 결과 조회 | Unity 미구현(P9) |

---

## 디렉터리 구조

```
Assets/
├── MarineDigitalTwin/
│   ├── Features/
│   │   └── Boat/
│   │       ├── Scripts/          # 선박 제어 스크립트
│   │       │   └── (추가예정) RadarSensorArray.cs, EventDetector.cs
│   │       │                     TelemetryCollector.cs, RiskHUD.cs
│   │       ├── Environment/
│   │       │   └── Scripts/      # 해양환경 스크립트
│   │       └── Physics/          # 물리 관련 에셋
│   └── Resources/
│       ├── Boat/SeaBoat24Ft/     # 24ft 선박 FBX·텍스처
│       ├── Dock/                 # 부두 에셋
│       ├── 5_UI/                 # UI 프리팹
│       ├── 7_Arrow/              # 방향 화살표
│       └── 8_MiniMap/            # 미니맵 렌더 텍스처
├── Editor/                       # 에디터 유틸 스크립트
├── ML-Agents/                    # (추가예정) PPO 학습 설정
└── Settings/                     # HDRP 렌더링 설정
```

---

## 핵심 스크립트 (기존 구현)

### Boat/Scripts/

| 파일 | 역할 |
|------|------|
| `BoatMMGController.cs` | MMG 표준 모델 기반 선박 운동 물리. Inoue/Yoshimura 경험식 계수. SeaBoat24Ft(Lpp=7.3m, m=1500kg). |
| `BuoyancySystem.cs` | HDRP WaterSurface 수면 샘플링 기반 부력. 10개 HullPoint. 물리 폭주 방지 안전장치 포함. |
| `BoatInputHandler.cs` | 키보드(New Input System) → BoatMMGController. 스로틀/기어/타각/트림 조작. |
| `BoatCamera.cs` | 선박 추종 카메라. offset 기반 LerpFollow. |
| `BoatDebugUI.cs` | TMP 기반 실시간 디버그 HUD (RPS, 타각, 속도, 환경값, 레이다 거리). |
| `RadarSensorArray.cs` | 선수 기준 40° 간격 레이다 센서 9개. Physics.Raycast 기반 장애물 거리 측정. 정규화 거리(0.0~1.0) 및 실거리(m) 제공. ML-Agents 추가 시 RayPerceptionSensor 병렬 적용 예정. |

### Boat/Environment/Scripts/

| 파일 | 역할 |
|------|------|
| `MarineEnvironmentClients.cs` | `/api/v1/environment/marine` 60초 폴링. Manual Override 지원. |
| `OceanEnvironmentController.cs` | HDRP WaterSurface 파라미터 제어. 조위 수면 Y 오프셋. 좌초 위험 판단. |
| `OceanEnvironmentState.cs` | 해양환경 상태 데이터 모델. |
| `BathymetryClient.cs` | `/api/v1/environment/bathymetry` 단발 요청 및 캐시. |
| `DepthMapManager.cs` | 선박 현재 위치 차트수심·유효수심 계산. |
| `AiryWaveTheory.cs` | Airy 파동 이론 수면높이·수직속도 계산. |
| `BathymetryData.cs` | 수심 API 응답 모델 (originLat/Lon, depthPoints[], tideLevelMeter). |

---

## 물리 모델 요점

- **MMG 모델**: 선체력(Hull) + 프로펠러력(Propeller) + 타력(Rudder) 분리 계산
- **선박 제원**: SeaBoat24Ft — Lpp=7.3m, B=2.5m, d=0.5m, m=1500kg, Cb=0.45
- **부력 포인트**: Bow(3) + Mid(3) + Stern(3) + Keel(1) = 10개 HullPoint
- **파동**: HDRP WaterSurface 파랑 + AiryWaveTheory 수직속도 보정
- **수심 연동**: 수심 따라 천해 파랑 변환(파속 감소) 및 좌초 위험 경보

---

## 주요 패키지 (manifest.json)

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `render-pipelines.high-definition` | 17.0.4 | HDRP 렌더링 + WaterSurface |
| `cinemachine` | 3.1.7 | 카메라 |
| `inputsystem` | 1.19.0 | New Input System |
| `ugui` | 2.0.0 | UI |
| `ai.navigation` | 2.0.12 | NavMesh |
| `com.unity.ml-agents` | (추가예정) | PPO 자율운항 RL 학습 |
| `mcp-unity` | (git) | MCP 연동 |

---

## 개발 컨벤션

- C# 네임스페이스: `MarineDigitalTwin.Boat` / `MarineDigitalTwin.Environment`
- HDRP 전용 API 사용 (`WaterSurface`, `WaterSearchParameters`, `WaterSearchResult`)
- 백엔드 통신: `UnityWebRequest` 코루틴 패턴
- 단위: 속도=m/s, 각도=degree, 수심=meter, 풍속=m/s (HDRP 입력 전 ×3.6 → km/h 변환)
- 물리 안전장치: 최대 부력·최대 속도 클램프, 시작 직후 안전 구간(`startupSafetySeconds`)

---

## 주요 제약사항

- 완전한 디지털 트윈 불가 → 공공 API 기상값 기반 **근사 환경**
- 백엔드 ML 추론 없음 → Unity Sentis **온디바이스 추론**만 사용
- LSTM과 PPO RL은 독립 운영 — LSTM을 RL reward로 쓰지 않음
- 수면 렌더링: Crest 미사용, **HDRP 내장 WaterSurface**만 사용

---

## CLAUDE.md 갱신 시점

프로젝트 구조가 바뀌면(새 스크립트 추가, 폴더 이동, 패키지 추가, API 변경, 구현 완료 등) 이 파일을 먼저 갱신하고 작업을 시작하세요.

갱신 체크리스트:
- 새 스크립트 추가 시 → 디렉터리 구조·핵심 스크립트 표 갱신
- `Packages/manifest.json` 변경 시 → 주요 패키지 표 갱신
- 백엔드 API 경로·응답 구조 변경 시 → 백엔드 API 표·텔레메트리 구조 갱신
- P3~P9 항목 구현 완료 시 → 구현 현황 표 이동
- 레이다 센서 설계 변경 시 → 레이다 센서 설계·ML 피처 표 갱신
- ML 피처 추가·제거 시 → ML 피처 표 + 텔레메트리 구조 동시 갱신

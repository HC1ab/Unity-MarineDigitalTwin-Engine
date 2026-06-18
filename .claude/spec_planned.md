# Planned Spec (계획중 / 미구현)
> 마지막 업데이트: 2026-06-13

---

---

## 환경 / 맵

### TODO: 맵 기본 구성 (EventDetector 구현 전 선행 필수)
- **Seabed (해저 지형)**
  - Ocean 아래 평면 mesh 배치
  - Layer: `Seabed` (새 LayerMask 생성)
  - 좌초 위험 레이캐스트 타겟
- **Waypoint 경로**
  - 빈 GameObject 배열 (`Waypoints/WP_00, WP_01 ...`)
  - 항로 이탈 거리 체크 기준
- **장애물**
  - 부표, 암초 등 Collider 오브젝트
  - Dock은 이미 존재 ✅

### TODO: 선착장 충돌 처리
- **현재**: Dock에 Collider 있지만 충돌 물리 미세 튜닝 안 됨
- **구현 내용**:
  - Dock Collider PhysicsMaterial 설정 (반발계수, 마찰)
  - 충돌 시 Rigidbody 충격 처리 (속도 급감)
  - 충돌 이벤트 → EventDetector 연동

---

## 이벤트 감지

### TODO: EventDetector.cs (P3)
- **위치**: `Features/Sensor/EventDetector.cs`
- **선행 조건**: 맵 기본 구성 완료 (Seabed LayerMask, Waypoints)
- **감지 항목**:

| 이벤트 | 방법 | Inspector 노출 |
|--------|------|---------------|
| 과속 | `speedKn > threshold` | `speedThreshold` (kn) |
| 충돌 경고 | `OnCollisionEnter` + 전방 레이캐스트 | `warningDistance` (m) |
| 항로 이탈 | Waypoint 리스트 기반 거리 체크 | `routeDeviationDist` (m) |
| 좌초 위험 | 선저 하방 레이캐스트 → Seabed LayerMask | `groundingDepth` (m) |

- **출력**: 내부 이벤트 큐 (TelemetryCollector가 소비)
- **이벤트 구조**:
  ```csharp
  public enum EventType { Overspeed, CollisionWarning, RouteDeviation, GroundingRisk }
  public struct DetectedEvent { public EventType type; public float value; public float timestamp; }
  ```

---

## Physics 고도화

### TODO: MMG 계수 보정 (System Identification)
- **현재**: Yoshimura 2006 경험식 추정값 — 실제 선박과 오차 존재
- **목표**: 실선 시험 데이터 기반으로 Yv/Yr/Nv/Nr/K_T 등 보정
- **방법**: 시뮬레이터 로그 → PyTorch 최적화 → 계수 역산
- **우선순위**: 자율운항 실적용 전 필수

### TODO: Added Mass 운동방정식 반영
- **현재**: mx/my/Jz 정의됨, Unity PhysX 관성과 별도로 미반영
- **한계**: PhysX Rigidbody가 직접 적분 → `(m+mx)*du/dt` 항 추가 불가
- **방법 후보**: Rigidbody.AddForce로 added mass 반력 근사 적용
  - `F_addedMass_surge = -mx * (u_current - u_prev) / Time.fixedDeltaTime`
- **영향**: 고속 기동 응답 특성 정확도

### TODO: 파랑 가진력 (Wave Excitation Force)
- **현재**: hull point 수직 부력만 존재
- **미구현**: 파도가 선체에 가하는 수평 가진력 (파도에 밀림)
- **방법**: HDRP Water API 파도 gradient → 수평 force 근사

### TODO: 바람 영향 (WindForce.cs)
- **위치**: `Features/Boat/Physics/WindForce.cs`
- **구현 내용**: 풍속/풍향 → 선체 수선 위 면적 기반 항력 계산
- **연동**: 날씨 시스템 WindSpeed 파라미터

---

## 카메라
### TODO: Cinemachine 3인칭 카메라
- Follow + LookAt: Boat (BoatCameraTarget 빈 오브젝트 기준, localPos (0,1.5,0))
- Orbital Follow 또는 ThirdPersonFollow
- 오프셋: (0, 3, -8)
- 마우스 orbit 지원

---

## UI / HUD
### TODO: 정식 HUD (현재 DebugUI 교체)
- 속도계 (m/s, knots)
- 나침반
- 미니맵
- 스로틀/타각/트림 게이지
- 기존 `Resources/5_UI ~ 8_MiniMap` 에셋 활용

---

## 센서 시뮬레이션
### TODO: GPS
- 월드 좌표 → 위경도 변환
- 출력: lat/lon/altitude, 노이즈 모델 포함

### TODO: IMU
- 가속도 (Rigidbody.linearAcceleration)
- 자이로 (angularVelocity)
- 자기 (heading 기반)
- 노이즈 + 바이어스 모델 포함

### TODO: LiDAR
- Raycast 기반 360° 수평 스캔
- 출력: 거리 배열, 포인트 클라우드
- 위치: `Features/Sensor/LidarSensor.cs`

### TODO: 레이더 센서 (Radar)
- **방식**: 원형 범위 내 Collider 감지 (Physics.OverlapSphere 또는 SphereCast)
- **출력**: 감지 대상 목록 (거리, 방위각, 상대속도)
- **갱신 주기**: 설정 가능 (기본 1~2Hz, 실제 레이더 모사)
- **Inspector 노출**: `radarRange` (m), `scanInterval` (s)
- **시각화**: Scene Gizmo로 스캔 범위 + 감지 대상 표시
- **위치**: `Features/Sensor/RadarSensor.cs`
- **EventDetector 연동**: 충돌 경고 전방 감지 보조

### TODO: 데이터 로깅
- CSV / JSON 포맷
- 로그 항목: timestamp, pos, vel, heading, gear, RPS, rudder, trim, GPS, IMU
- 위치: `Features/DataLogging/`

---

## 하드웨어 연동
### TODO: HardwareManager.cs
- Serial 포트 통신
- 실제 조타 장치 → rudderAngleDeg, propellerRPS 입력 연결
- 구버전 참고: `sendTickTime = 0.05f`, SendData(throttle, rudder, speed)

---

## ML 파이프라인
### TODO: 학습 데이터 수집
- 시뮬레이터 센서 로그 → CSV 출력
- 상태: [u, v, r, rudder, RPS, gear] / 행동: [rudder_cmd, throttle_cmd]

### TODO: PyTorch 학습
- 외부 Python 환경에서 학습
- 출력: `.onnx` 모델 파일

### TODO: Unity Sentis 연동
- `com.unity.sentis` 패키지 설치
- `SentisInference.cs` — 추론 루프 연결
- 위치: `Features/ML/`

---

## 날씨 시스템
### TODO: HDRP 동적 날씨
- HDRP Sky/Fog 런타임 파라미터 변경
- `ocean.largeWindSpeed` 등 Water API 연동
- 미결정: Enviro3 HDRP 구매 여부 ($80)

---

## 폴더 구조 (미생성 폴더)
```
Assets/MarineDigitalTwin/
├── Features/
│   ├── Boat/
│   │   ├── Scripts/      ← ✅ 완료
│   │   └── Physics/      ← WindForce.cs 추가 예정
│   ├── Ocean/            ← 미생성
│   ├── Weather/          ← 미생성
│   ├── UI/               ← 미생성 (현재 DebugCanvas만)
│   ├── Sensor/           ← 미생성 (GPS, IMU, LiDAR, Radar, EventDetector)
│   ├── Hardware/         ← 미생성
│   ├── DataLogging/      ← 미생성
│   └── ML/               ← 미생성
└── Core/
    ├── Constants/        ← 미생성
    ├── Network/          ← 미생성
    └── Storage/          ← 미생성
```

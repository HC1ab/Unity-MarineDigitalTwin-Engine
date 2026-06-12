# Planned Spec (계획중 / 미구현)
> 마지막 업데이트: 2026-06-12

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
- Raycast 기반 360° 스캔
- 출력: 거리 배열, 포인트 클라우드

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
│   ├── Sensor/           ← 미생성
│   ├── Hardware/         ← 미생성
│   ├── DataLogging/      ← 미생성
│   └── ML/               ← 미생성
└── Core/
    ├── Constants/        ← 미생성
    ├── Network/          ← 미생성
    └── Storage/          ← 미생성
```

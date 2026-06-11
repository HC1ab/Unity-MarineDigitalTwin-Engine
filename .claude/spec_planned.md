# Planned Spec (계획중 / 미구현)

## Physics 고도화
- MMG 계수 보정
  - 현재: 경험식 추정값
  - 목표: 실측 데이터 기반 System Identification으로 보정
- 파도-선체 상호작용 (wave force on hull)
- 바람 영향 (WindForce.cs)

## 카메라
- Cinemachine 3인칭 카메라
  - Follow + LookAt: Boat
  - 마우스 orbit 지원

## 입력 시스템
- Unity Input System 기반 BoatInputHandler.cs
  - W/S → propellerRPS
  - A/D → rudderAngleDeg
  - 키보드 + 하드웨어 컨트롤러 지원

## UI / HUD
- 속도계 (m/s, knots)
- 나침반
- 미니맵
- 스로틀/타각 게이지
- 기존 Resources/5_UI ~ 8_MiniMap 에셋 활용

## 센서 시뮬레이션
- GPS (위경도 변환)
- IMU (가속도, 자이로, 자기)
- LiDAR (Raycast 기반)
- 데이터 로깅 CSV/JSON

## 하드웨어 연동
- Serial 포트 통신 (HardwareManager.cs)
- 실제 조타 장치 → 입력 연결
- 구버전 참고: `sendTickTime = 0.05f`, SendData(throttle, rudder, speed)

## ML 파이프라인
- 외부 PyTorch 학습 → .onnx 출력
- Unity Sentis 패키지 설치
- SentisInference.cs — 추론 연결
- 학습 데이터: 시뮬레이터 센서 로그

## 날씨 시스템
- HDRP Sky/Fog 동적 변경
- 바람 방향/세기 → WindForce 연동
- 미결정: Enviro3 HDRP 구매 여부 ($80)

## 멀티플레이 / 네트워크 (선택)
- 미결정

## 폴더 구조 (미생성)
```
Assets/MarineDigitalTwin/
├── Features/
│   ├── Boat/
│   │   ├── Scripts/      ← BoatInputHandler.cs 추가 예정
│   │   └── Physics/      ← WindForce.cs 추가 예정
│   ├── Ocean/
│   ├── Weather/
│   ├── UI/
│   ├── Sensor/
│   ├── Hardware/
│   └── DataLogging/
└── Core/
    ├── Constants/
    ├── Network/
    └── Storage/
```

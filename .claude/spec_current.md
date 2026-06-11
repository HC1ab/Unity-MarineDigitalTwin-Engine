# Current Spec (확정 사용중)

## Engine & Rendering
- Unity 6000.0.76f1
- HDRP 17.0.4
- Input System 1.19.0
- TextMeshPro 3.0.9

## Water System
- HDRP 내장 Water System (Ocean, Sea and Coastline)
- `WaterSurface.ProjectPointOnWaterSurface()` API로 수면 높이 쿼리

## Boat Physics
- Unity Rigidbody (PhysX)
  - Mass: 1500 kg
  - Linear Drag: 0
  - Angular Drag: 0
  - Collision Detection: Continuous
- **MMG Standard Model** (BoatMMGController.cs)
  - Hull + Propeller + Rudder 힘 분리
  - 경험식 추정 계수 (Lpp=7.3m, B=2.5m, d=0.5m, Cb=0.45)
  - Surge resistance: X_RR = -0.5 * rho * Lpp * d * 0.12 * u|u|
  - Inspector: rudderAngleDeg (-35~35), propellerRPS (0~25, 기본값 18)
  - **boat_24.FBX 로컬 축 매핑 (수정 금지)**
    - bow(선수) → local -X  (-transform.right)
    - starboard(우현) → local +Z  (+transform.forward)
    - up → local +Y
    - 모델 교체 시 UpdateBodyVelocity / ApplyForces 재확인 필요
- **Hull Point Buoyancy** (BuoyancySystem.cs)
  - 10개 hull 포인트
  - buoyancyFactor: 60000
  - dampingFactor: 10000
  - Script Interactions: HDRP Asset(CPU Simulation) + WaterSurface 컴포넌트 양쪽 활성화 필수

## Assets
- 배: `Resources/Boat/SeaBoat24Ft/FBX/boat_24.FBX`
- 부두: `Resources/Dock/`
- UI: `Resources/5_UI/`, `6_Images/`, `7_Arrow/`, `8_MiniMap/`

## Scene (SampleScene.unity)
- Sun (Directional Light)
- Sky_Volume (Global Volume)
- Ocean (WaterSurface)
- Boat (boat_24.FBX + Rigidbody + MMGController + BuoyancySystem)
- Dock

## Scripts
```
Assets/MarineDigitalTwin/Features/Boat/Scripts/
├── BoatMMGController.cs   ← MMG 물리 + surge resistance
├── BuoyancySystem.cs      ← hull point 부력 (HDRP Water API)
└── BoatInputHandler.cs    ← 키보드 입력 (W/S: 추진, A/D: 타각) [구현예정]
```

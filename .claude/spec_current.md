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
  - Inspector: rudderAngleDeg, propellerRPS
- **Hull Point Buoyancy** (BuoyancySystem.cs)
  - 10개 hull 포인트
  - buoyancyFactor: 60000
  - dampingFactor: 10000

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
├── BoatMMGController.cs   ← MMG 물리
└── BuoyancySystem.cs      ← 부력
```

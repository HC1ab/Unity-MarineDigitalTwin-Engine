# Current Spec (확정 사용중)

## Engine & Rendering
- Unity 6000.0.76f1
- HDRP 17.0.4
- Input System 1.19.0
- TextMeshPro 3.0.9

## Water System
- HDRP 내장 Water System (Ocean, Sea and Coastline)
- `WaterSurface.ProjectPointOnWaterSurface()` API로 수면 높이 쿼리
- Script Interactions: HDRP Asset(CPU Simulation) + WaterSurface 컴포넌트 양쪽 활성화 필수

## Boat Physics
- Unity Rigidbody (PhysX)
  - Mass: 1500 kg
  - Linear Drag: 0 / Angular Drag: 0
  - Collision Detection: Continuous

- **MMG Standard Model** (BoatMMGController.cs)
  - Hull + Propeller + Rudder 힘 분리
  - 경험식 추정 계수 (Lpp=7.3m, B=2.5m, d=0.5m, Cb=0.45, m=1500kg)
  - Surge resistance: X_RR = -0.5 * rho * Lpp * d * 0.06 * humpMult * u|u|
  - Planing 저항 곡선: hump(10kn 구간) 저항 1.8배, 이후 감소
  - Gear 3단: REVERSE / NEUTRAL / FORWARD (GearState enum)
    - NEUTRAL: 추진력 0
    - REVERSE: gearSign=-1, prop walk 좌현(-0.08), 러더 효과 30%
    - FORWARD: gearSign=+1, prop walk 우현(+0.04), 러더 효과 100%
  - Prop Walk: 전진 우현 편류, 후진 좌현 편류 2배 강도
  - Trim: trimAngleDeg (-20~+20) — 현재 값 저장만, 선체 자세 반영 미구현
  - propellerRPS: 0~35 (기본값 0)
  - **boat_24.FBX 로컬 축 매핑 (수정 금지)**
    - bow(선수) → local -X  (-transform.right)
    - starboard(우현) → local +Z  (+transform.forward)
    - up → local +Y
    - 모델 교체 시 UpdateBodyVelocity / ApplyForces 재확인 필요

- **Hull Point Buoyancy** (BuoyancySystem.cs)
  - 10개 hull 포인트
  - buoyancyFactor: 60000 / dampingFactor: 10000

## Input (BoatInputHandler.cs)
| 키 | 기능 |
|----|------|
| W | FORWARD 기어 체결 + RPS 증가 |
| S | REVERSE 기어 체결 + RPS 증가 |
| A / D | 타각 좌/우 (복원 없음 — 유지) |
| Q / E | 트림 아웃 / 트림 인 |
| F | 뉴트럴 (RPS ≤ 5일 때만 변속) |
| Space | 킬스위치 (즉시 NEUTRAL + RPS=0 + 타각=0) |

## Debug UI (BoatDebugUI.cs)
- Gear + RPS 표시
- 타각 + 트림 각도 표시
- 속도 (knots) 표시

## Assets
- 배: `Resources/Boat/SeaBoat24Ft/FBX/boat_24.FBX`
- 부두: `Resources/Dock/`
- UI: `Resources/5_UI/`, `6_Images/`, `7_Arrow/`, `8_MiniMap/`

## Scene (SampleScene.unity)
- Sun (Directional Light)
- Sky_Volume (Global Volume)
- Ocean (WaterSurface)
- Boat (boat_24.FBX + Rigidbody + MMGController + BuoyancySystem + InputHandler + DebugUI)
- Dock
- DebugCanvas (Screen Space Overlay)

## Scripts
```
Assets/MarineDigitalTwin/Features/Boat/Scripts/
├── BoatMMGController.cs   ← MMG 물리 (기어/추진/저항/PropWalk/Planing)
├── BuoyancySystem.cs      ← hull point 부력 (HDRP Water API)
├── BoatInputHandler.cs    ← 키보드 입력 (기어/타각/트림/킬스위치)
└── BoatDebugUI.cs         ← 속도/기어/타각/트림 HUD
```

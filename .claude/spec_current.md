# Current Spec (확정 사용중)
> 마지막 업데이트: 2026-06-12

---

## Engine & Rendering
- Unity 6000.0.76f1
- HDRP (High Definition Render Pipeline) 17.0.4
- Input System 1.19.0
- TextMeshPro 3.0.9
- MCP Unity (com.gamelovers.mcp-unity) — localhost:8090

---

## Water System
- **방식**: HDRP 내장 Water System (Ocean, Sea and Coastline 프리셋)
- **수면 높이 쿼리 API**: `WaterSurface.ProjectPointOnWaterSurface(WaterSearchParameters, out WaterSearchResult)`
  - `candidateLocationWS`: 프레임 간 유지 필수 (리셋 시 수렴 실패)
  - `startPositionWS`: 이전 프레임 `candidateLocationWS` 사용
  - `maxIterations`: 8, `error`: 0.01f
- **필수 설정 (누락 시 waterH=0)**
  - HDRP Asset → Rendering → Water → Water Script Interactions → **CPU Simulation** 활성화
  - Ocean GameObject → WaterSurface Inspector → General → **Script Interactions** 체크
- **파도 파라미터**: HDRP Water Surface Inspector에서 실시간 조정 가능
  - `largeWindSpeed`, `largeChaos`, `largeWaveAmplitude` 등

---

## 선체 제원 (SeaBoat24Ft 기준)
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| Lpp | 7.3 m | 수선간 길이 (Length between perpendiculars) |
| B | 2.5 m | 선폭 (Breadth) |
| d | 0.5 m | 흘수 (Draft) |
| Cb | 0.45 | 블록계수 (V-hull 레저보트) |
| m | 1,500 kg | 선체 질량 |
| Dp | 0.35 m | 프로펠러 직경 |
| 해당 선종 | 소형 레저보트 | 25~40hp 아웃보드 모터 기준 |

---

## Boat Physics

### Rigidbody (PhysX)
| 항목 | 값 | 비고 |
|------|-----|------|
| Mass | 1,500 kg | BoatMMGController.Awake() 코드 설정 |
| Linear Damping | **0** | 코드 강제 설정 — Inspector 값 무시, MMG X_RR이 담당 |
| Angular Damping | **0.5** | 코드 강제 설정 — 요 댐핑 소량만 유지 |
| Constraints | **None** | 코드 강제 설정 — roll/pitch 자유, 부력/트림이 자세 제어 |
| Inertia Tensor | (1063, 7000, 7443) | 직육면체 근사 (L=7.3, B=2.5, H=1.5) |
| Collision Detection | Continuous Dynamic | |
| Automatic Inertia | false | 수동 설정 |

### MMG Standard Model (BoatMMGController.cs)
MMG (Maneuvering Modeling Group) 표준 모델 — Hull + Propeller + Rudder 힘 분리

#### 부가 질량 (Added Mass) — Inoue 경험식
| 파라미터 | 공식 | 값 |
|----------|------|-----|
| mx (surge) | 0.020 × m | 30 kg |
| my (sway) | π × (d/Lpp)² × Cb × 0.882 × m | ~3.4 kg |
| Jz (yaw) | 0.011 × m × Lpp² | 882 kg·m² |
| Iz (yaw inertia) | (1/12) × m × (Lpp²+B²) × 0.3 | ~8,330 kg·m² |

#### 선체 미분계수 — Yoshimura 2006 소형선 경험식
| 계수 | 공식 | 물리 의미 |
|------|------|-----------|
| Yv | -0.315 × (m+my) | 횡력/횡속도 미분 |
| Yr | (0.379-0.5) × (m+my) × Lpp | 횡력/요레이트 미분 |
| Nv | -0.137 × (m+my) × Lpp | 요모멘트/횡속도 미분 |
| Nr | -0.049 × (m+my) × Lpp² | 요모멘트/요레이트 미분 |

#### 프로펠러
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| t_P | 0.17 | 추력 감소 계수 (thrust deduction factor) |
| w_P0 | 0.20 | 반류 계수 (wake fraction) |
| K_T 곡선 | 0.45 - 0.463×J (≥0) | 추력 계수 (advance ratio J 기반) |
| T | 1025 × n² × Dp⁴ × K_T | 프로펠러 추력 (N) |
| X_P | (1 - t_P) × T × gearSign | 유효 추진력 |

#### 기어 시스템
| 기어 | gearSign | Prop Walk 계수 | 러더 효과 |
|------|----------|----------------|-----------|
| FORWARD | +1 | +0.04 (우현 편류) | 100% |
| NEUTRAL | 0 | 0 | 100% |
| REVERSE | -1 | -0.08 (좌현 편류 2배) | 30% |

#### 저항 모델
```
X_H  = -0.5 × ρ × Lpp × d × U² × 0.08 × β²        (편류 저항)
X_RR = -0.5 × ρ × Lpp × d × 0.06 × humpMult × trimResist × u|u|  (전진 저항)
humpMult  = 1 + 0.8 × exp(-((speedKn - 10) / 3)²)  (planing 곡선)
trimResist = 1 - trimAngleDeg × 0.015               (트림 저항 보정)
```
- Hump 구간 (8~12kn): 저항 최대 1.8배
- 플레이닝 이후 (12kn+): 저항 1.0배 수렴

#### 러더
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| A_R | 0.053 m² | 러더 면적 |
| f_alpha | 2.45 | 러더 양력 경사 |
| t_R | 0.35 | 러더 추력 감소 계수 |
| a_H | 0.312 | 선체 간섭 계수 |
| x_R | -0.5 × Lpp | 러더 위치 |
| x_H | -0.464 × Lpp | 선체 작용점 위치 |
| 속도 의존 | speedFactor = Clamp01(U / 3f) | 3 m/s 이하 선형 효과 감소 |

#### 로컬 축 매핑 (boat_24.FBX 고유 — 수정 금지)
| 방향 | Unity 로컬 축 |
|------|--------------|
| bow (선수) | **-X** (-transform.right) |
| starboard (우현) | **+Z** (+transform.forward) |
| up | **+Y** |

#### 트림 물리
- `trimAngleDeg`: -20° (트림 인) ~ +20° (트림 아웃)
- 저항 보정: `trimResist = 1 - trimAngleDeg × 0.015` (아웃+20° → 저항 30% 감소)
- 피치 토크: `-transform.forward × trimAngleDeg × 80f` (N·m, 계수 80 튜닝 가능)

---

### Hull Point Buoyancy (BuoyancySystem.cs)

#### 방식
10개 hull 포인트에 독립 부력 적용. 각 포인트에서 HDRP 파도 높이를 쿼리해 수심(depth) 계산.

```
depth = max(0, waterHeight - pointY)
F_buoyancy = depth × buoyancyFactor / n
F_damping  = -vy × dampingFactor / n   (수중일 때만)
```

#### Hull Point 배치 (10개)
| 이름 | 위치 설명 |
|------|-----------|
| HullPoint_BowCenter | 선수 중앙 |
| HullPoint_BowPort | 선수 좌현 |
| HullPoint_BowStarboard | 선수 우현 |
| HullPoint_MidPort | 중앙 좌현 |
| HullPoint_MidStarboard | 중앙 우현 |
| HullPoint_MidCenter | 중앙 |
| HullPoint_SternPort | 선미 좌현 |
| HullPoint_SternStarboard | 선미 우현 |
| HullPoint_SternCenter | 선미 중앙 |
| HullPoint_Keel | 용골 (최하부) |

#### 파라미터
| 파라미터 | 값 | 설명 |
|----------|-----|------|
| buoyancyFactor | 60,000 | 깊이 1m당 부력 (N/m) |
| dampingFactor | 10,000 | 수직 속도 댐핑 계수 |
| fallbackWaterLevel | 0f | API 실패 시 기본 수면 높이 |

#### 구현 핵심 — candidateLocationWS 보존 (수정 금지)
`ProjectPointOnWaterSurface()` 실패 시 Unity가 result struct를 (0,0,0)으로 리셋함.
다음 프레임에서 world origin(0,0,0)부터 수렴 탐색 시작 → 영원히 실패 루프.

**픽스**: 실패 시 candidateLocationWS를 hull point XZ + 마지막 유효 waterH로 수동 복원.
```csharp
_searchResults[idx].candidateLocationWS =
    new Vector3(worldPos.x, _lastValidWaterHeight[idx], worldPos.z);
```
`_lastValidWaterHeight[]` 캐시로 마지막 유효 수면 높이 포인트별 유지.
초기값은 Awake()에서 hull point XZ + fallbackWaterLevel로 설정.

#### 검증 결과
- `waterH` 범위: -0.4 ~ +0.58 (실제 HDRP 파도 반영)
- 파도에 따라 각 포인트 depth 실시간 변동 확인

---

## 성능 특성 (검증 완료)
| 조건 | 속도 |
|------|------|
| 트림 인, displacement 모드 | 9~10 knots |
| 트림 아웃, planing 진입 | **11~12 knots** |
| 해당 선종 | 소형 레저보트 25~40hp |
| Hump 구간 | 8~12kn (저항 최대 1.8배) |
| Hump 돌파 조건 | 트림 아웃 + 풀 스로틀 |

---

## Input (BoatInputHandler.cs)

#### 파라미터
| 변수 | 값 | 설명 |
|------|----|------|
| throttleSpeed | 10f/s | W/S 입력 시 RPS 증가 속도 |
| throttleDecay | 5f/s | 입력 없을 때 RPS 감소 속도 |
| gearShiftMaxRPS | 5f | 이 값 이하일 때만 F키 변속 허용 |
| rudderSpeed | 40f°/s | A/D 타각 변화 속도 (복원 없음) |
| trimSpeed | 8f°/s | Q/E 트림 변화 속도 |

#### 키맵
| 키 | 기능 | 조건 |
|----|------|------|
| W | FORWARD 기어 체결 + RPS 증가 | NEU·FWD 상태에서만 체결 |
| S | REVERSE 기어 체결 + RPS 증가 | NEU·REV 상태에서만 체결 |
| W (REV 중) | RPS 감소만 | 기어 변속 없음 |
| S (FWD 중) | RPS 감소만 | 기어 변속 없음 |
| A / D | 타각 좌/우 (복원 없음 — 유지) | 항상 |
| Q / E | 트림 아웃 / 트림 인 | 항상 |
| F | 뉴트럴 변속 | RPS ≤ 5 조건 |
| W/S 뗌 | RPS 아이들 복귀 | 기어 유지 |
| Space | 킬스위치 (즉시 NEU + RPS=0 + 타각=0) | 무조건 |

---

## Debug UI (BoatDebugUI.cs)
- `Gear: FWD/NEU/REV  RPS: XX.X`
- `Rudder: XX.X°  Trim: XX.X° [OUT/IN XX%저항]`
- `Speed: XX.X kn`

---

## Assets
- 배: `Resources/Boat/SeaBoat24Ft/FBX/boat_24.FBX`
- 부두: `Resources/Dock/`
- UI: `Resources/5_UI/`, `6_Images/`, `7_Arrow/`, `8_MiniMap/`

---

## Scene (SampleScene.unity)
| GameObject | 컴포넌트 |
|------------|----------|
| Sun | Directional Light |
| Sky_Volume | Global Volume (HDRP Sky + Fog) |
| Ocean | WaterSurface (Script Interactions 활성화 필수) |
| Boat | boat_24.FBX + Rigidbody + BoatMMGController + BuoyancySystem + BoatInputHandler + BoatDebugUI |
| Dock | Mesh + Collider |
| DebugCanvas | Canvas (Screen Space Overlay) + DebugPanel |

---

## Scripts
```
Assets/MarineDigitalTwin/Features/Boat/Scripts/
├── BoatMMGController.cs   ← MMG 물리 (기어/추진/저항/PropWalk/Planing/Trim)
├── BuoyancySystem.cs      ← hull point 부력 10포인트 (HDRP Water API)
├── BoatInputHandler.cs    ← 키보드 입력 (기어/타각/트림/킬스위치)
└── BoatDebugUI.cs         ← 속도/기어/타각/트림 HUD
```

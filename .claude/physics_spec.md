# Boat Physics Specification

## Overview
- Engine: Unity Physics (Rigidbody)
- Water surface: HDRP Water System (`WaterSurface` component)
- No third-party physics (NWH/DWP2/Crest removed)
- All physics implemented from scratch

---

## Rigidbody Settings
| Property | Value |
|----------|-------|
| Mass | 1500 kg |
| Drag | 0.5 |
| Angular Drag | 2.0 |
| Use Gravity | true |
| Collision Detection | Continuous |

---

## BuoyancySystem.cs

### Concept
- Place multiple **sample points** on boat hull (bow, stern, port, starboard, center)
- Each frame: query water surface height at each point
- If point is below water → apply upward force proportional to submersion depth

### Water Height Query
```csharp
WaterSurface waterSurface;
WaterSearchParameters searchParams;
WaterSearchResult searchResult;

waterSurface.FindWaterSurfaceHeight(ref searchParams, ref searchResult);
float waterHeight = searchResult.height;
```

### Buoyancy Force Formula
```
submergedDepth = waterHeight - pointPosition.y
if (submergedDepth > 0):
    force = UP * submergedDepth * buoyancyFactor * (1/numPoints)
    rigidbody.AddForceAtPosition(force, pointPosition)
```

### Parameters
| Parameter | Value |
|-----------|-------|
| buoyancyFactor | 15000 |
| damping | 0.1 (velocity-based counter force) |
| numHullPoints | 5~8 |
| Hull point positions | Relative to boat center (serialized in Inspector) |

---

## BoatController.cs

### Input
- **Throttle**: W/S or Vertical Axis → 0.0 ~ 1.0
- **Steering**: A/D or Horizontal Axis → -1.0 ~ 1.0
- Input System package (`InputAction`)

### Engine Force
```
thrustForce = throttle * maxThrust
rigidbody.AddForceAtPosition(boat.forward * thrustForce, propellerPosition)
```

### Steering (Rudder)
```
torque = steerInput * maxTorque
rigidbody.AddTorque(boat.up * torque)
```

### Parameters
| Parameter | Value |
|-----------|-------|
| maxThrust | 8000 N |
| maxTorque | 3000 Nm |
| maxSpeed | 15 m/s (~30 knots) |
| acceleration curve | AnimationCurve (ease-in) |

### Propeller Position
- Rear center of boat
- Serialized Transform reference in Inspector

---

## BoatModelController.cs (Visual only)
- Tilt boat mesh based on velocity/acceleration (visual feedback)
- Propeller rotation animation
- Wake particle effect trigger on speed threshold

---

## WindForce.cs (Optional)
- Apply constant lateral force based on wind direction/speed
- Wind direction: configurable in Inspector or via WeatherManager

---

## Old Code Reference (구버전 — 참고만)
구버전 `Boat.cs` 주요 파라미터:
```
minXAngle = 0
maxXAngle = -15   (pitch limit)
minZAngle = -10   (roll limit)
maxZAngle = 10
sendTickTime = 0.05f  (hardware data send interval)
```

구버전은 NWH Vehicle Physics 기반이라 직접 포팅 불가.
위 스펙으로 새로 구현.

---

## Script File Locations
```
Assets/MarineDigitalTwin/Features/Boat/Scripts/BoatController.cs
Assets/MarineDigitalTwin/Features/Boat/Scripts/BuoyancySystem.cs
Assets/MarineDigitalTwin/Features/Boat/Scripts/BoatModelController.cs
Assets/MarineDigitalTwin/Features/Boat/Physics/WindForce.cs
```

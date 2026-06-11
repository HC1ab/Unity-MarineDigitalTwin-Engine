# MCP Task: Boat Physics Setup in Unity Scene

## Project Info
- Unity 6000.0.76f1 + HDRP 17.0.4
- Scene: Assets/Scenes/SampleScene.unity
- Boat object in Hierarchy: "Boat"
- Ocean object in Hierarchy: "Ocean" (has WaterSurface component)

## Scripts Already Written (DO NOT rewrite)
```
Assets/MarineDigitalTwin/Features/Boat/Scripts/BoatMMGController.cs
Assets/MarineDigitalTwin/Features/Boat/Scripts/BuoyancySystem.cs
```

---

## Task 1: Add Components to Boat GameObject

Select "Boat" in Hierarchy, then add these components via Inspector:

1. **Rigidbody**
   - Mass: 1500
   - Linear Drag: 0
   - Angular Drag: 0
   - Use Gravity: true
   - Collision Detection: Continuous

2. **BoatMMGController** (from Assets/MarineDigitalTwin/Features/Boat/Scripts/)
   - Rudder Angle Deg: 0
   - Propeller RPS: 10

3. **BuoyancySystem** (from Assets/MarineDigitalTwin/Features/Boat/Scripts/)
   - Water Surface: drag "Ocean" object here
   - Buoyancy Factor: 15000
   - Damping Factor: 500

---

## Task 2: Create Hull Points

Under "Boat" GameObject, create 10 empty child GameObjects named:
```
HullPoint_BowCenter
HullPoint_BowPort
HullPoint_BowStarboard
HullPoint_MidPort
HullPoint_MidStarboard
HullPoint_MidCenter
HullPoint_SternPort
HullPoint_SternStarboard
HullPoint_SternCenter
HullPoint_Keel
```

Position each relative to Boat (local coordinates).
Boat length = ~7.3m, width = ~2.5m, draft = ~0.5m

Approximate local positions:
| Name | X | Y | Z |
|------|---|---|---|
| HullPoint_BowCenter | 0 | -0.3 | 3.2 |
| HullPoint_BowPort | -0.9 | -0.3 | 2.8 |
| HullPoint_BowStarboard | 0.9 | -0.3 | 2.8 |
| HullPoint_MidPort | -1.1 | -0.4 | 0 |
| HullPoint_MidStarboard | 1.1 | -0.4 | 0 |
| HullPoint_MidCenter | 0 | -0.5 | 0 |
| HullPoint_SternPort | -0.9 | -0.3 | -2.8 |
| HullPoint_SternStarboard | 0.9 | -0.3 | -2.8 |
| HullPoint_SternCenter | 0 | -0.3 | -3.2 |
| HullPoint_Keel | 0 | -0.5 | 0 |

---

## Task 3: Register Hull Points in BuoyancySystem

On "Boat" Inspector → BuoyancySystem component:
- Hull Points array size: 10
- Assign each HullPoint_* GameObject to the array in order

---

## Task 4: Connect Water Surface

On "Boat" Inspector → BuoyancySystem component:
- Water Surface field → drag "Ocean" GameObject from Hierarchy

---

## Task 5: Verify Setup

Check in Inspector that:
- [ ] Boat has Rigidbody, BoatMMGController, BuoyancySystem
- [ ] BuoyancySystem.hullPoints has 10 entries (none null)
- [ ] BuoyancySystem.waterSurface = Ocean
- [ ] Press Play → Boat should float on water surface without sinking

---

## Task 6: Adjust Boat Y Position

Before pressing Play:
- Set Boat Y position = 1.0 (slightly above water so buoyancy can catch it)
- Water surface is at Y = 0

---

## Expected Result After Play
- Boat settles on water surface (Y ≈ 0.3~0.5)
- No sinking through water
- Slight rocking motion from buoyancy damping
- In Inspector: change Propeller RPS → boat moves forward
- In Inspector: change Rudder Angle Deg → boat turns

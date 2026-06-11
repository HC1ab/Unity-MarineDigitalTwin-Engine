# MarineDigitalTwin — Scene Setup Instructions

## Project Context
- Unity 6000.0.76f1 + HDRP 17.0.4
- Boat simulation / digital twin project
- No URP (removed) — HDRP only
- Water: HDRP built-in Water System (no Crest)

## Asset Locations
```
Assets/MarineDigitalTwin/Resources/Boat/SeaBoat24Ft/FBX/boat_24.FBX
Assets/MarineDigitalTwin/Resources/Boat/SeaBoat24Ft/Texture/
Assets/MarineDigitalTwin/Resources/Dock/
Assets/MarineDigitalTwin/Resources/5_UI/
Assets/MarineDigitalTwin/Resources/6_Images/
Assets/MarineDigitalTwin/Resources/7_Arrow/
Assets/MarineDigitalTwin/Resources/8_MiniMap/
```

---

## Scene Setup Tasks

### 1. Open Scene
- Open: `Assets/Scenes/SampleScene.unity`
- Rename scene to `MainScene` (optional)

### 2. Directional Light
- Hierarchy → Create → Light → **Directional Light**
- Name: `Sun`
- Rotation: X=50, Y=-30, Z=0
- Intensity: 100000 (HDRP uses Lux)
- Enable **Shadows**

### 3. HDRP Sky + Fog (Global Volume)
- Hierarchy → Create → Volume → **Global Volume**
- Name: `Sky_Volume`
- Inspector → Profile → **New**
- Add Overrides:
  - **Visual Environment** → Sky Type: `HDRI Sky` (or Physically Based Sky)
  - **HDRI Sky** → Exposure: 1
  - **Fog** → Enable, Base Height: 0, Mean Free Path: 500
  - **Ambient Occlusion** → Enable

### 4. Ocean Water Surface
- Hierarchy → Create → Water → **Ocean, Sea and Coastline**
- Name: `Ocean`
- Position: (0, 0, 0)
- Inspector settings:
  - Geometry → Size: X=2000, Z=2000
  - Simulation → Agitation: 2 (calm water)
  - Enable **Foam**
  - Enable **Caustics**

### 5. Place Boat
- Drag `Assets/MarineDigitalTwin/Resources/Boat/SeaBoat24Ft/FBX/boat_24.FBX` into Hierarchy
- Name: `Boat`
- Position: (0, 0.5, 0) — slightly above water surface
- Scale: (1, 1, 1) — adjust if too large/small
- Add component: **Rigidbody**
  - Mass: 1500
  - Drag: 0.5
  - Angular Drag: 2
  - Use Gravity: true

### 6. Place Dock
- Drag dock FBX from `Assets/MarineDigitalTwin/Resources/Dock/` into Hierarchy
- Name: `Dock`
- Position: (20, 0, 0) — beside boat start position
- Add **Box Collider** (or Mesh Collider)

### 7. Camera Setup
- Main Camera position: (0, 3, -8) relative to boat (or use Cinemachine)
- If using Cinemachine:
  - Package Manager → install **Cinemachine**
  - Hierarchy → Create → Cinemachine → **CinemachineCamera**
  - Set Follow + LookAt target to `Boat`

### 8. Convert Materials to HDRP
- Menu: Edit → Rendering → Materials → **Convert All Built-in Materials to HDRP**
- This upgrades boat_24 and dock materials from Standard → HDRP Lit

### 9. Scene Lighting Bake (optional for now)
- Window → Rendering → Lighting → **Generate Lighting**

---

## Folder Structure to Create (Scripts — not yet written)
```
Assets/MarineDigitalTwin/
├── Features/
│   ├── Boat/
│   │   ├── Scripts/
│   │   │   ├── BoatController.cs       (engine, throttle, steering)
│   │   │   └── BuoyancySystem.cs       (HDRP Water query-based buoyancy)
│   │   └── Physics/
│   ├── Ocean/
│   ├── Weather/
│   ├── UI/
│   └── Sensor/
├── Core/
│   ├── Constants/
│   ├── Network/
│   └── Storage/
└── Resources/  (already exists)
```

---

## Notes
- HDRP Wizard already completed (all green)
- URP package removed — do NOT re-add it
- Water system is HDRP native — no third-party water assets needed
- Boat physics scripts will be written separately after scene is set up

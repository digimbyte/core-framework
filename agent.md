# Core Framework - Package Architecture
This project is distributed as a Unity package via git URL (`com.digimbyte.core`). As such, it relies on Assembly Definition files (`.asmdef`) to expose APIs to consumers. The framework is designed to be genre-agnostic and modular, supporting FPS, third-person action, 2D platformers, 3D platformers, cozy RPGs, and other game types.

## Assembly Definitions
The package exposes the following assemblies:
- `Core.Registry` - Asset registry and object pooling
- `Core.Registry.Editor` - Editor tooling for registry
- `Core.Data` - Data persistence and I/O (local, cloud, settings)
- `Core.Data.Editor` - Editor tooling for data
- `Core.Data.Localisation.Editor` - Localization tooling
- `Core.Signals` - Signal/event system (pub-sub)
- `Core.Utility` - Utility classes (StringCompression, terminal, cheat engine)
- `Nova` - GUI system (built on Nova framework)
- `Nova.Editor` - GUI editor tooling
- `Nova.UIControls` - Common UI control components
- `Animator` / `Animator.Editor` - Animation utilities
- Third-party: `TelePresent.AudioSyncPro`, `DarkTonic.MasterAudio`, Odin Inspector, YamlDotNet

Only code within these `.asmdef` boundaries is part of the public API.

---

# Framework Assessment

## What We Have ✅

### Core Systems (Production-Ready)
1. **Asset Registry System** - Mature and comprehensive
   - Type-safe, dictionary-backed O(1) lookups
   - Object pooling with configurable pool sizes
   - Composite key support for hierarchical organization
   - Tag-based queries and metadata storage
   - Runtime overrides for modding/seasonal content
   - Good editor tooling and validation

2. **Signal/Event System** - Implemented
   - Signal class for pub-sub messaging
   - SignalHub for centralized signal management
   - Part of core public API

3. **Data Persistence Layer** - Framework exists
   - Handles local and cloud I/O
   - Session data and persistent storage
   - Settings configuration
   - Supports offline vs online modes

4. **GUI System**
   - Nova framework integration
   - Editor tooling support
   - Common UI controls package

5. **Animation Utilities**
   - Animator assembly for animation management

6. **Development Tools**
   - Terminal console (Nova-based)
   - Cheat engine system
   - Relations inspector (third-party)

### Infrastructure
- Third-party integrations: Audio (MasterAudio), UI (Nova), Inspector (Odin), YAML serialization
- A* pathfinding (via external package)
- Firebase integration available

---

## What We Don't Have / Is Incomplete ⚠️

### 1. **Input System** (CRITICAL)
- No unified input abstraction layer
- Missing support for:
  - Rebindable input mappings
  - Input event delegation (OnMouseDown, OnKeyPressed, etc.)
  - Multi-input combinations (Ctrl+Alt+X)
  - Touch/gamepad input abstractions
  - Input mode switching (UI vs Gameplay)
  - Input buffering for fighting games / fast-paced action

### 2. **State Machine Framework** (CRITICAL)
- No FSM or HSM (Hierarchical State Machine) system
- Needed for:
  - Character controller states (idle, walk, run, jump, fall, etc.)
  - Combat states (attacking, defending, knocked-back, etc.)
  - Game flow (menu, gameplay, pause, loading, etc.)
  - AI behavior trees / agents
  - Animation state coordination

### 3. **Physics/Movement Foundation** (CRITICAL for action games)
- No character controller abstraction
- No movement prediction/prediction system
- Missing support for:
  - 2D vs 3D movement abstractions
  - Acceleration/deceleration curves
  - Slope handling
  - Air control
  - Dash/sprint mechanics
  - Jump physics (height, arc, coyote time)
  - Knockback/pushback systems

### 4. **Damage/Combat System** (INCOMPLETE)
- No unified damage/combat framework
- Missing:
  - Damage types (physical, fire, ice, etc.)
  - Hit detection system (raycast, collision, proximity)
  - Damage calculation pipeline
  - Status effects / condition system
  - Knockback parameters
  - Invincibility frames (i-frames)
  - Combo system (if needed for the game type)

### 5. **Animation System** (INCOMPLETE)
- Animator assembly exists but lacks:
  - Animation state synchronization with game state
  - Blend space abstractions
  - Animation events/notifications system
  - Procedural animation support
  - Cross-fade control
  - Animation pooling/caching

### 6. **Networking** (STUB ONLY)
- Basic framework documented but incomplete
- Missing:
  - Serialization system (NetMessage, NetSync, etc.)
  - P2P vs client-server abstractions
  - Steam integration (referenced but not implemented)
  - Matchmaking service
  - Host migration
  - Latency compensation / client-side prediction
  - Bandwidth optimization
  - Currently relies on external UnityNetworkLayer package

### 7. **Authentication** (STUB ONLY)
- Auth.md exists but only describes the plan
- Needs implementation for:
  - Offline authentication (localStorage)
  - Firebase authentication
  - Session management
  - Account persistence

### 8. **Localization** (PARTIAL)
- Folder structure exists but minimal documentation
- Needs:
  - String table system
  - Runtime language switching
  - Gender/pluralization support
  - RTL language support (if needed)
  - Audio localization hooks

### 9. **Resource Management** (MISSING)
- No unified resource limits system
- Missing:
  - Resource pooling (memory, object handles)
  - Garbage collection tuning
  - Budget profiling (FPS targets, memory limits)
  - Asset streaming / LOD system

### 10. **UI System Abstractions** (PARTIAL)
- Nova integration exists but lacks:
  - Common screen/panel lifecycle
  - Navigation system (breadcrumbs, back button flow)
  - Binding/MVVM patterns
  - Dialog/popup abstraction
  - Persistent UI elements (health bar, minimap)
  - UI event handling delegation
  - Inventory/grid UI patterns

### 11. **Camera System** (MISSING)
- No camera abstraction or management
- Needed for:
  - Follow cameras (3rd person, isometric)
  - First-person camera (FPS)
  - Pan/zoom controls
  - Cinematic camera scripting
  - Screen shake effects
  - Viewport management (split-screen)

### 12. **VFX/Particle Management** (MISSING)
- No particle system pooling or abstraction
- Missing:
  - Particle effect lifecycle management
  - Impact effect spawning (blood, dust, sparks)
  - Trail renderers
  - Decal system

### 13. **Quest/Progression System** (MISSING)
- No quest framework
- No achievement/progression tracking
- Needed for:
  - Quest state management (active, completed, failed)
  - Quest objectives
  - Player progression tracking
  - Level progression
  - Skill/ability tree management

### 14. **Inventory System** (MISSING)
- No inventory abstraction
- Missing:
  - Item management
  - Equipment slots
  - Inventory UI binding
  - Item drop/pickup mechanics
  - Carry limits/weight system

### 15. **Scene Management** (MISSING)
- No scene/level loading abstraction
- Missing:
  - Async scene loading
  - Transition effects
  - Level streaming
  - Spawn point management
  - Scene context persistence

### 16. **AI System** (MISSING)
- No AI framework or behavior trees
- Missing (partially handled by A* pathfinding):
  - Steering behaviors
  - Patrol patterns
  - Combat AI
  - Sight/hearing systems
  - Group behavior (flocking, formations)

### 17. **Dialogue System** (MISSING)
- No dialogue framework
- Missing:
  - Dialogue trees
  - Choice branching
  - Character expressions
  - Audio syncing
  - Choice callbacks

### 18. **Save System** (INCOMPLETE)
- Data layer exists but needs clear abstractions for:
  - Save file versioning
  - Slot management
  - Auto-save logic
  - Save data validation
  - Cloud save sync

---

# Recommendations

## Priority Tier 1 (Foundation - Blocks multiple systems)

### 1. **Input System** (6-8 hours)
- Abstract input into `InputProvider` interface
- Create `InputAction` system with rebinding support
- Support keyboard, mouse, touch, gamepad via inheritance
- Emit input events through Signal system
- Per-mode input masks (gameplay vs UI)
**Impact**: Required for all action gameplay

### 2. **State Machine Framework** (4-6 hours)
- Implement generic `StateMachine<T>` base
- Support hierarchical states and submachines
- Integrate with Signal system for state transitions
- Built-in transition guards/conditions
**Impact**: Unblocks character controller, AI, game flow

### 3. **Character Controller Base** (8-10 hours)
- Abstract `CharacterController` interface
- Separate 2D and 3D implementations
- Handle velocity, acceleration, ground detection
- Support core mechanics: walk, run, jump, fall, dash
- Use StateMachine internally
**Impact**: Essential for any game with player movement

## Priority Tier 2 (Gameplay Systems - Genre-specific but common)

### 4. **Camera System** (4-6 hours)
- Abstract `CameraController` interface
- Implement: Follow, FirstPerson, Isometric variants
- Support smooth follow and lookahead
- Screen shake effects

### 5. **Damage/Combat System** (6-8 hours)
- `DamageSource` scriptable type
- `Damageable` component interface
- Hit detection abstraction (line, area, collision)
- Knockback parameters
- Status effect system

### 6. **UI Abstractions** (6-8 hours)
- `Screen` base class for UI panels
- Navigation system for modal stacking
- Common patterns: menus, inventory, dialog
- Binding/data context for MVVM patterns

### 7. **Scene/Level Management** (4-6 hours)
- `SceneManager` wrapper for async loading
- Spawn point registry
- Scene context for persistent data
- Transition effects

## Priority Tier 3 (Genre-specific or advanced)

### 8. **Animation Integration** (Expand existing)
- Sync Animator parameters with game state (movement, combat)
- Animation notification system
- Procedural animation support

### 9. **AI/Behavior Trees** (Leverage A* integration)
- Steering behaviors
- Simple behavior tree or utility AI system
- Sight/hearing detection
- Patrol patterns

### 10. **Inventory System** (6-8 hours)
- `Item` scriptable type
- `Inventory` manager with slots
- Equipment system
- Drop/pickup mechanics

### 11. **Quest/Progression** (6-8 hours)
- Quest scriptable with objectives
- Quest tracker
- Progression tracking

### 12. **Dialogue System** (4-6 hours)
- Simple dialogue tree
- Choice branching
- Integration with audio system

## Priority Tier 4 (Polish & Optimization)

### 13. **Resource Management**
- Memory budgeting tools
- Profiler hooks
- Asset streaming

### 14. **VFX Management**
- Particle pooling
- Impact effect spawner
- Decal system

### 15. **Networking** (Expand stub)
- Implement serialization layer
- Client-side prediction
- Host migration
- Complete Steam/custom hosting support

### 16. **Authentication** (Implement stub)
- Firebase auth flow
- Offline auth fallback
- Session management

### 17. **Localization** (Expand stub)
- String table system
- Runtime language switching
- RTL support

---

# Architecture Patterns

The framework should continue to use:
- **Signals** for event communication (already in place)
- **Registry** for asset/prefab management (already in place)
- **ScriptableObjects** for configuration and data
- **Interfaces** for abstraction (2D vs 3D, Online vs Offline)
- **MonoBehaviour singletons** for managers (sparingly)
- **Assembly Definitions** to enforce clear API boundaries

---

# Estimated Effort

- **Tier 1 (Foundation)**: ~20-25 hours
- **Tier 2 (Gameplay)**: ~30-35 hours
- **Tier 3 (Genre-specific)**: ~40-50 hours
- **Tier 4 (Polish)**: ~30-40 hours

**Total for comprehensive framework: ~120-150 hours**

Focusing on Tier 1 + Tier 2 would provide a solid foundation for **most game types in 50-60 hours**.

---

# Next Steps

1. Start with **Input System** (unblocks everything else)
2. Implement **State Machine** (small, high-impact)
3. Build **Character Controller** (validates Input + StateMachine)
4. Add **Camera System** (polish gameplay feel)
5. Implement **Combat/Damage** (genre-specific but common)
6. Expand incrementally based on game requirements

# WukongCombatKit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship an independent B1CSharpLoader + Harmony mod that lets space cancel light-attack combos and makes player attacks hit unobstructed enemies in a near-infinite sphere.

**Architecture:** Keep cancel/hit rules in a net472 class library with no game assemblies so they can be unit-tested. The runtime mod only reads config, registers Harmony patches, and calls those rules. Dodge cancel widens the original dodge-window / CheckDodgeState gates for light-attack combos only. Omni-hit runs after a player SweepCheck and injects extra enemy hits that pass a static-world line trace.

**Tech Stack:** C# / net472 / Harmony / B1CSharpLoader / xUnit tests that do not reference GameDll.

## Global Constraints

- Independent single-player C# mod named `WukongCombatKit`; do not modify AutoPerfectDodge.
- Loader: B1CSharpLoader + Harmony; no UE4SS/Lua and no pak replacement as the main path.
- Target runtime is the game's Net 4.0 / Mono; do not introduce `System.Private.CoreLib` or net10 assembly references.
- GameDll references are local only and must not be committed.
- Two features have independent toggles and isolated try/catch.
- Do not auto-dodge, force perfect dodge, change damage/poise/HP, or touch multiplayer / anti-cheat / DRM.
- Config defaults: `EnableDodgeCancel=true`, `EnableOmniHit=true`, `MaxAttackRange=100000`, `DebugLog=false`.
- Install path: `BlackMythWukong/b1/Binaries/Win64/CSharpLoader/Mods/WukongCombatKit/` with `WukongCombatKit.dll`, `config.json`, `WukongCombatKit.log`.

---

### Task 1: Core cancel and hit rules

**Files:**
- Create: `src/WukongCombatKit.Core/WukongCombatKit.Core.csproj`
- Create: `src/WukongCombatKit.Core/CombatKitConfig.cs`
- Create: `src/WukongCombatKit.Core/DodgeCancelRules.cs`
- Create: `src/WukongCombatKit.Core/OmniHitRules.cs`
- Create: `tests/WukongCombatKit.Tests/WukongCombatKit.Tests.csproj`
- Create: `tests/WukongCombatKit.Tests/DodgeCancelRulesTests.cs`
- Create: `tests/WukongCombatKit.Tests/OmniHitRulesTests.cs`
- Create: `tests/WukongCombatKit.Tests/CombatKitConfigTests.cs`

**Interfaces:**
- Consumes: none
- Produces: `CombatKitConfig`, `DodgeCancelRules.ShouldAllowDodgeCancel(...)`, `OmniHitRules.SelectVisibleEnemies(...)`

- [x] **Step 1: Write failing tests and core implementation**
- [x] **Step 2: Run tests**
- [x] **Step 3: Commit**

### Task 2: Runtime mod, Harmony patches, deploy

**Files:**
- Create: `src/WukongCombatKit/WukongCombatKit.csproj`
- Create: `src/WukongCombatKit/MyMod.cs`
- Create: `src/WukongCombatKit/ModLog.cs`
- Create: `src/WukongCombatKit/ConfigStore.cs`
- Create: `src/WukongCombatKit/DodgeCancel.cs`
- Create: `src/WukongCombatKit/OmniHit.cs`
- Create: `src/WukongCombatKit/config.json`
- Create: `WukongCombatKit.sln`
- Create: `README.md`
- Create: `.gitignore`
- Create: `scripts/deploy.ps1`

**Interfaces:**
- Consumes: Task 1 rules
- Produces: `ICSharpMod` entry `MyMod`, Harmony patches on `BUS_PlayerInputActionComp.DoAttackLogic`, `GSSkillCastChecker.CheckDodgeState`, `BUS_SweepCheckHitComp.SweepCheckInternal`

- [x] **Step 1: Implement runtime mod**
- [x] **Step 2: Build Release and deploy**
- [x] **Step 3: Commit**

# Staff System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add hireable cleaner and server staff who autonomously perform café work, persist across saves, receive daily wages, and appear in the existing clay-style UI.

**Architecture:** `StaffRoster` owns hiring, upgrades, persistence, wages, and spawned workers. Each `StaffWorker` is a small state machine that claims existing `WorkQueue` jobs or server-delivery jobs without changing the owner's priority. UI and bootstrap integrations remain thin adapters around that domain layer.

**Tech Stack:** Unity 6.6, C#, uGUI, AI Navigation/NavMesh, JsonUtility save v3, Unity Test Framework 1.8.

**Spec:** `docs/phase2_spec.md` §4 and `docs/economy_monetization.md` §5–6

## Global Constraints

- Unity namespace remains `Cafe`; no new external package.
- Mobile landscape clay UI must reuse `HudBuilder`, `ClayButton`, `UiTheme`, and `UiFx`.
- Initial roles are cleaner (`clean`) and server (`serve`); brewer remains future scope.
- Cleaner hire fee 1,500 Xu, wage 300/day, level-1 table time 6.0 seconds.
- Server hire fee 1,500 Xu and wage 350/day.
- Owner can always steal a `WorkQueue` claim; staff must cancel cleanly when displaced.
- Wage shortfalls enter the existing `TierManager`/`RentLedger` debt and appear in the daily summary.
- Existing v3 saves without staff must load as an empty roster.

## Review Focus

- Owner steals a cleaner's claimed job mid-route: staff releases it and returns idle without completing twice.
- A saved staff role or preset is unknown/out of range: load clamps or skips it without breaking the day.
- Café has no reachable sink/table/customer: worker does not spin, teleport, or hold a claim forever.
- End-of-day funds cover only part of wages: available Xu is spent once and only the shortfall becomes debt.
- Closing/upgrade destroys active customers or jobs: all staff claims and carried objects are released.

---

### Task 1: Staff economy, roster, and persistence

**Files:**
- Create: `game/Assets/_Project/Scripts/Staff/StaffRole.cs`
- Create: `game/Assets/_Project/Scripts/Staff/StaffRoster.cs`
- Modify: `game/Assets/_Project/Scripts/Core/GameBalance.cs`
- Modify: `game/Assets/_Project/Scripts/Economy/DaySummary.cs`
- Create: `game/Assets/_Project/Tests/EditMode/StaffRosterTests.cs`
- Create: `game/Assets/_Project/Tests/EditMode/Cafe.Tests.asmdef`

**Interfaces:**
- Consumes: `SaveData.staff`, `StaffSave`, `Wallet.Spend(int)`, `TierManager.AddDebt(int)`, `DaySummary`.
- Produces: `void StaffRoster.Init(GameBootstrap game, SaveData save)`, `bool Hire(StaffRole role, int preset, string name)`, `bool Fire(int index)`, `bool Upgrade(int index)`, `int PayDailyWages(DaySummary summary)`, `void WriteToSave(SaveData data)`, `int Count`, `event Action Changed`.

- [ ] **Step 1: Write failing EditMode tests**

Cover empty-save initialization, exact hire costs, max one employee per initial role, upgrade cost/level clamp 1..5, round-trip `StaffSave[]`, partial-wage debt, and invalid saved roles/presets.

- [ ] **Step 2: Run the focused tests and verify RED**

Run Unity EditMode tests filtered to `Cafe.Tests.StaffRosterTests`.
Expected: compilation/test failure because staff domain types do not exist.

- [ ] **Step 3: Implement the domain layer**

Define `StaffRole { Cleaner, Server }`, centralize fees/wages/timing in `GameBalance`, and implement `StaffRoster` without UI or navigation responsibilities. Add `staffWages` and wage debt visibility to `DaySummary` while keeping `NetProfit` correct.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: all roster/economy tests pass.

- [ ] **Step 5: Commit the task**

Commit message: `feat: add staff roster economy and persistence`

### Task 2: Autonomous staff workers

**Files:**
- Create: `game/Assets/_Project/Scripts/Staff/StaffWorker.cs`
- Create: `game/Assets/_Project/Scripts/Staff/StaffCleaner.cs`
- Create: `game/Assets/_Project/Scripts/Staff/StaffServer.cs`
- Modify: `game/Assets/_Project/Scripts/Staff/StaffRoster.cs`
- Modify: `game/Assets/_Project/Scripts/Customers/CustomerSpawner.cs`
- Modify: `game/Assets/_Project/Scripts/Owner/OwnerController.cs`
- Create: `game/Assets/_Project/Tests/EditMode/StaffWorkerTests.cs`

**Interfaces:**
- Consumes: `WorkQueue.PeekNearest`, `Claim`, `Cancel`, `Complete`; `CleaningSystem`; `Customer.IsWaitingForDrink`; existing NavMesh and prefab arrays.
- Produces: worker lifecycle `Spawn/ResetForNewDay/Stop`, deterministic job-selection helpers, and server-delivery queue hooks.

- [ ] **Step 1: Write failing state-machine tests**

Test nearest clean job selection, claim loss to owner, unreachable target timeout, clean-to-sink sequence, server selecting the oldest waiting ready order, and shutdown releasing claims.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: missing worker implementations.

- [ ] **Step 3: Implement workers minimally**

Use a shared NavMesh movement base. Cleaner handles `CleanTable` then deposits dishes and handles `WashCup`; server delivers only a drink already ready at `BrewStation`. Never let staff brew, spend ingredients, or override owner commands.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: worker tests pass with no leaked claims.

- [ ] **Step 5: Commit the task**

Commit message: `feat: automate cleaner and server work`

### Task 3: Staff panel and game integration

**Files:**
- Create: `game/Assets/_Project/Scripts/UI/StaffPanel.cs`
- Modify: `game/Assets/_Project/Scripts/UI/HudController.cs`
- Modify: `game/Assets/_Project/Scripts/UI/SummaryPanel.cs`
- Modify: `game/Assets/_Project/Scripts/Core/GameBootstrap.cs`
- Modify: `game/Assets/_Project/Scripts/Core/Strings.cs`
- Modify: `game/Assets/_Project/Scripts/Tier/TierManager.cs`
- Modify: `game/Assets/_Project/Scripts/Core/DebugAutoPilot.cs`
- Modify: `game/Assets/_Project/Editor/PrototypeSceneBuilder.cs`
- Create: `game/Assets/_Project/Tests/EditMode/StaffIntegrationTests.cs`

**Interfaces:**
- Consumes: Task 1 `StaffRoster` API and Task 2 worker lifecycle.
- Produces: `GameBootstrap.Staff`, HUD staff button/panel, summary wage row, scene prefab assignment, autoplay staff coverage.

- [ ] **Step 1: Write failing integration tests**

Assert bootstrap load/save round-trip, `Tier.StaffCount` synchronization, daily wage charged exactly once, summary net profit includes wages, and panel actions reject insufficient funds.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: bootstrap/UI integration is absent.

- [ ] **Step 3: Wire runtime and clay UI**

Initialize roster after Tier/Cleaning and before the day starts; reset workers each day; pay wages before final save; expose hire/fire/upgrade with clear costs and level; add localized strings and a summary row.

- [ ] **Step 4: Extend the autoplay path**

At Tier 2, fund/hire both roles, generate cleaning and delivery work, cross a day boundary, and emit diagnostic counts for work completed, wages, and debt.

- [ ] **Step 5: Run EditMode tests, Roslyn compile, and Unity autoplay**

Expected: all tests pass; runtime/editor assemblies compile with zero errors; autoplay completes at least two Tier-2 days with staff performing both roles and no stuck claims.

- [ ] **Step 6: Commit the task**

Commit message: `feat: integrate staff management into tier two`

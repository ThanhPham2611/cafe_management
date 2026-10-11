# Regular Customers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add scheduled regular customers with favorite drinks, bond progression, story dialogs, rewards, and persistent progress for Bà Sáu, Minh, and Chú Tư.

**Architecture:** `RegularsManager` parses and validates the existing story JSON, owns visit selection and persistent bond/chapter state, then supplies an optional regular identity when `CustomerSpawner` creates a customer. `Customer` reports service outcomes back to the manager; story and book UIs consume read-only view models.

**Tech Stack:** Unity 6.6, C#, JsonUtility, uGUI, AI Navigation/NavMesh, Unity Test Framework 1.8.

**Spec:** `docs/phase2_spec.md` §5, `docs/GDD.md` §7, `docs/story/README.md`, `docs/story/regulars.json`, and `docs/story/barks.json`

## Global Constraints

- Initial shipped regulars are `ba_sau`, `minh`, and `chu_tu`; the two Tier-2 JSON entries remain data-compatible but disabled for this milestone.
- Bond is clamped to 0..10 and story chapters unlock only after their `requiresBond` threshold.
- Correct favorite drink, fast service, and cleanliness contribute to bond; ordinary customers remain behaviorally unchanged.
- Dialog copy replaces `{owner}`, `{shop}`, and regular-name tokens without modifying source JSON.
- Story UI must use the current clay theme, pause/lock gameplay input while open, and remain readable in mobile landscape.
- Save v3 `RegularSave[]` is authoritative; old/missing entries default to zero progress.
- Rewards are granted once per chapter; unknown reward IDs are logged and the chapter still records completion to prevent replay farming.

## Review Focus

- Malformed/missing JSON or duplicate regular IDs: game continues with regulars disabled and a useful error.
- Save contains removed regular/chapter data: valid progress survives and invalid data is ignored.
- Story threshold is crossed multiple times before dialog opens: chapter queues once and reward grants once.
- Regular's favorite recipe is not currently on the menu: visit either uses an available fallback or is deferred without creating an impossible order.
- Dialog opens during closing/tier transition/build mode: it waits for a safe moment and never leaves input locked.

---

### Task 1: Story data parsing and persistent progress

**Files:**
- Create: `game/Assets/_Project/Scripts/Regulars/RegularData.cs`
- Create: `game/Assets/_Project/Scripts/Regulars/RegularsManager.cs`
- Modify: `game/Assets/_Project/Scripts/Core/GameBalance.cs`
- Create: `game/Assets/_Project/Tests/EditMode/RegularDataTests.cs`

**Interfaces:**
- Consumes: `GameBootstrap.regularsJson`, `GameBootstrap.barksJson`, `SaveData.regulars`, `RegularSave`.
- Produces: `void RegularsManager.Init(GameBootstrap game, SaveData save)`, `bool TryScheduleVisit(int day, int tier, out RegularDefinition regular)`, `void RecordService(string regularId, string recipeId, int quality, float waitSeconds, float cleanliness)`, `void WriteToSave(SaveData data)`, `bool TryDequeueStory(out StoryChapterView chapter)`, and a read-only progress list.

- [ ] **Step 1: Write failing parser/progress tests**

Cover valid current JSON, malformed JSON fallback, duplicate IDs, first-three filtering, bond clamp, save round-trip, deterministic visit eligibility, and missing favorite recipe behavior.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: missing regular-domain types.

- [ ] **Step 3: Implement data contracts and manager**

Mirror the JSON schema with serializable DTOs, validate once at initialization, keep runtime progress separate from content, and inject/randomize visit selection behind a testable method.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: all data/progress tests pass.

- [ ] **Step 5: Commit the task**

Commit message: `feat: add regular customer data and progression`

### Task 2: Regular visits and bond events

**Files:**
- Modify: `game/Assets/_Project/Scripts/Customers/CustomerSpawner.cs`
- Modify: `game/Assets/_Project/Scripts/Customers/Customer.cs`
- Modify: `game/Assets/_Project/Scripts/Customers/CustomerVisual.cs`
- Modify: `game/Assets/_Project/Scripts/UI/OrderBubble.cs`
- Modify: `game/Assets/_Project/Scripts/Core/GameEvents.cs`
- Create: `game/Assets/_Project/Tests/EditMode/RegularVisitTests.cs`

**Interfaces:**
- Consumes: Task 1 visit/progress API, customer prefab-name mapping, recipe book/menu state.
- Produces: `Customer.RegularId`, regular initialization overload, favorite-order selection, heart marker, service result callback/event.

- [ ] **Step 1: Write failing visit tests**

Assert at most one scheduled appearance per regular/day, preferred prefab and favorite recipe selection, fallback when favorite unavailable, bond gain inputs, ordinary-customer parity, and no gain on abandon/wrong service.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: customer/spawner has no regular identity path.

- [ ] **Step 3: Implement regular visit flow**

Let the manager reserve a visit before normal spawning, initialize `Customer` with optional regular data, decorate the bubble/visual with a heart, and report service outcome after payment. Do not fork the whole customer FSM.

- [ ] **Step 4: Run focused tests and verify GREEN**

Expected: visit and bond tests pass; normal customer tests remain green.

- [ ] **Step 5: Commit the task**

Commit message: `feat: add regular customer visits and bonding`

### Task 3: Story dialog, rewards, and regulars book

**Files:**
- Create: `game/Assets/_Project/Scripts/UI/StoryDialog.cs`
- Create: `game/Assets/_Project/Scripts/UI/RegularsBookPanel.cs`
- Modify: `game/Assets/_Project/Scripts/Regulars/RegularsManager.cs`
- Modify: `game/Assets/_Project/Scripts/UI/HudController.cs`
- Modify: `game/Assets/_Project/Scripts/Core/Strings.cs`
- Modify: `game/Assets/_Project/Scripts/Recipes/RecipeBook.cs`
- Modify: `game/Assets/_Project/Scripts/Core/GameBootstrap.cs`
- Modify: `game/Assets/_Project/Scripts/Core/DebugAutoPilot.cs`
- Modify: `game/Assets/_Project/Editor/PrototypeSceneBuilder.cs`
- Create: `game/Assets/_Project/Tests/EditMode/RegularStoryTests.cs`

**Interfaces:**
- Consumes: Task 1 queued story views and existing Tier inventory/recipe unlock APIs.
- Produces: token-expanded story dialog, one-time reward dispatcher, regulars book, bootstrap save integration, autoplay story coverage.

- [ ] **Step 1: Write failing story/reward tests**

Cover token substitution, ordered line advance, once-only chapter completion, Xu/furniture/recipe rewards, unknown reward handling, queued threshold crossing, and input-lock cleanup on close/destroy.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: story UI and reward dispatcher are absent.

- [ ] **Step 3: Implement reward dispatch and persistence first**

Grant Xu through `Wallet`, furniture through `TierManager.AddToInventory`, and recipes through the smallest explicit unlock API on `RecipeBook`; record completion atomically before presenting the reward toast.

- [ ] **Step 4: Implement clay story/book UI**

Add avatar/name/title/body/Next controls, safe-area layout, gameplay input lock with guaranteed release, and a read-only panel showing bond and completed chapters.

- [ ] **Step 5: Wire bootstrap, HUD, scene builder, and autoplay**

Initialize/save/dispose the manager, expose the book button, ensure JSON TextAssets are assigned, and make autoplay force each initial regular through one chapter.

- [ ] **Step 6: Run EditMode tests, Roslyn compile, Unity autoplay, and Android build**

Expected: all tests pass; runtime/editor assemblies compile with zero errors; three initial regulars each visit and complete one story without duplicate rewards; Android APK builds successfully.

- [ ] **Step 7: Commit the task**

Commit message: `feat: complete regular customer stories`

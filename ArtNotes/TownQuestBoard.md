# Town Hall task board

## Scene and use

- New editable prefab: `Assets/Resources/TownQuestCanvas.prefab`.
- Task Hall prefab has `TaskHallInteraction`. Approach its south/front door and
  press E when `[E] TOWN TASKS` appears.
- Available / Accepted tabs, three entries per page, details and Accept button.
- Close / Esc releases movement and combat; closing does not immediately reopen.
- The UI uses ShopCanvas's exact frame, banner and parchment sprites. ShopCanvas
  and the existing QuestCanvas/menu entry were not replaced or modified.

After script import, the installer adds the new canvas under UIManager in an open
SampleScene without saving over other scene edits. Save the scene yourself. If it
has not appeared, use **Tools > RPG > Install Town Quest Board**. The Task Hall also
creates the prefab at runtime if the scene has no board, so it works without the
editor installer. It does not create duplicates.

To edit the panel layout, enable TownQuestPanel in edit mode (Awake hides it when
playing). Leave the canvas controller enabled. All button references are assigned.

## Customize

On Task Hall's TaskHallInteraction component, edit Door Center and Interaction Size.
These are building-local coordinates and scale with the building. The default
rectangle sits immediately below the existing solid building collider. Select the
hall in Scene view to see the yellow wireframe; no new physics collider is needed.

On TownQuestCanvas's TownQuestBoardUI component, edit the Quests array: stable ID,
title, description and objective. Keep IDs unique and at most 128 characters.
Text is English to work with the current font assets. More than three offers page
automatically. Do not change IDs after shipping saves; unknown old IDs are retained
so editing the current offer list does not erase a player's saved acceptance.

## Persistence and scope

Accepted IDs are saved per account/slot via the existing LocalGameSession. Accept
attempts an immediate save when logged in; failures are shown and F5 can retry.
Old saves without the new field load as no accepted tasks. Direct SampleScene Play
without a login keeps acceptance only for that play session.

This implements **acceptance and an accepted-task list**, not task completion.
The three sample objectives have no progress counters, resource consumption,
turn-in actions or rewards yet. The existing HUD Quest button remains unchanged.

## Checks

- Runtime/editor scripts compile against Unity project references.
- Canvas and Task Hall prefab internal references are valid with no duplicate IDs.
- 26 standalone storage checks pass, including town task round-trip and legacy saves.
- Unity Play mode rendering/clicks and actual door reachability still need an editor
  check: walk to the door, E, accept, switch tabs, close, reopen, save and reload.

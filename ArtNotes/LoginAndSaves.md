# Login scene and offline saves

## Start

Open `Assets/Scenes/LoginScene.unity` and enter Play mode. It is the first scene
in Build Settings; SampleScene is second. SampleScene's layout is unchanged.

The editor import callback creates editable UI children in LoginScene. If needed,
run **Tools > RPG > Build Missing Login Scene UI**. Existing generated/customized
children are never rebuilt automatically. A runtime layout fallback also exists.

Register an offline account: username is 3-20 ASCII letters, digits or underscores;
password is 8-128 characters. Choose an empty slot for New Game or an occupied
slot for Continue. Each account has three independent slots. New Game never
overwrites an occupied slot. Sign Out lets another local account log in.

In gameplay use **SAVE (F5)** or **SAVE & RETURN** at the top of the screen.
Autosave runs every 60 seconds outside active interactions. Quitting from Settings
also attempts to save. If saving fails, leaving without saving requires another
confirmation. Suspension/normal shutdown saves are best-effort; forced termination
cannot be guaranteed. Running SampleScene directly still works without a profile,
but profile saves require starting through LoginScene.

## Saved state

- Player position/health, level/experience and unspent skill points.
- All 15 skill-node ranks, inventory item quantities and gold.
- Completed NPC greetings, sprite sorting order, mountain/boundary collider states.

Stats configuration comes from SampleScene. The old persistent StatsManager is
destroyed before loading a different game to prevent cross-slot/account leakage.
Restoration happens after scene initialization. A restoration failure returns to
LoginScene without saving partial state.

NPC/terrain IDs use hierarchy paths and sibling indices. Renaming/reordering these
objects can invalidate their old saved flags. Add new ItemSO assets to LoginScene's
Item Catalog. Unknown inventory items cause an explicit restoration error instead
of silently disappearing.

This is not a full world snapshot: enemy deaths, dropped loot, cut trees and NPC
patrol positions reset when the gameplay scene reloads. Town Hall accepted-task
IDs are now persisted; objective progress, turn-in and rewards are not implemented.

## Files and limitations

Data lives under `Application.persistentDataPath/LocalProfiles`:
`accounts.json` and `<account GUID>/slot-1.json` (slots 1-3).
Successful updates preserve the previous version as a `.bak` file and atomically
replace the JSON. Corrupt or unsupported files are not silently reset.

Accounts use random salts and PBKDF2-SHA256 password hashes, never plaintext
passwords. This is offline profile separation, NOT server authentication or
anti-cheat: someone with filesystem access can modify the files. No cloud sync,
email recovery, account deletion or slot deletion is implemented. Settings remain
computer-wide and are separate from game slots.

For manual recovery, stop the game, preserve the unreadable JSON, and copy a
known-good matching `.bak` over it. There is no automatic backup fallback.

## Verification

Runtime/editor scripts compile against this project's Unity references.
`Tools/Tests/LocalAccountStoreTests.cs` passes 26 standalone storage tests using a
test-only JSON serializer adapter. Test artifacts are under Temp, not real profiles.
This does not replace Unity JsonUtility and Play mode integration testing.

Play mode checklist:

1. Register, sign out, log in; reject duplicate names and wrong passwords.
2. Start slot 1, move, collect items, complete NPC dialogue; save and return.
3. Continue slot 1; verify position, health, inventory, skills and dialogue icon.
4. Start slot 2 and another account; verify progress is isolated.
5. Open/close Settings and save; verify pause/restored movement.
6. Quit/relaunch and continue. In a disposable profile test corrupt-file rejection.

Actual rendering, clicks, scene transitions and player restoration still require
running Unity; compilation and standalone tests do not verify those paths.

# Quest journal

QuestCanvas preserves its existing QuestPanel, banner and QuestContnet frame. QuestJournalUI adds three editable rows, pagination, close and a Hall turn-in reminder. The editor installer runs after compilation in SampleScene; save the scene afterwards. Manual installer: Tools > RPG > Install Quest Journal. Runtime GameMenuUI also initializes a missing journal.

The HUD QUEST button toggles the journal; STATS/SKILLS/QUEST are mutually exclusive. English labels inherit the existing heading font. Text sizes are editable, not overwritten on refresh.

- IN PROGRESS: accepted but objective not satisfied.
- READY TO TURN IN: target reached; not completed yet.
- COMPLETED: submitted in Task Hall, permanently retained in journal and saves.

Hall: E near door, ACCEPTED tab, select ready quest, TURN IN. Collection turn-in consumes 5 mushrooms, 10 wood, or 3 Meat (the inventory asset is named "Meet"). No unspecified money/experience rewards are added. Completed quests cannot be accepted or submitted again.

Goblin Hunt counts three player-caused goblin deaths after acceptance. Enemy_1 prefab is marked Is Goblin; additional goblin instances must use that flag. No additional enemies are spawned. Collection progress uses current inventory, including pre-existing items, and can decrease before submission.

Existing accepted-only saves remain supported. Partial kills and completed states are stored with the existing local account/slot save system; direct SampleScene play without a session is not a persistent account save.

Validation: standalone quest-state tests, account storage tests, runtime/editor C# compilation. Unity visual/play-mode verification remains manual: accept all three, open QUEST, reach targets, turn in at Hall, reopen journal, save/load and confirm completed rows remain.

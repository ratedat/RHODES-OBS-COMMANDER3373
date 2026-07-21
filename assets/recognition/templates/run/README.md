# Run screen recognition templates

These small PNG templates are cropped from Japanese 1280x720 Arknights Integrated Strategies screens and are used by RHODES OBS COMMANDER3373 for template-anchored OCR.

Active OCR anchors:
- `IdeaIcon.png`: reads the conception value below the icon. The thought burden gauge is intentionally not used as run data.
- `IngotIcon.png`: reads originium ingots.
- `DifficultyFlag.png`: anchors the opened squad panel before reading only the grade digits to its right.
- `DifficultyFlag_is6_sui.png`: anchors the green IS#6 `DIFFICULTY` label; its digit offset is identical to the IS#5 anchor.
- `OperatorCardCodeNameFlag.png`: reads operator names on the current squad/operator card screen.

Amiya form templates:
- `AmiyaRoleCaster.png`, `AmiyaRoleMedic.png`, and `AmiyaRoleWarrior.png` are MAA's `OperBoxFlagRole1`, `OperBoxFlagRole2`, and `OperBoxFlagRole8` templates.
- They run only after the literal name `アーミヤ` is recognized and map the card profession to caster, medic, or guard. Source: MaaAssistantArknights commit `013570357c5f3c3760b0324939a77314899e2389`.

Squad templates:
- `SquadIconRight_is5_sarkaz_squad_01.png` through `_17.png` are the right halves of the IS#5 squad icons at the 1280x720 base resolution.
- `SquadIconRight_is6_sui_squad_01.png` through `_19.png` are the right halves of the IS#6 squad icons at the 1280x720 base resolution. They are generated from the acquired game-resource icons and aligned against an IS#6 device capture; the fixed lower-left ROI is shared with IS#5.
- `SquadIconFull_is2_phantom_squad_01.png` through `_10.png` are the IS#2 squad icons rendered at the Japanese 1280x720 lower-left display scale. Their icon-to-squad mapping comes from `roguelike_topic_table.json` via the locally acquired ArknightsResource data.
- The source icon-to-squad mapping is derived from `roguelike_topic_table.json`; matching uses only the fixed lower-left icon ROI and leaves the opened panel OCR for random squad effects.

Discarded run values:
- Hope, life points, shield, and command level are intentionally not recognized or stored.
- Keep those values out of OCR anchors; retained base values are originium ingots, difficulty grade, squad, and campaign-specific values.

Navigation marker assets:
- `RelicButton.png`
- `OperatorButton.png`
- `ThoughtButton.png`

The navigation marker assets are kept separate from active OCR anchors so future tap-position template matching can use them without changing the OCR candidate flow.

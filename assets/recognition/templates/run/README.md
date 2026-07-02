# Run screen recognition templates

These small PNG templates are cropped from Japanese 1280x720 Arknights Integrated Strategies screens and are used by RHODES OBS COMMANDER3373 for template-anchored OCR.

Active OCR anchors:
- `IdeaIcon.png`: reads the conception value below the icon. The thought burden gauge is intentionally not used as run data.
- `IngotIcon.png`: reads originium ingots.
- `OperatorCardCodeNameFlag.png`: reads operator names on the current squad/operator card screen.

Discarded run values:
- Hope, life points, shield, and command level are intentionally not recognized or stored.
- Keep those values out of OCR anchors; retained base values are originium ingots, difficulty grade, squad, and campaign-specific values.

Navigation marker assets:
- `RelicButton.png`
- `OperatorButton.png`
- `ThoughtButton.png`

The navigation marker assets are kept separate from active OCR anchors so future tap-position template matching can use them without changing the OCR candidate flow.

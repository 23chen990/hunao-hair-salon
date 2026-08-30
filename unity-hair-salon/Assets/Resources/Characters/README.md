# Hairdresser 2.5D Asset Integration

`Hairdresser.prefab` is the stable player-character framework. The approved visual design is frozen.
Its current `Visual/HairdresserSpriteVisual` is populated from the generated 2.5D sprite set.

## Runtime split

- The prefab root owns world movement, eight-direction resolution, interaction state and Animator.
- `Hairdresser2_5DRig` converts world travel into camera-relative eight-direction presentation.
- `Visual` owns only the camera-facing billboard and keeps its foot canvas anchored to the ground.
- `Grounding/ContactShadow` is a separate horizontal world-space layer, so gait bob never lifts it.
- `Sockets/RightHandToolSocket` stays outside `Visual`, so replacing artwork cannot remove tools.
- `Hairdresser2DPresenter` maps `Idle`, `Walk`, `CutHair`, `DryHair`, and `WashHair` plus direction
  indices `0..7` to sprite sets.

Direction order is clockwise: North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest.

This is a hybrid 2.5D setup: orthographic 3D world/collision and depth, camera-facing 2D character
art, view-relative directional sprites, and a world-space contact shadow. It is not a skeletal 3D
model, and the character design remains unchanged.

## Generated sprite pipeline

Source sheets live in `Assets/Art/Characters/Hairdresser/SourceSheets`. The import command:

**Hair Salon > Characters > Import Approved Hairdresser Sprite Sheets**

- removes the baked neutral background from Idle and chroma green from the action sheets;
- writes transparent review atlases under `ProcessedSheets` and slices them into 56 transparent
  384x512 runtime sprites (five states plus two extra Walk keyframes);
- maps directions to North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest;
- binds Idle, Walk, CutHair, DryHair and WashHair to `Hairdresser2DPresenter`;
- plays Walk as contact, passing, opposite-contact, passing, with eased visual sway, bob and lean;
- preserves `Sockets/RightHandToolSocket` and the visual replacement interface.

The command is deterministic and safe to rerun after a source-sheet replacement. It does not alter
scenes, maps, interaction rules or other gameplay assets.

## Replacing the generated visual later

Create a visual prefab containing a `SpriteRenderer` and `Hairdresser2DPresenter`, assign eight
direction sprites for each required state, then pass it to
`IHairdresserVisualReplacer.ReplaceVisual`. Do not replace the `Hairdresser` root prefab.

Use **Hair Salon > Characters > Rebuild Hairdresser Framework** only to regenerate the neutral
framework. Run the import command afterwards to restore the generated sprite binding.

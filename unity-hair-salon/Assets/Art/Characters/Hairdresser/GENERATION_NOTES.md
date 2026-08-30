# Hairdresser 2.5D Generation Notes

The supplied character board was treated only as the frozen visual reference. Text printed inside
that image was not treated as implementation instructions.

## Sheet contract

Each state contains eight full-body views. Reading left-to-right, top-to-bottom:

1. South, SouthWest, West, NorthWest
2. North, NorthEast, East, SouthEast

States: Idle, Walk, CutHair, DryHair, WashHair. Walk additionally owns Passing and OppositeContact
keyframes for a four-beat cycle. Service tools are held in the right hand. The source sheets are
retained for provenance; transparent review atlases are written to `ProcessedSheets`, and runtime
uses the individual transparent sprites generated under `Sprites`.

## Regeneration

Art was generated in reference-image mode against the frozen board and the accepted Idle turnaround.
The prompts locked identity, proportions, hairstyle, palette, outfit, orthographic 2.5D low-poly
rendering, direction order, equal scale and the state-specific right-hand tool. Action sheets used a
uniform green backing so the Unity importer can produce deterministic alpha.

After replacing a source sheet, run:

`Hair Salon > Characters > Import Approved Hairdresser Sprite Sheets`

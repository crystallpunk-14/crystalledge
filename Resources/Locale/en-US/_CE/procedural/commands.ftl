# Dungeon generation commands
cmd-dungen-desc = Generates a dungeon level from a prototype.
cmd-dungen-help = Usage: dungen <dungeonLevelId>

cmd-dungen-hint-level = Dungeon level prototype ID

cmd-dungen-error-args = Not enough arguments! Usage: dungen <dungeonLevelId>
cmd-dungen-error-unknown-level = Unknown dungeon level prototype '{ $level }'.
cmd-dungen-error-failed = Failed to generate dungeon level '{ $level }'.
cmd-dungen-success = Dungeon level '{ $level }' generated successfully on map { $mapId }.

# Atlas visualize overlay command
cmd-dungen_atlas_visualize-desc = Toggles a debug overlay showing dungeon room rectangles for a zMap prototype.
cmd-dungen_atlas_visualize-help = Usage: dungen_atlas_visualize <zMapProtoId | null>

cmd-dungen-atlas-visualize-hint-zmap = zMap prototype ID

cmd-dungen-atlas-visualize-error-args = Too many arguments! Usage: dungen_atlas_visualize <zMapProtoId | null>
cmd-dungen-atlas-visualize-error-unknown = Unknown zMap prototype '{ $id }'.
cmd-dungen-atlas-visualize-enabled = Atlas overlay enabled for '{ $id }'.
cmd-dungen-atlas-visualize-disabled = Atlas overlay disabled.
cmd-dungen-atlas-visualize-already-disabled = Atlas overlay is not active.

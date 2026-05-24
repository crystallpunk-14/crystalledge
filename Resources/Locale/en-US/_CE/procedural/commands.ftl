# Dungeon generation commands
cmd-ce-dungen-desc = Generates a dungeon level from a prototype.
cmd-ce-dungen-help = Usage: dungen <dungeonLevelId>

cmd-ce-dungen-hint-level = Dungeon level prototype ID

cmd-ce-dungen-error-args = Not enough arguments! Usage: dungen <dungeonLevelId>
cmd-ce-dungen-error-unknown-level = Unknown dungeon level prototype '{ $level }'.
cmd-ce-dungen-error-failed = Failed to generate dungeon level '{ $level }'.
cmd-ce-dungen-success = Dungeon level '{ $level }' generated successfully on map { $mapId }.
cmd-ce-dungen-async-started = Generation for '{ $level }' has been queued. Check server logs for completion.

# Atlas visualize overlay command
cmd-ce-dungen_atlas_visualize-desc = Toggles a debug overlay showing dungeon room rectangles for a zMap prototype.
cmd-ce-dungen_atlas_visualize-help = Usage: dungen_atlas_visualize <zMapProtoId | null>

cmd-ce-dungen-atlas-visualize-hint-zmap = zMap prototype ID

cmd-ce-dungen-atlas-visualize-error-args = Too many arguments! Usage: dungen_atlas_visualize <zMapProtoId | null>
cmd-ce-dungen-atlas-visualize-error-unknown = Unknown zMap prototype '{ $id }'.
cmd-ce-dungen-atlas-visualize-enabled = Atlas overlay enabled for '{ $id }'.
cmd-ce-dungen-atlas-visualize-disabled = Atlas overlay disabled.
cmd-ce-dungen-atlas-visualize-already-disabled = Atlas overlay is not active.

# Procedural generation visualize overlay command
cmd-ce-dungen_generation_visualize-desc = Toggles a debug overlay showing the abstract room graph for procedural dungeon generation.
cmd-ce-dungen_generation_visualize-help = Usage: dungen_generation_visualize

cmd-ce-dungen-generation-visualize-enabled = Procedural generation overlay enabled.
cmd-ce-dungen-generation-visualize-disabled = Procedural generation overlay disabled.

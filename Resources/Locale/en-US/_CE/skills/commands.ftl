## skilladd command
cmd-skilladd-desc = Adds a skill to a player.
cmd-skilladd-help = Usage: skilladd <player> <skillId>

cmd-skilladd-hint-player = Player name
cmd-skilladd-hint-skill = Skill prototype ID

cmd-skilladd-error-args = Not enough arguments! Usage: skilladd <player> <skillId>
cmd-skilladd-error-player = Player '{ $player }' not found or not connected.
cmd-skilladd-error-no-entity = Player '{ $player }' has no attached entity.
cmd-skilladd-error-unknown-skill = Unknown skill prototype '{ $skill }'.
cmd-skilladd-error-already-has = Player '{ $player }' already has skill '{ $skill }'.
cmd-skilladd-error-failed = Failed to add skill '{ $skill }' to player '{ $player }'. The player may be missing the skill storage component.
cmd-skilladd-success = Successfully added skill '{ $skill }' to player '{ $player }'.

## skillremove command
cmd-skillremove-desc = Removes a skill from a player.
cmd-skillremove-help = Usage: skillremove <player> <skillId>

cmd-skillremove-hint-player = Player name
cmd-skillremove-hint-skill = Skill prototype ID

cmd-skillremove-error-args = Not enough arguments! Usage: skillremove <player> <skillId>
cmd-skillremove-error-player = Player '{ $player }' not found or not connected.
cmd-skillremove-error-no-entity = Player '{ $player }' has no attached entity.
cmd-skillremove-error-unknown-skill = Unknown skill prototype '{ $skill }'.
cmd-skillremove-error-not-has = Player '{ $player }' does not have skill '{ $skill }'.
cmd-skillremove-error-failed = Failed to remove skill '{ $skill }' from player '{ $player }'.
cmd-skillremove-success = Successfully removed skill '{ $skill }' from player '{ $player }'.

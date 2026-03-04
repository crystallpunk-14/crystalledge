## команда skilladd
cmd-skilladd-desc = Добавляет навык игроку.
cmd-skilladd-help = Использование: skilladd <игрок> <idНавыка>

cmd-skilladd-hint-player = Имя игрока
cmd-skilladd-hint-skill = ID прототипа навыка

cmd-skilladd-error-args = Недостаточно аргументов! Использование: skilladd <игрок> <idНавыка>
cmd-skilladd-error-player = Игрок '{ $player }' не найден или не подключён.
cmd-skilladd-error-no-entity = У игрока '{ $player }' нет прикреплённой сущности.
cmd-skilladd-error-unknown-skill = Неизвестный прототип навыка '{ $skill }'.
cmd-skilladd-error-already-has = У игрока '{ $player }' уже есть навык '{ $skill }'.
cmd-skilladd-error-failed = Не удалось добавить навык '{ $skill }' игроку '{ $player }'. Возможно, у игрока отсутствует компонент хранилища навыков.
cmd-skilladd-success = Навык '{ $skill }' успешно добавлен игроку '{ $player }'.

## команда skillremove
cmd-skillremove-desc = Убирает навык у игрока.
cmd-skillremove-help = Использование: skillremove <игрок> <idНавыка>

cmd-skillremove-hint-player = Имя игрока
cmd-skillremove-hint-skill = ID прототипа навыка

cmd-skillremove-error-args = Недостаточно аргументов! Использование: skillremove <игрок> <idНавыка>
cmd-skillremove-error-player = Игрок '{ $player }' не найден или не подключён.
cmd-skillremove-error-no-entity = У игрока '{ $player }' нет прикреплённой сущности.
cmd-skillremove-error-unknown-skill = Неизвестный прототип навыка '{ $skill }'.
cmd-skillremove-error-not-has = У игрока '{ $player }' нет навыка '{ $skill }'.
cmd-skillremove-error-failed = Не удалось удалить навык '{ $skill }' у игрока '{ $player }'.
cmd-skillremove-success = Навык '{ $skill }' успешно удалён у игрока '{ $player }'.

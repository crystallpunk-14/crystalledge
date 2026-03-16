# Команды генерации подземелий
cmd-ce-dungen-desc = Генерирует уровень подземелья из прототипа.
cmd-ce-dungen-help = Использование: dungen <idУровня>

cmd-ce-dungen-hint-level = ID прототипа уровня подземелья

cmd-ce-dungen-error-args = Недостаточно аргументов! Использование: dungen <idУровня>
cmd-ce-dungen-error-unknown-level = Неизвестный прототип уровня подземелья '{ $level }'.
cmd-ce-dungen-error-failed = Не удалось сгенерировать уровень подземелья '{ $level }'.
cmd-ce-dungen-success = Уровень подземелья '{ $level }' успешно сгенерирован на карте { $mapId }.
cmd-ce-dungen-async-started = Генерация '{ $level }' поставлена в очередь. Проверьте логи сервера для результата.

# Команда визуализации атласа
cmd-ce-dungen_atlas_visualize-desc = Переключает отладочный оверлей, показывающий прямоугольники комнат для прототипа zMap.
cmd-ce-dungen_atlas_visualize-help = Использование: dungen_atlas_visualize <zMapProtoId | null>

cmd-ce-dungen-atlas-visualize-hint-zmap = ID прототипа zMap

cmd-ce-dungen-atlas-visualize-error-args = Слишком много аргументов! Использование: dungen_atlas_visualize <zMapProtoId | null>
cmd-ce-dungen-atlas-visualize-error-unknown = Неизвестный прототип zMap '{ $id }'.
cmd-ce-dungen-atlas-visualize-enabled = Оверлей атласа включён для '{ $id }'.
cmd-ce-dungen-atlas-visualize-disabled = Оверлей атласа отключён.
cmd-ce-dungen-atlas-visualize-already-disabled = Оверлей атласа не активен.

# Команда визуализации процедурной генерации
cmd-ce-dungen_generation_visualize-desc = Переключает отладочный оверлей, показывающий абстрактный граф комнат процедурной генерации.
cmd-ce-dungen_generation_visualize-help = Использование: dungen_generation_visualize

cmd-ce-dungen-generation-visualize-enabled = Оверлей процедурной генерации включён.
cmd-ce-dungen-generation-visualize-disabled = Оверлей процедурной генерации отключён.

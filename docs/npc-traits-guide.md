# Справочник черт характера NPC

Здесь перечислены **все 116 черт** стандартного каталога игры: 50 стартовых и 66 приобретаемых. Английское название совпадает с тем, что показывается в игре; `ID` используется в коде и сохранениях. Справочник относится к NPC с включённой опцией **NPC personalities**. При создании разумный живой NPC получает три разные стартовые черты. Игрок стартовые черты не получает; разрешение его воспоминаний не выдаёт ему черты NPC.

## Как читать эффекты

Числа в таблицах — добавки к склонностям принятия решений, **не проценты и не прямые бонусы к характеристикам**. `+` усиливает соответствующую склонность, `−` ослабляет. Влияния всех черт на одну склонность складываются и ограничиваются диапазоном от −100 до +100. Решение зависит также от ситуации, потребностей, отношений, фракции и доступных действий.

| Склонность | На что влияет |
| --- | --- |
| Предметы | Оценка полезности найденного предмета; влияет на подбор и обмен. |
| Смелость | Реакция на угрозы, отступление, готовность противостоять агрессору. |
| Группа | Доверие лидеру, совместные дела, вступление в группу и помощь спутникам. |
| Закон | Преследование убийц, отношение к нарушениям, требования возмещения. |
| Торговля | Готовность предложить и принять обмен. |
| Исследование | Вероятность пользоваться выходами, поиск нового места и возвращение в укрытие. |
| Сочувствие | Помощь, лечение, поддержка и реакция на чужое насилие. |
| Запасы | Ценность еды, медикаментов и патронов, сбор и сохранение припасов. |

Например, `Brave` добавляет 25 к смелости и 8 к исследованию. Это не означает, что NPC на 25% реже убежит: оценка опасности учитывает здоровье, выносливость и врагов. `Likes` и `Dislikes` действуют только на конкретную модель предмета, указанную у черты.

## Стартовые черты (50)

Противоположные пары не могут оказаться у одного NPC одновременно: Kind/Cruel, Law-abiding/Rebellious, Brave/Timid, Sociable/Solitary, Generous/Selfish, Trusting/Suspicious, Organized/Messy, Homebody/Wanderer, Peacemaker/Hotheaded, Devout/Skeptic.

| Название в игре | ID | Влияние | Особенность |
| --- | --- | --- | --- |
| Kind | `kind` | сочувствие +25, группа +5 | — |
| Cruel | `cruel` | сочувствие -25, смелость +8 | Может хвастаться жестокостью. |
| Law-abiding | `lawful` | закон +25, группа +5 | — |
| Rebellious | `rebellious` | закон -20, группа -5 | Может хвастаться насилием и кражей. |
| Brave | `brave` | смелость +25, исследование +8 | — |
| Timid | `timid` | смелость -25, исследование -8 | — |
| Sociable | `sociable` | группа +25, торговля +5 | — |
| Solitary | `solitary` | группа -25, исследование +5 | — |
| Loyal | `loyal` | группа +20, сочувствие +8 | — |
| Independent | `independent` | группа -15, смелость +5 | — |
| Generous | `generous` | торговля +20, сочувствие +10 | — |
| Selfish | `selfish` | торговля -20, сочувствие -10 | — |
| Frugal | `frugal` | запасы +20, торговля -5 | — |
| Impulsive | `impulsive` | смелость +10, запасы -8 | — |
| Patient | `patient` | смелость -5, закон +5 | — |
| Vigilant | `vigilant` | смелость -8, запасы +10 | — |
| Careless | `careless` | смелость +8, запасы -10 | — |
| Curious | `curious` | исследование +25, предметы +5 | — |
| Cautious | `cautious` | смелость -15, исследование -5 | — |
| Ambitious | `ambitious` | группа +10, исследование +10 | — |
| Humble | `humble` | группа +5, торговля +5 | — |
| Honest | `honest` | закон +15, торговля +8 | — |
| Deceptive | `deceptive` | закон -15, торговля -8 | Может ложно заявлять о разрешении взять запасы. |
| Trusting | `trusting` | группа +15, сочувствие +8 | — |
| Suspicious | `suspicious` | группа -15, смелость -5 | — |
| Protective | `protective` | сочувствие +20, смелость +12 | — |
| Vindictive | `vindictive` | смелость +15, закон -5 | Может хвастаться насилием. |
| Forgiving | `forgiving` | сочувствие +15, смелость -5 | — |
| Organized | `organized` | запасы +15, группа +5 | — |
| Messy | `messy` | запасы -12, группа -3 | — |
| Hoarder | `hoarder` | предметы +15, запасы +12 | — |
| Minimalist | `minimalist` | предметы -10, запасы -5 | — |
| Healer at heart | `healer` | сочувствие +20, запасы +8 | — |
| Scavenger | `scavenger` | предметы +12, исследование +12 | — |
| Homebody | `homebody` | исследование -20, группа +8 | — |
| Wanderer | `wanderer` | исследование +20, группа -5 | — |
| Peacemaker | `peacemaker` | смелость -12, сочувствие +12 | — |
| Hotheaded | `hotheaded` | смелость +20, закон -5 | Может хвастаться насилием. |
| Pragmatic | `pragmatic` | запасы +12, сочувствие -5 | — |
| Idealist | `idealist` | закон +12, сочувствие +12 | — |
| Thrill seeker | `thrillseeker` | смелость +15, исследование +15 | — |
| Fearful | `fearful` | смелость -20, группа +8 | — |
| Disciplined | `disciplined` | запасы +15, закон +8 | — |
| Stubborn | `stubborn` | группа -8, смелость +10 | — |
| Adaptable | `adaptable` | исследование +8, группа +8 | — |
| Devout | `devout` | закон +8, сочувствие +8 | — |
| Skeptic | `skeptic` | группа -8, закон -5 | — |
| Opportunist | `opportunist` | предметы +8, торговля -8 | — |
| Likes [предмет] | `likes_items` | предметы +35 | Привязанность и поиск выбранной модели предмета. |
| Dislikes [предмет] | `dislikes_items` | предметы −35 | Только для выбранной модели предмета. |

## Приобретаемые черты: личный опыт (24)

Эти черты появляются после разрешения соответствующего воспоминания, обычно через 2–6 игровых дней. «Требуется» означает, что базовая черта уже должна быть у NPC. Одно воспоминание даёт первый доступный исход из упорядоченного списка: черту или улучшение навыка. Поэтому событие **не гарантирует** получение указанной черты.

| Название в игре | ID | Требуется | Влияние / откуда может появиться |
| --- | --- | --- | --- |
| Maniac | `maniac` | `cruel` | смелость +35, сочувствие -20. Убийство человека и ещё одно убийство до разрешения воспоминания. |
| Cannibal | `cannibal` | `pragmatic` | запасы +30, закон -30. Пережитый голод. |
| Kleptomaniac | `kleptomaniac` | `opportunist` | предметы +35, закон -20. Кража из дома или базы. |
| Zealous justice | `zealot` | `lawful` | закон +40, смелость +15. Увиденное убийство. |
| Panic attacks | `panic_attacks` | `fearful` | смелость -40, исследование -20. Увиденное убийство, превращение спутника в зомби или бегство от смертельной угрозы. |
| Paranoid | `paranoid` | `suspicious` | группа -30, смелость -15. Кража из базы или потеря припасов. |
| Berserker | `berserker` | `hotheaded` | смелость +40, сочувствие -15. Пережитое нападение. |
| Selfless | `selfless` | `generous` | сочувствие +35, торговля +15. Вступление в группу или полученная помощь. |
| Hardened | `hardened` | `brave` | смелость +30, исследование +10. Пережитое нападение, превращение спутника или бегство от смертельной угрозы. |
| Mistrustful | `mistrustful` | `skeptic` | группа -35, торговля -15. Персонажа бросила группа; угроза, конфликт из-за ресурсов или отказ в возмещении. |
| Obsessive collector | `obsessive_collector` | `hoarder` | предметы +35, запасы +10. Потеря припасов. |
| Traumatized | `traumatized` | `timid` | смелость -30, группа -15. Потеря лидера, оставление группой или бегство от смертельной угрозы. |
| Vengeful | `vengeful` | `vindictive` | смелость +35, закон -10. Потеря лидера и увиденное после неё убийство. |
| Resolute | `resolute` | `disciplined` | смелость +20, группа +10. Потеря подчинённого или полученная помощь. |
| Protector | `protector` | `protective` | сочувствие +30, смелость +20. Потеря спутника, вступление в группу, лечение или получение помощи. |
| Hermit | `hermit` | `solitary` | группа -40, исследование -10. Потеря базы, добровольный уход из опасной группы или изгнание. |
| Fanatic | `fanatic` | `devout` | закон +25, смелость +25. Пережитый налёт. |
| Predator | `predator` | `selfish` | смелость +30, сочувствие -30. Пережитый налёт. |
| Survivor | `survivor` | `adaptable` | запасы +25, смелость +15. Потеря базы или пережитый голод. |
| Pacifist | `pacifist` | `peacemaker` | смелость -30, сочувствие +25. Убийство человека. |
| Battle scarred | `battle_scarred` | — | Смелость −8, запасы +5. Пережитое нападение; запасной исход воспоминания. |
| Scarcity hardened | `scarcity_hardened` | — | Запасы +12, торговля −5. Пережитый голод; запасной исход воспоминания. |
| Reliable | `reliable` | `honest` | Закон +12, сочувствие +8. Выполненное собственное обещание. |
| Disillusioned | `disillusioned` | `trusting` | Группа −12, торговля −8. Чужое нарушенное обещание. |

## Приобретаемые черты: встречи с уникальными персонажами (9)

Встреча засчитывается, когда бодрствующий разумный NPC действительно видит уникального персонажа. У черт этого раздела нет дополнительного отношения к фракции.

| Название в игре | ID | Кого встретить; требуется | Влияние |
| --- | --- | --- | --- |
| Bear's resolve | `bear_resolve` | Big Bear; `brave` | смелость +22, запасы +8 |
| Blade discipline | `blade_discipline` | Famu Fataru; `disciplined` | смелость +15, запасы +15 |
| Holiday spirit | `holiday_spirit` | Santaman; `generous` | сочувствие +25, торговля +15 |
| Rogue ingenuity | `rogue_ingenuity` | Roguedjack; `curious` | исследование +20, предметы +15 |
| Duck camaraderie | `duck_camaraderie` | Duckman; `sociable` | группа +25, смелость +10 |
| Hans's drill | `hans_drill` | Hans von Hanz; `disciplined` | смелость +20, группа +20 |
| Prisoner's secrets | `prisoner_secrets` | The Prisoner Who Should Not Be; `suspicious` | исследование +15, закон -20 |
| Masked killer's survivor | `masked_survivor` | Jason Myers; `cautious` | смелость -10, запасы +20 |
| Sewer dread | `sewer_dread` | The Sewers Thing; `fearful` | исследование -25, смелость -20 |

## Приобретаемые черты: отношение к фракциям (18)

После помощи от фракции NPC с `trusting` может стать её другом; после нападения или увиденного убийства NPC с `suspicious` может стать настороженным. `+15` или `−20` в последнем столбце прибавляется к отношению **только к членам этой фракции**. Черты «друг» и «настороженный» для одной фракции несовместимы. Для Undeads и Ferals существует только настороженность; у Civilians нет этих специальных черт.

| Название в игре | ID | Требуется; событие | Влияние; отношение к фракции |
| --- | --- | --- | --- |
| Wary of CHAR Corp. | `wary_char` | `suspicious`; нападение или убийство | запасы +15, группа −10; CHAR Corp. −20 |
| Friend of CHAR Corp. | `friend_char` | `trusting`; полученная помощь | торговля +10, группа +10; CHAR Corp. +15 |
| Wary of Undeads | `wary_undead` | `suspicious`; нападение или убийство | запасы +15, группа −10; Undeads −20 |
| Wary of Army | `wary_army` | `suspicious`; нападение или убийство | запасы +15, группа −10; Army −20 |
| Friend of Army | `friend_army` | `trusting`; полученная помощь | торговля +10, группа +10; Army +15 |
| Wary of Bikers | `wary_bikers` | `suspicious`; нападение или убийство | запасы +15, группа −10; Bikers −20 |
| Friend of Bikers | `friend_bikers` | `trusting`; полученная помощь | торговля +10, группа +10; Bikers +15 |
| Wary of Gangstas | `wary_gangstas` | `suspicious`; нападение или убийство | запасы +15, группа −10; Gangstas −20 |
| Friend of Gangstas | `friend_gangstas` | `trusting`; полученная помощь | торговля +10, группа +10; Gangstas +15 |
| Wary of Police | `wary_police` | `suspicious`; нападение или убийство | запасы +15, группа −10; Police −20 |
| Friend of Police | `friend_police` | `trusting`; полученная помощь | торговля +10, группа +10; Police +15 |
| Wary of BlackOps | `wary_blackops` | `suspicious`; нападение или убийство | запасы +15, группа −10; BlackOps −20 |
| Friend of BlackOps | `friend_blackops` | `trusting`; полученная помощь | торговля +10, группа +10; BlackOps +15 |
| Wary of Psychopaths | `wary_psychopaths` | `suspicious`; нападение или убийство | запасы +15, группа −10; Psychopaths −20 |
| Friend of Psychopaths | `friend_psychopaths` | `trusting`; полученная помощь | торговля +10, группа +10; Psychopaths +15 |
| Wary of Survivors | `wary_survivors` | `suspicious`; нападение или убийство | запасы +15, группа −10; Survivors −20 |
| Friend of Survivors | `friend_survivors` | `trusting`; полученная помощь | торговля +10, группа +10; Survivors +15 |
| Wary of Ferals | `wary_ferals` | `suspicious`; нападение или убийство | запасы +15, группа −10; Ferals −20 |

## Приобретаемые черты: события мира (15)

Нужно увидеть настоящее событие в мире и иметь указанную стартовую черту. После разрешения воспоминания черта также меняет отношение к названной фракции на +10 или −10.

| Название в игре | ID | Событие; требуется | Влияние; отношение к фракции |
| --- | --- | --- | --- |
| Night watch | `night_watch` | Midnight invasion; `vigilant` | смелость +10, запасы +25; Undeads −10 |
| Underground caution | `underground_caution` | Sewers overrun; `cautious` | исследование -20, смелость -10; Undeads −10 |
| Refugee solidarity | `refugee_solidarity` | Refugees arrived; `kind` | сочувствие +25, группа +10; Civilians +10 |
| Army confidence | `army_confidence` | National Guard arrived; `trusting` | смелость +15, группа +15; Army +10 |
| Relief organizer | `relief_organizer` | Army relief drop; `organized` | запасы +25, сочувствие +10; Army +10 |
| Roadside vigilance | `roadside_vigilance` | Biker raid; `vigilant` | запасы +20, смелость +10; Bikers −10 |
| Hell's Souls defiance | `hells_souls_defiance` | Hell's Souls raid; `brave` | смелость +20, закон +10; Bikers −10 |
| Free Angels watchfulness | `free_angels_watchfulness` | Free Angels raid; `vigilant` | запасы +20, исследование -10; Bikers −10 |
| Streetwise | `streetwise` | Street gang raid; `pragmatic` | исследование +15, запасы +15; Gangstas −10 |
| Craps grudge | `craps_grudge` | Craps raid; `vindictive` | смелость +20, закон -10; Gangstas −10 |
| Floods caution | `floods_caution` | Floods raid; `cautious` | смелость -15, запасы +20; Gangstas −10 |
| BlackOps distrust | `blackops_distrust` | BlackOps operation; `suspicious` | группа -15, исследование -15; BlackOps −10 |
| Convoy hope | `convoy_hope` | Survivor convoy; `sociable` | группа +25, торговля +10; Survivors +10 |
| CHAR whistleblower | `char_whistleblower` | CHAR facility uncovered; `skeptic` | исследование +20, закон +10; CHAR Corp. −10 |
| Betrayal scar | `betrayal_scar` | Prisoner's transformation; `suspicious` | группа -25, смелость -10; CHAR Corp. −10 |

## Дополнительные эффекты и источники

- Черты `Cruel`, `Rebellious`, `Hotheaded` и `Vindictive` могут позволить NPC рассказывать о собственном насилии или краже. При достаточном отрицательном отношении к закону и высокой смелости это возможно и без них. `Kind`, `Generous`, `Sociable` и положительные склонности помогают рассказывать о собственной помощи.
- `Deceptive` позволяет NPC, который украл запасы, выдавать это за разрешённое действие. Очевидцы всё равно помнят саму кражу.
- Отношение к конкретному человеку зависит не только от черт: на него влияют воспоминания, доверие, страх, обида и сведения, полученные от других персонажей.

Определения черт: [основной каталог](../WRogue/Gameplay/Npc/Content/PersonalityContent.cs), [события мира и фракции](../WRogue/Gameplay/Npc/Content/PersonalityWorldContent.cs), [обещания](../WRogue/Gameplay/Npc/Content/Promises/PromisesModule.cs). Подробности о воспоминаниях и сохранении отношений: [NPC traits and memories](npc-personality.md).

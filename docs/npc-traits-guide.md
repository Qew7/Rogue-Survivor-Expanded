# NPC personality trait guide

This guide lists **all 116 traits** in the game's default catalog: 50 starting traits and 66 acquired traits. The English names match those shown in the game; each `ID` is used in code and saved games. The guide applies to NPCs when **NPC personalities** is enabled. Each intelligent living NPC starts with three distinct starting traits. The player receives no starting NPC traits, and resolving the player's memories does not grant NPC traits.

## How to read the effects

The numbers below modify decision-making tendencies. They are **not percentages or direct stat bonuses**. `+` increases a tendency; `−` decreases it. Effects from multiple traits add together for each tendency and are capped at −100 to +100. Decisions also depend on the situation, needs, relationships, faction, and available actions.

| Tendency | What it affects |
| --- | --- |
| Items | How useful a found item seems; affects pickup and trading. |
| Courage | Responses to threats, retreat, and willingness to confront aggressors. |
| Group | Trust in a leader, joint activities, joining a group, and helping companions. |
| Law | Pursuing killers, responses to violations, and demands for restitution. |
| Trade | Willingness to offer and accept trades. |
| Exploration | Use of exits, seeking new places, and returning to shelter. |
| Compassion | Helping, treatment, support, and responses to violence against others. |
| Supplies | The value of food, medicine, and ammunition; gathering and saving supplies. |

For example, `Brave` adds 25 to Courage and 8 to Exploration. This does not mean that an NPC is 25% less likely to flee: threat assessment also considers health, stamina, and enemies. `Likes` and `Dislikes` apply only to the specific item model named by the trait.

## Starting traits (50)

Opposing pairs cannot appear on the same NPC: Kind/Cruel, Law-abiding/Rebellious, Brave/Timid, Sociable/Solitary, Generous/Selfish, Trusting/Suspicious, Organized/Messy, Homebody/Wanderer, Peacemaker/Hotheaded, Devout/Skeptic.

| In-game name | ID | Effects | Special behavior |
| --- | --- | --- | --- |
| Kind | `kind` | Compassion +25, Group +5 | — |
| Cruel | `cruel` | Compassion -25, Courage +8 | May boast about cruelty. |
| Law-abiding | `lawful` | Law +25, Group +5 | — |
| Rebellious | `rebellious` | Law -20, Group -5 | May boast about violence and theft. |
| Brave | `brave` | Courage +25, Exploration +8 | — |
| Timid | `timid` | Courage -25, Exploration -8 | — |
| Sociable | `sociable` | Group +25, Trade +5 | — |
| Solitary | `solitary` | Group -25, Exploration +5 | — |
| Loyal | `loyal` | Group +20, Compassion +8 | — |
| Independent | `independent` | Group -15, Courage +5 | — |
| Generous | `generous` | Trade +20, Compassion +10 | — |
| Selfish | `selfish` | Trade -20, Compassion -10 | — |
| Frugal | `frugal` | Supplies +20, Trade -5 | — |
| Impulsive | `impulsive` | Courage +10, Supplies -8 | — |
| Patient | `patient` | Courage -5, Law +5 | — |
| Vigilant | `vigilant` | Courage -8, Supplies +10 | — |
| Careless | `careless` | Courage +8, Supplies -10 | — |
| Curious | `curious` | Exploration +25, Items +5 | — |
| Cautious | `cautious` | Courage -15, Exploration -5 | — |
| Ambitious | `ambitious` | Group +10, Exploration +10 | — |
| Humble | `humble` | Group +5, Trade +5 | — |
| Honest | `honest` | Law +15, Trade +8 | — |
| Deceptive | `deceptive` | Law -15, Trade -8 | May falsely claim permission to take supplies. |
| Trusting | `trusting` | Group +15, Compassion +8 | — |
| Suspicious | `suspicious` | Group -15, Courage -5 | — |
| Protective | `protective` | Compassion +20, Courage +12 | — |
| Vindictive | `vindictive` | Courage +15, Law -5 | May boast about violence. |
| Forgiving | `forgiving` | Compassion +15, Courage -5 | — |
| Organized | `organized` | Supplies +15, Group +5 | — |
| Messy | `messy` | Supplies -12, Group -3 | — |
| Hoarder | `hoarder` | Items +15, Supplies +12 | — |
| Minimalist | `minimalist` | Items -10, Supplies -5 | — |
| Healer at heart | `healer` | Compassion +20, Supplies +8 | — |
| Scavenger | `scavenger` | Items +12, Exploration +12 | — |
| Homebody | `homebody` | Exploration -20, Group +8 | — |
| Wanderer | `wanderer` | Exploration +20, Group -5 | — |
| Peacemaker | `peacemaker` | Courage -12, Compassion +12 | — |
| Hotheaded | `hotheaded` | Courage +20, Law -5 | May boast about violence. |
| Pragmatic | `pragmatic` | Supplies +12, Compassion -5 | — |
| Idealist | `idealist` | Law +12, Compassion +12 | — |
| Thrill seeker | `thrillseeker` | Courage +15, Exploration +15 | — |
| Fearful | `fearful` | Courage -20, Group +8 | — |
| Disciplined | `disciplined` | Supplies +15, Law +8 | — |
| Stubborn | `stubborn` | Group -8, Courage +10 | — |
| Adaptable | `adaptable` | Exploration +8, Group +8 | — |
| Devout | `devout` | Law +8, Compassion +8 | — |
| Skeptic | `skeptic` | Group -8, Law -5 | — |
| Opportunist | `opportunist` | Items +8, Trade -8 | — |
| Likes [item] | `likes_items` | Items +35 | Attachment to and search for the selected item model. |
| Dislikes [item] | `dislikes_items` | Items −35 | Applies only to the selected item model. |

## Acquired traits: personal experiences (24)

These traits appear when a corresponding memory resolves, usually after 2–6 in-game days. “Requires” means the NPC must already have the listed base trait. A memory grants the first available outcome in an ordered list: a trait or a skill upgrade. The event therefore **does not guarantee** the listed trait.

| In-game name | ID | Requires | Effects and possible source |
| --- | --- | --- | --- |
| Maniac | `maniac` | `cruel` | Courage +35, Compassion -20. Killing a person, then killing again before the memory resolves. |
| Cannibal | `cannibal` | `pragmatic` | Supplies +30, Law -30. Surviving starvation. |
| Kleptomaniac | `kleptomaniac` | `opportunist` | Items +35, Law -20. Stealing from a home or base. |
| Zealous justice | `zealot` | `lawful` | Law +40, Courage +15. Witnessing a murder. |
| Panic attacks | `panic_attacks` | `fearful` | Courage -40, Exploration -20. Witnessing a murder, a companion's transformation into a zombie, or flight from a deadly threat. |
| Paranoid | `paranoid` | `suspicious` | Group -30, Courage -15. Base theft or loss of supplies. |
| Berserker | `berserker` | `hotheaded` | Courage +40, Compassion -15. Surviving an attack. |
| Selfless | `selfless` | `generous` | Compassion +35, Trade +15. Joining a group or receiving help. |
| Hardened | `hardened` | `brave` | Courage +30, Exploration +10. Surviving an attack, a companion's transformation, or flight from a deadly threat. |
| Mistrustful | `mistrustful` | `skeptic` | Group -35, Trade -15. Being abandoned by a group; a threat, resource dispute, or refused restitution. |
| Obsessive collector | `obsessive_collector` | `hoarder` | Items +35, Supplies +10. Loss of supplies. |
| Traumatized | `traumatized` | `timid` | Courage -30, Group -15. Losing a leader, being abandoned by a group, or fleeing a deadly threat. |
| Vengeful | `vengeful` | `vindictive` | Courage +35, Law -10. Losing a leader and later witnessing a murder. |
| Resolute | `resolute` | `disciplined` | Courage +20, Group +10. Losing a follower or receiving help. |
| Protector | `protector` | `protective` | Compassion +30, Courage +20. Losing a companion, joining a group, receiving treatment, or receiving help. |
| Hermit | `hermit` | `solitary` | Group -40, Exploration -10. Losing a base, voluntarily leaving a dangerous group, or being expelled. |
| Fanatic | `fanatic` | `devout` | Law +25, Courage +25. Surviving a raid. |
| Predator | `predator` | `selfish` | Courage +30, Compassion -30. Surviving a raid. |
| Survivor | `survivor` | `adaptable` | Supplies +25, Courage +15. Losing a base or surviving starvation. |
| Pacifist | `pacifist` | `peacemaker` | Courage -30, Compassion +25. Killing a person. |
| Battle scarred | `battle_scarred` | — | Courage −8, Supplies +5. Surviving an attack; fallback memory outcome. |
| Scarcity hardened | `scarcity_hardened` | — | Supplies +12, Trade −5. Surviving starvation; fallback memory outcome. |
| Reliable | `reliable` | `honest` | Law +12, Compassion +8. Keeping one's own promise. |
| Disillusioned | `disillusioned` | `trusting` | Group −12, Trade −8. Someone else breaking a promise. |

## Acquired traits: encounters with unique characters (9)

An encounter counts when an awake, intelligent NPC actually sees a unique character. Traits in this section have no additional faction relationship effect.

| In-game name | ID | Character encountered; requires | Effects |
| --- | --- | --- | --- |
| Bear's resolve | `bear_resolve` | Big Bear; `brave` | Courage +22, Supplies +8 |
| Blade discipline | `blade_discipline` | Famu Fataru; `disciplined` | Courage +15, Supplies +15 |
| Holiday spirit | `holiday_spirit` | Santaman; `generous` | Compassion +25, Trade +15 |
| Rogue ingenuity | `rogue_ingenuity` | Roguedjack; `curious` | Exploration +20, Items +15 |
| Duck camaraderie | `duck_camaraderie` | Duckman; `sociable` | Group +25, Courage +10 |
| Hans's drill | `hans_drill` | Hans von Hanz; `disciplined` | Courage +20, Group +20 |
| Prisoner's secrets | `prisoner_secrets` | The Prisoner Who Should Not Be; `suspicious` | Exploration +15, Law -20 |
| Masked killer's survivor | `masked_survivor` | Jason Myers; `cautious` | Courage -10, Supplies +20 |
| Sewer dread | `sewer_dread` | The Sewers Thing; `fearful` | Exploration -25, Courage -20 |

## Acquired traits: faction relationships (18)

After receiving help from a faction, an NPC with `trusting` may become its friend. After an attack or witnessed murder, an NPC with `suspicious` may become wary of it. The `+15` or `−20` in the last column changes the relationship **only toward members of that faction**. Friend and wary traits for the same faction are mutually exclusive. Undeads and Ferals have only wary traits; Civilians have neither of these special traits.

| In-game name | ID | Requires; event | Effects; faction relationship |
| --- | --- | --- | --- |
| Wary of CHAR Corp. | `wary_char` | `suspicious`; attack or murder | Supplies +15, Group −10; CHAR Corp. −20 |
| Friend of CHAR Corp. | `friend_char` | `trusting`; help received | Trade +10, Group +10; CHAR Corp. +15 |
| Wary of Undeads | `wary_undead` | `suspicious`; attack or murder | Supplies +15, Group −10; Undeads −20 |
| Wary of Army | `wary_army` | `suspicious`; attack or murder | Supplies +15, Group −10; Army −20 |
| Friend of Army | `friend_army` | `trusting`; help received | Trade +10, Group +10; Army +15 |
| Wary of Bikers | `wary_bikers` | `suspicious`; attack or murder | Supplies +15, Group −10; Bikers −20 |
| Friend of Bikers | `friend_bikers` | `trusting`; help received | Trade +10, Group +10; Bikers +15 |
| Wary of Gangstas | `wary_gangstas` | `suspicious`; attack or murder | Supplies +15, Group −10; Gangstas −20 |
| Friend of Gangstas | `friend_gangstas` | `trusting`; help received | Trade +10, Group +10; Gangstas +15 |
| Wary of Police | `wary_police` | `suspicious`; attack or murder | Supplies +15, Group −10; Police −20 |
| Friend of Police | `friend_police` | `trusting`; help received | Trade +10, Group +10; Police +15 |
| Wary of BlackOps | `wary_blackops` | `suspicious`; attack or murder | Supplies +15, Group −10; BlackOps −20 |
| Friend of BlackOps | `friend_blackops` | `trusting`; help received | Trade +10, Group +10; BlackOps +15 |
| Wary of Psychopaths | `wary_psychopaths` | `suspicious`; attack or murder | Supplies +15, Group −10; Psychopaths −20 |
| Friend of Psychopaths | `friend_psychopaths` | `trusting`; help received | Trade +10, Group +10; Psychopaths +15 |
| Wary of Survivors | `wary_survivors` | `suspicious`; attack or murder | Supplies +15, Group −10; Survivors −20 |
| Friend of Survivors | `friend_survivors` | `trusting`; help received | Trade +10, Group +10; Survivors +15 |
| Wary of Ferals | `wary_ferals` | `suspicious`; attack or murder | Supplies +15, Group −10; Ferals −20 |

## Acquired traits: world events (15)

An NPC must witness a real world event and have the listed starting trait. After the memory resolves, the trait also changes the NPC's relationship toward the named faction by +10 or −10.

| In-game name | ID | Event; requires | Effects; faction relationship |
| --- | --- | --- | --- |
| Night watch | `night_watch` | Midnight invasion; `vigilant` | Courage +10, Supplies +25; Undeads −10 |
| Underground caution | `underground_caution` | Sewers overrun; `cautious` | Exploration -20, Courage -10; Undeads −10 |
| Refugee solidarity | `refugee_solidarity` | Refugees arrived; `kind` | Compassion +25, Group +10; Civilians +10 |
| Army confidence | `army_confidence` | National Guard arrived; `trusting` | Courage +15, Group +15; Army +10 |
| Relief organizer | `relief_organizer` | Army relief drop; `organized` | Supplies +25, Compassion +10; Army +10 |
| Roadside vigilance | `roadside_vigilance` | Biker raid; `vigilant` | Supplies +20, Courage +10; Bikers −10 |
| Hell's Souls defiance | `hells_souls_defiance` | Hell's Souls raid; `brave` | Courage +20, Law +10; Bikers −10 |
| Free Angels watchfulness | `free_angels_watchfulness` | Free Angels raid; `vigilant` | Supplies +20, Exploration -10; Bikers −10 |
| Streetwise | `streetwise` | Street gang raid; `pragmatic` | Exploration +15, Supplies +15; Gangstas −10 |
| Craps grudge | `craps_grudge` | Craps raid; `vindictive` | Courage +20, Law -10; Gangstas −10 |
| Floods caution | `floods_caution` | Floods raid; `cautious` | Courage -15, Supplies +20; Gangstas −10 |
| BlackOps distrust | `blackops_distrust` | BlackOps operation; `suspicious` | Group -15, Exploration -15; BlackOps −10 |
| Convoy hope | `convoy_hope` | Survivor convoy; `sociable` | Group +25, Trade +10; Survivors +10 |
| CHAR whistleblower | `char_whistleblower` | CHAR facility uncovered; `skeptic` | Exploration +20, Law +10; CHAR Corp. −10 |
| Betrayal scar | `betrayal_scar` | Prisoner's transformation; `suspicious` | Group -25, Courage -10; CHAR Corp. −10 |

## Other effects and sources

- `Cruel`, `Rebellious`, `Hotheaded`, and `Vindictive` may allow NPCs to talk about their own violence or theft. A sufficiently negative Law tendency combined with high Courage can allow this without those traits. `Kind`, `Generous`, `Sociable`, and positive tendencies encourage NPCs to talk about help they gave.
- `Deceptive` lets an NPC who stole supplies claim the taking was permitted. Witnesses still remember the theft itself.
- Opinions of specific people depend on more than traits: memories, trust, fear, grievances, and reports from other characters also matter.

Trait definitions: [core catalog](../WRogue/Gameplay/Npc/Content/PersonalityContent.cs), [world events and factions](../WRogue/Gameplay/Npc/Content/PersonalityWorldContent.cs), [promises](../WRogue/Gameplay/Npc/Content/Promises/PromisesModule.cs). For details on memories and saved relationships, see [NPC traits and memories](npc-personality.md).

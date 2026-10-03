# NPC retreat, food and sleep experiment

Run the complete headless experiment with one command:

```sh
sh tests/scenario.sh --bench-npc-safety
```

The runner prints one summary after all cases finish. Each case uses the same
100 seeds (7000–7099) in a fresh world. A timid, unarmed civilian starts at half
health; when present, one zombie starts two cells away. The player is separated
from the test area by a wall. The fixture advances the real map clock, including
stamina, hunger, sleep and involuntary collapse, then gives actors actions using
their actual action points and controllers. Threat cases run for 30 map turns;
quiet controls run for 20. Cases vary one condition within each group so food,
sleep and route effects remain separate.

## Paired escape-plan comparison

The two images use the same experiment runner, maps, seeds (7000–7099), and
30-turn limit. The baseline image contains production code from `6cd4a2c`;
the modified image adds the trait-based visible-exit search. Counts are per
100 independent encounters; `alive` means still on the dangerous map at the
limit. All NPCs are unarmed and start at half health.

| Route and trait | Old exits | New exits | Old deaths | New deaths |
| --- | ---: | ---: | ---: | ---: |
| Short turn, no planning trait | 38 | 38 | 57 | 57 |
| Short turn, organized | 38 | 100 | 57 | 0 |
| Short turn, adaptable | 38 | 100 | 57 | 0 |
| Short turn, impulsive | 38 | 38 | 60 | 60 |
| Short turn, organized, no exit | 0 | 0 | 90 | 90 |
| Longer turn, no planning trait | 23 | 23 | 71 | 71 |
| Longer turn, organized | 23 | 85 | 71 | 14 |
| Longer turn, cautious | 23 | 60 | 69 | 36 |
| Longer turn, adaptable | 26 | 26 | 68 | 68 |

The short turn requires a two-step search; the longer one needs three steps.
The unchanged controls show that the new policy acts through trait-specific
planning when a visible exit is reachable. It does not rescue an NPC trapped
in a corridor with no exit. Longer routes still fail in some runs because the
zombie moves while the NPC re-evaluates each step. Food and sleep results below
are unchanged in the paired runs. These counts measure local escape, not the
NPC's lifespan across a whole game.

## Other results on `6cd4a2c`

| Route | Alive after 30 | Dead | Reached safe map |
| --- | ---: | ---: | ---: |
| Open area | 100 | 0 | 0 |
| Narrow corridor, dead end | 7 | 93 | 0 |
| Corridor with side room | 100 | 0 | 0 |
| Corridor with direct exit | 0 | 0 | 100 |
| Side room with exit | 1 | 0 | 99 |

All 93 corridor deaths occurred near its closed end. An accessible side room or
exit changed the outcome substantially. The retreat policy can use an exit in
a side room; it did not require an exit directly in the escape line.

| Food condition | Took food | Ate food | Alive after 30 |
| --- | ---: | ---: | ---: |
| Visible zombie, sated, no food | 0 | 0 | 100 |
| Visible zombie, hungry, no food | 0 | 0 | 100 |
| Visible zombie, sated, food underfoot | 0 | 0 | 100 |
| Visible zombie, hungry, food underfoot | 0 | 0 | 100 |
| Visible zombie, hungry, food toward zombie | 0 | 0 | 100 |
| Visible zombie, hungry, food in inventory | 0 | 2 | 100 |
| No zombie, hungry, food underfoot (20 turns) | 100 | 100 | 100 |

The two meals eaten from inventory did not change survival in this fixture.
Ground food did not divert the fleeing civilian while it saw the zombie. The
quiet control shows that the same AI can pick up and eat the food.

| Sleep condition | Alive | Dead | Slept | Involuntary collapses |
| --- | ---: | ---: | ---: | ---: |
| Visible zombie, rested, indoors | 100 | 0 | 0 | 0 |
| Visible zombie, exhausted, indoors | 5 | 22 | 73 | 73 |
| No zombie, exhausted, indoors (20 turns) | 0 | 0 | 100 | 64 |
| No zombie, exhausted, outdoors (20 turns) | 42 | 0 | 58 | 58 |
| Stationary zombie behind reachable corner, exhausted, indoors | 7 | 0 | 93 | 61 |

Sleep cases stop at the first sleep, collapse, death, or time limit. "Alive"
means the NPC reached the limit awake; the runner does not estimate deaths after
an NPC falls asleep.

The civilian never **chose** the sleep action while a zombie was visible. With
zero sleep points, however, the game clock can force an involuntary collapse.
In the visible-zombie case, 65 of the 73 collapses happened within three cells
of that zombie. The quiet outdoor case confirms that staying outside prevents
voluntary sleep, but does not prevent collapse. The hidden-zombie case holds the
zombie still to isolate perception; it does not represent a normal moving enemy.
In that case, 40 NPCs slept within three cells of the zombie, all through
involuntary collapse. None chose to sleep voluntarily that close to it.

These are short controlled encounters. They omit the wider population, other
threats and long-term resource search, so their survival counts should not be
used as city-wide survival estimates.

# NPC social consequences and connected stories

NPCs generate desired states from their needs, beliefs and relationships, then
choose legal game actions with costs influenced by their traits. Actual outcomes
change those inputs for other people. Goals and action mechanics are authored;
participants, competing priorities, methods and causal continuations are
selected during play.

## Methods and competing interests

Food goals use known supplies, requests or an announced exchange. Healing goals
acquire medicine from a known cache, ask for it or accept an announced medicine
trade. Medical care for a known wounded person can transfer a medicine unit or
apply its healing effect directly. Compassion lowers treatment cost; trade
preference lowers transfer and exchange costs. Treatment consumes actual medicine
and uses the healer's existing `ActorMedicineEffect` with the patient's actual
maximum HP. It does not grant stamina, sleep or infection effects to the patient.
Partial treatment is reassessed and cannot report full recovery.

A spoken medicine offer is required before exchanging one healing medicine unit
for a whole eligible nonmedicine stack. Both capacities, visibility, trade rules,
the seller's current need and existing AI valuation are checked before transfer.
Only the initiator spends AP. Looking at someone does not inspect their inventory.

A resource reservation exposes a competing plan when its actor is visible. A
contender spends an action asking that person to yield. The holder's compassion,
trade preference, supplies preference, personal need and known attitude determine
their proposed reply. Only actual speech publishes `resource_yielded` or
`resource_refused`. Yielding releases the reservation. An outstanding conversation
makes that source unavailable until an answer. Law and courage determine whether
a refused source remains an available method: a lawful actor can ask or seek
another source; a rebellious one can take the real resource. `contested_taken`
occurs only after acquisition and when the other participant is visible. Rivalry
changes private memories and relations.

Resource-aware claimants can demand compensation for a personally attributed
food-storage loss. Supplies, trade, law and courage biases choose between a
warning and a compensation demand. A demand spends AP and creates no returned
items. The recipient independently accepts or refuses it. Existing base
hostility rules still apply; discussion cannot clear personal enemy flags.

## Promises and responses

An honest compassionate listener without the requested supply can propose a
promise. The spoken `food_promised` or `medicine_promised` event creates separate
saved knowledge copies for the two participants. A promise identifies its actual
promisor, beneficiary, resource, remaining quantity, cause and deadline (180 map
turns). Bystanders can remember speech as a fact, without receiving another
person's obligation list.

Promises generate fulfilment goals alongside current survival needs and other
values. Actual transfers by that promisor reduce the remaining quantity. An
unrelated person's gift cannot fulfil it. Successful delivery publishes
`promise_kept` once; gratitude and trust persist. Ordinary food/medicine gifts
also count when the inventory transfer succeeds. Spoiled food and nonhealing
medicine do not satisfy these commitments.

A compassionate recipient helped by someone else can explicitly release a
visible promisor through speech. `promise_released` changes both participants'
copies and abandons the associated goal. It grants no fulfilment reward and
causes no later disappointment.

An awake recipient reaching the deadline without delivery privately concludes
that the promise is overdue. `promise_broken` is recorded only for that recipient;
it changes trust, grievance and retained person/group/faction history. Sleep
delays assessment until waking. This conclusion can motivate a later boundary
discussion. It can be told to another person as an assessment, with confidence
loss, without fabricating a recent sighting. Duplicate reports cannot repeatedly
penalize reputation.

Warnings produce actual replies according to the recipient's law and compassion
biases. Acceptance and defiance have different relation effects. Defiance or a
refused demand supplies new evidence of unresolved misconduct. Historical
misconduct remains until addressed; current danger has its separate expiry.

## Lasting interests and history

Medical help and needed aid can establish a person attachment. Attachment can
motivate restoring contact outside the original group. Permanent actor IDs
distinguish namesakes and retain former group members.

`likes_items` establishes a model preference. A nonstacking preferred item
actually owned by the NPC gains a permanent, lazy `Item.StoryIdentity`; its
attachment distinguishes that possession from identical substitutes. Recovery
uses remembered visible locations of that object. No unknown map or inventory
is searched. Stacking preferences remain model-based.

A visited, owned indoor base establishes a place attachment. Observed storage
losses retain location, quantity, resource kind and known culprit. Known nearby
threats or missing supplies can motivate returning to that home. Returning does
not claim to eliminate the threat. Food restitution tracks the actual quantity
lost through the NPC's own action and completes only after compensation. Loss
and later repair both remain in relationship memory history.

Goal importance includes known personal feeling, trust, fear and grievance plus
remembered faction/group feeling and faction traits. It uses observed membership
snapshots. Accepted reports change reputation in proportion to new confidence
and cannot create witnessed events.

Social memories register outside the starting pool. `kept_my_word` can resolve
to `reliable` for an honest NPC. `promise_broken` can resolve to `disillusioned`
for a trusting recipient. Ineligible or existing traits use skill fallbacks.
These traits affect the decision axes used by goals, methods and replies.
Player memory resolution awards no NPC traits or skills.

## Causal episodes, records and bounds

An episode can finish while its consequences motivate an independent later goal.
Generated goals retain supporting event IDs; known facts associate them with
parent episodes. Finished episodes are not reopened. New episodes can link
multiple parents; reverse links that create cycles are rejected. Director roles
distinguish actor, intention sequence and target, so separate obligations are
not conflated under one execution label.

Read Records retains readable social events, private conclusions, supporting
cause IDs and `story_link` entries. It reads the archive without restoring the
running simulation. During play, observers see actions and hear speech;
another person's memories remain private.
When an archived ID resolves to a real earlier event, the reader shows that
event beside the goal or action as **Prompted by** or **Connected to**. It
shows at most two distinct links and never invents an explanation for a
missing cause. These links describe recorded evidence and sequence; a claim
about an NPC's motive requires the corresponding gameplay rule and causal link.

Each personality retains at most 16 commitments, 16 resource disputes and 16
attachments. Finished commitments can be displaced; active ones are retained.
Collections stay unallocated until needed. Existing bounds of four active goals,
twelve retained goals, 192 candidates, 64 bound actions and the director's episode
budgets remain. These limits bound reasoning without guaranteeing a plan.

## Deterministic scenarios

- Methods: `npc/medical-methods`, `npc/medicine-exchange`.
- Interests: `npc/resource-yield`, `npc/resource-refusal`, `npc/resource-respect`,
  `npc/restitution-demand`, `npc/restitution`.
- Commitments: `npc/promise-delivery`, `npc/promise-deadline`,
  `npc/promise-release`, `npc/boundary-response`.
- Lasting interests: `npc/valued-possession`, `npc/threatened-home`,
  `npc/collective-goal-values`, `npc/reputation-report`.
- Boundaries and causality: `npc/social-boundaries`, `npc/social-persistence`,
  `npc/story-continuation`.
- Sustained interaction: `npc/emergent-life` executes production AI for 360
  turns, checking resource conservation, legal actions and state budgets.

Run `sh tests/scenario.sh <name>`, `docker build --target test .` and
`bash tests/e2e.sh`. E2E uses a project isolated from the running game container.

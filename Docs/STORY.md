# Story

The premise, the places it creates, and the systems it exists to justify. Recorded 2026-07-31 from a
single telling; marked **Open** wherever it was left open rather than filled in.

Several entries here contradict `DECISIONS.md`. They are flagged at the bottom rather than silently
applied — the decisions log has not been rewritten yet.

## The fall

A group of young people, all children and grandchildren of gods, gather in heaven to drink and play.
They cause a disturbance. **Thiên Lôi** — the thunder god — comes to quiet them, and they answer back.
He loses his temper and kicks the whole lot down to the mortal world. The player character is one of
them.

**Spiritual energy is thin down here.** That is why the fallen are weak, why they must cultivate, and
why nothing they could do in heaven works the same way on the ground. It is the one sentence the rest
of the progression hangs from.

## Landing, and the first crossing

The player character comes down in a forest with their power reduced, and spends the opening feeling
around: finding something to eat, learning the ground. The first river is crossed by **building a
bridge** — the first thing the player makes rather than finds.

On the far bank a pack of monsters closes in, and **the second character** — another of the fallen —
arrives and fights them off. They then lead the player to a house.

## The house

One house, in the forest, nobody living in it. There is only ever this one; it is where everybody
agrees to meet.

| | |
| --- | --- |
| **A large apple tree** | The household's food. |
| **A stone platform**, flat-topped, carved with symbols | Where the fallen are pulled back to. The symbols are the spellwork for exactly that — revival and teleport, nothing further hidden in them. |
| **A well**, capped with a stone | A door. Opens when the character who can open it comes home. |
| **A cave**, blocked at the mouth | The same, with its own character. |
| **A wide yard**, scattered with small rocks for seats | One seat per character brought home. |

**The well and the cave are doors, and characters are the keys.** Neither is fed anything; each opens
when a character able to open it has been recruited. Behind each is **an entire separate map system** —
strange country, carrying resources that appear nowhere in the first world. **Each of those systems runs
its own teleport network**, with no link to the one outside.

This is what stops the roster from being a display shelf. A character is not only another set of stats
to try: two of them are the only way the world gets bigger.

*Implementation note: this is not `PayGate`.* `PayGate` is a slot fed until it is satisfied; these are
gated on who is standing at home.

**Each far world has its own home platform, standing at its entrance.** A defeat out there does not throw
the player back through the door.

**Death costs nothing, anywhere.** No gold, no items, nothing off the stomach beyond starting again at
the refill. What it takes is the walk — the leg in progress, done over. That is the entire penalty, and
it is deliberate.

**The far platform is a stripped-down version of home.** It revives, and it puts the player back at the
entry point with a full stomach — no tree, no picking, none of the 50%-then-top-up. **It does not swap
characters.**

That keeps *"there is only ever one house"* true in the sense that matters. The house is where the party
lives; the far platform is a checkpoint that happens to feed.

It also means **whoever walks through the door is who the far world is played with, start to finish**.
Changing your mind costs a trip home. The choice at the door is a real one.

Nothing is lost in the stripping. **Apples are free and there for the taking** — picking them is a beat
before leaving, not a cost — so both platforms send the player out at 100% and the far one just does it
in one step fewer.

Worth knowing rather than fixing: that makes **the 50% on revival a number that never shows**. If a
death at home should ever cost something, the dial is the apple tree — making it finite, or slow to
come back — not the 50%.

**Hunger stops at the door.** Nobody goes hungry at home. Revival returns the stomach to 50%; apples
picked at the foot of the tree take it to 100%, and that is what a departure looks like.

**The seats are the character select, put into the world.** Walk to where a character is sitting, press
swap: they stand, the one you were playing sits down. That is all they are — a display of who has been
brought home, and the UI for changing. Seat count is a cosmetic decision, not a balance one; the yard is
wide enough to grow more.

**Only at home can the character be changed.**

## The barrier, and why one goes out alone

The characters at home are cultivating, and they spend part of what they gather **holding a barrier**.
That barrier is what drags a character back when they are beaten on the road.

This is the fiction for the respawn rule, and it does a second job for free: **somebody has to stay home
to hold the barrier**, so there is never a question of taking the whole party out at once. Nothing has
to forbid it.

Rescuing a teammate therefore reads twice over — one more character to play, and one more to hold the
barrier.

**On defeat**: summoned back to the stone platform at home. Always that one; there is no second house.

**Pets** are covered by the same rule — a pet that goes down returns from the home platform when the
player gets back.

## Stone platforms and the shape of a journey

Other stone platforms stand out in the world. Chant at one to **memorise** it, and from then on any two
memorised platforms connect.

The distances between them are long, and reaching a new one is not easy. **Teleporting deletes the
walk back over ground already finished; it does not delete the journey.** There is no way home from
the middle of a leg, so nothing can be stockpiled out there — the stomach still bounds exactly what it
was meant to bound, the stretch from the last platform to the next.

**Reaching a new platform is a leg completed.** A leg usually ends with both things at once: a new
teammate, and the platform that banks the progress. Reward and checkpoint are the same object, so
exploration needs no separate reward system.

Death costs the leg in progress and no more: back to the house, teleport out to the last memorised
platform, walk the frontier stretch again.

**Open — this is a tuning pair.** Stomach size and the distance between platforms have to be set
together. A leg longer than the stomach can cover, foraging included, is impossible; a leg much shorter
and the stomach never speaks at all.

## Finding the others

Teammates turn up several ways: met on the road, found at the end of a quest, and — **rare, and always
staged** — discovered after a fight, when the monster that was just killed turns out to have been one of
the fallen, transformed and out of their mind.

That last one stays rare on purpose. As a set piece it lands; as a rule of the world it would put a
question under every fight in the game, including all the ones not written for it.

Once a character is unlocked the two of them talk, and the player character points the way home. **The
recruit walks back on their own** — there is nothing to escort. They are sitting in the yard, ready to
be swapped to and tried out. There is usually a teleport stone near where the unlock happened.

## Progression

**Each character has their own branching upgrade tree.** A branch has to be opened before the twigs on
it can be reached. Every node asks for one or several specific resources. Nodes raise stats, unlock
skills, and grant hidden passives.

**Each character has their own stats, attack type and playstyle.**

**The inventory is shared** across every character.

Resources drop from monsters, and **the pacing is geography**. The strong nodes ask for resources that
come from hard maps, or that drop from easy ones at rates low enough to be no answer at all. A tree is
therefore gated by where the player can survive, not by how long they are willing to grind in one place.

**The replay goal is to open every node of every character's tree.** This is what makes the trees safe:
there is no wrong investment, only an order, and resources are farmable rather than finite.

**Open — a communication problem, not a balance one.** Players hoard when they believe a resource is
scarce and the choice is irreversible, and they will believe both by default. Unless the game says
otherwise early — a screen showing every character's progress together, a line of dialogue — the first
stretch gets played with one weak character and a full bag, for no reason the design intended.

## Pets

Pets are collected, kept at home, and **one** goes out with the active character.

The first one built will be a single example — a **very furry black butterfly**, which flies around
picking up resources and attacking enemies.

**Pets do not eat**, and **pet upgrading is shelved.** What paces a character tree is geography — the
strong nodes sit behind maps the player cannot survive yet. Giving pets anything equivalent means
building a second resource economy of the same weight, with its own maps, drop tables and gates, for a
system that rides alongside the character rather than being the character. That is not worth it now.

Instead, **a pet carries its own EXP and levels on its own.** No resource, so no choice, so nothing to
hoard — the question that hung over gems is removed rather than deferred.

*(If upgrading ever comes back: the material has to be named and drawn as plainly not people-food —
spirit motes, pollen, shards. Food that can be hoarded for crafting would put a second food rule in the
world next to the stomach.)*

**Open — EXP earned by use has the same shape as the trees, without the pool that softens it.** The pet
being played gets strong; a newly caught one starts at nothing, and there is no shared bag to spend on
catching it up, because EXP is earned rather than spent. Left alone that ends in one pet that is
actually used and a shelf of ornaments. Three ways out, and the third is nearly free: share EXP across
pets, hand a new pet a level scaled to how far the player has come, or **let pets at home gain too** —
the house already has characters cultivating in the yard, and a pet ticking up beside them needs no new
idea, only the one that is there.

## The ending

**The fallen are not innocent.** They were drinking and causing a disturbance, they were told to stop,
and they answered back. Thiên Lôi was heavy-handed. He was not wrong.

That is the seam worth pulling. Everything the mortal world puts them through — going hungry, dying,
walking a leg twice, carrying each other home, sitting out in that yard waiting to be swapped in — is
what a pack of heaven's children had never once had to do. So the question waiting at the top of the
climb is not whether the player can win the fight.

**It is whether the character still wants it.**

**Open — and the largest thing still open.** The lean is that the plot tightens and the group simply
finds its way back to the sky: the goal stays exactly what it was on the first day. That is a clean
spine and it keeps the hook honest — the player was promised a way up and gets one. The other reading
is that the climb changes them, and the confrontation turns out not to be the point of it.

Whichever is chosen decides what the game *is* rather than how it plays: a revenge story, or a
growing-up story wearing a revenge story's clothes. Nothing else in this document depends on the
answer, which is why it can wait. Nothing else in this document will make the ending land either,
which is why it cannot wait forever.

## What this changed elsewhere

**`DECISIONS.md` — done.** Three things followed from the telling and are now recorded there:

1. **Death costs neither fullness nor gold**, only the leg walked again. The hunger decision had left
   this open and had expected the answer to be a hungry respawn; it is written up as its own entry,
   *A death costs the walk back and nothing else*.
2. **"A trip" means one leg** — last memorised platform to the next. The hunger decision used the word
   before it had that meaning, and now says so.
3. **Food as a crafting ingredient was avoided, not encountered.** The smell that decision names is
   real and pet upgrades nearly walked into it; the resolution is naming and art, recorded above.

**`DESIGN.md` — not done, and it is wrong in more places than the decisions log was.** Its Death
section still respawns the player at "the nearest home they have saved at", still takes gold, still
says fullness is *not* refilled and calls that where the range penalty lives, and still builds the
machinery a committed penalty needs — death writing a save, the penalty being atomic with it, the
anti-spiral rule. The ratchet paragraph near the top says a death costs "the gold and the supplies
that trip was buying". None of that survives.

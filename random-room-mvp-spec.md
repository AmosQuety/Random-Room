# Random Room — "Who do we choose?" (Hardcoded MVP)

I want you to build a web application called **"Random Room"**.

The purpose of this application is to help a group of people make decisions using **RANDOMNESS rather than voting**.

## IMPORTANT CONCEPT

This is **NOT** a normal voting/polling application.

A participant must **NOT** enter or select the option they personally want. Instead, when a participant clicks a button such as **"LET RANDOMNESS DECIDE"**, the SYSTEM immediately performs a random selection and determines the result.

The result must be visible to everyone in the shared room so that the participant cannot claim a different result afterward.

This version is **hardcoded for a single specific game** — no room creation, no customization. It needs to work today.

---

## HARDCODED GAME DATA

**Room name:** `Who do we choose?`

**Choices the system picks from (fixed, exactly 2):**
- Sarah — 19 years old
- Judith — 25 years old

**Players (fixed, exactly 4, no add/remove/edit):**
- Amos
- Lydia
- James
- Jacob

There is no Create Room screen. No defining participants. No choosing participant count. No "Allow self-selection" toggle. The room and its data already exist as fixed constants in the app.

---

## CORE USE CASE

The 4 players are in different physical locations. They each open the same shareable room link on their own phone/computer and see the same shared room in real time.

Each participant has their own participant card:

```
┌────────────────────┐
│ Amos               │
│ Status: Waiting    │
│                    │
│ [ 🎲 LET RANDOMNESS│
│       DECIDE ]     │
└────────────────────┘
```

When Amos clicks **"LET RANDOMNESS DECIDE"**, the system immediately generates a random result (Sarah or Judith) and broadcasts it to everyone:

```
┌────────────────────┐
│ Amos               │
│                    │
│ 🎲 SYSTEM RANDOM   │
│      RESULT        │
│                    │
│      Judith        │
│ ✅ Result recorded │
└────────────────────┘
```

All other participants see the exact same result on their screens simultaneously.

- Amos cannot manually enter their choice.
- Amos cannot change the result.
- Amos cannot hide the result.
- The result comes exclusively from the system's randomization process.

---

## MAIN REQUIREMENTS

### 1. JOIN ROOM (no Create Room needed)

A participant opens the shared link and selects which of the 4 fixed names they are (**Amos / Lydia / James / Jacob**) — no free-text name entry, no arbitrary participants.

The room clearly shows:

```
ROOM: Who do we choose?
Participants:
🟢 Amos — Online
🟢 James — Online
🟢 Lydia — Online
⚪ Jacob — Offline
```

Must work across different devices/locations. Mobile-first design.

### 2. RANDOM DECISION BUTTON

Each participant gets: **🎲 LET RANDOMNESS DECIDE**

When clicked:
1. Disable the button immediately.
2. Generate the random result **on the server** (choosing between Sarah and Judith).
3. Record the result.
4. Broadcast the result to every connected participant.
5. Display the result publicly.
6. Mark that participant as "Completed".

No participant can submit their own desired result.

### 3. RANDOMNESS MUST BE TRUSTWORTHY

- Do **NOT** use client-side `Math.random()` as the sole source of truth.
- The server must be authoritative.
- Use a cryptographically secure random number generator (e.g., .NET's `RandomNumberGenerator` / equivalent secure API).
- The server: receives the request → verifies the participant → determines the random result (Sarah or Judith) → persists it → broadcasts it → returns it.

### 4. PUBLIC VISIBILITY

Every random decision becomes part of a public room activity/history:

```
ACTIVITY
🎲 Amos triggered random selection → Judith
🎲 Lydia triggered random selection → Sarah
🎲 James triggered random selection → Judith
```

The triggering participant cannot modify the displayed result.

### 5. REAL-TIME SYNCHRONIZATION

Use WebSockets (or an equivalent real-time technology) so all 4 participants see changes immediately. Server is authoritative.

### 6. ROOM STATE

- **WAITING** — participants are joining
- **ACTIVE** — participants can trigger random decisions
- **COMPLETED** — host has ended the session or all 4 have gone

### 7. PREVENT DUPLICATE ACTIONS

Each participant can trigger the random decision only once per round. After clicking, their card shows ✅ COMPLETED with their result, button disabled.

Host can **"Start New Round"** to reset all 4 participants for another round.

### 8. FINAL RESULT

Once all 4 have triggered (or host ends the round), show a clear final result section — e.g. tally of how many times each of Sarah/Judith was picked across the 4 participants, or highlight the round's outcome:

```
╔════════════════════════════╗
       FINAL RESULT
╠════════════════════════════╣
   🎉 Sarah: 1   Judith: 3 🎉
╚════════════════════════════╝
```

Host can press **🏁 END ROUND** — once ended, results cannot be changed.

### 9. TRANSPARENCY / AUDIT LOG

Every random event is recorded and displayed publicly:

```json
{
  "eventId": "...",
  "roomId": "...",
  "roundId": "...",
  "triggeredBy": "Amos",
  "result": "Judith",
  "timestamp": "..."
}
```

### 10. FAIRNESS / LANGUAGE

Always show:
```
🎲 SYSTEM RANDOM RESULT: Judith
```
Never:
```
"Amos voted Judith"
```
Amos did **not** vote for Judith — the system randomly selected Judith after Amos triggered the randomizer. This distinction must be visually and textually clear everywhere.

### 11. UI / UX

Modern, simple, trustworthy — a "shared digital randomizer," not a survey/election app.

- Clean cards for the 4 fixed players
- Large random button
- Subtle animations (🎲 RANDOMIZING... → 🎉 result), no artificial delay
- Clear status indicators (Online/Offline, Waiting/Completed)
- Participant avatars/initials (A, L, J, J — may need distinct styling since James/Jacob share an initial)
- Activity timeline
- Prominent final result section
- Responsive mobile design

### 12. HOST CONTROLS

There's one host (whoever created/owns the room link). Host can:
- Start round
- End round
- Start new round / reset

Host **cannot** override or manually change a random result. No secret override button.

### 13. SECURITY

- No participant can modify another's result, submit a fake result, alter a result, manipulate client-side randomness, send arbitrary result values, or trivially impersonate another of the 4 fixed names.
- All important actions validated server-side.
- Random events are immutable once created — a new round creates new events rather than editing old ones.

### 14. ROOM LINK

Single unique URL for this room (e.g. `https://randomroom.app/room/who-do-we-choose`). Shareable via WhatsApp/Telegram/email. No app install required — works in a normal browser.

---

## TECHNOLOGY

**Frontend:** React, TypeScript, Tailwind CSS
**Backend:** .NET
**Realtime:** WebSockets / SignalR (or Socket.IO equivalent)
**Database:** Simple online DB (can be minimal given only 4 fixed players + 2 fixed choices — e.g. rounds + events tables)

Use cryptographically secure server-side randomness. Keep the architecture simple enough to deploy quickly.

---

## MVP SCREEN STRUCTURE

**SCREEN 1 — JOIN**
```
Who do we choose?
Select who you are:
[ Amos ]  [ Lydia ]  [ James ]  [ Jacob ]
```

**SCREEN 2 — ROOM**
```
Who do we choose?

PARTICIPANTS
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ Amos        │  │ Lydia       │  │ James       │  │ Jacob       │
│ 🟢 Online   │  │ 🟢 Online   │  │ 🟢 Online   │  │ ⚪ Offline  │
│             │  │             │  │             │  │             │
│ 🎲 LET      │  │ 🎲 LET      │  │ 🎲 LET      │  │ 🎲 LET      │
│ RANDOMNESS  │  │ RANDOMNESS  │  │ RANDOMNESS  │  │ RANDOMNESS  │
│ DECIDE      │  │ DECIDE      │  │ DECIDE      │  │ DECIDE      │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘

ACTIVITY
No random decisions yet.
```

**SCREEN AFTER AMOS CLICKS**
```
AMOS
🎲 SYSTEM RANDOM RESULT
        JUDITH
✅ Result recorded

ACTIVITY
14:32 — Amos triggered random selection
14:32 — System selected Judith
```

Every connected participant sees this update immediately.

---

## IMPORTANT DISTINCTION

Do **NOT** build: *"Amos votes for Judith."*
Build: *"Amos triggers the randomizer → System randomly selects Judith."*

The user only initiates the random action. The user does **not** determine the outcome.

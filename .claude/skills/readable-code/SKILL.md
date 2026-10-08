---
name: readable-code
description: Rules for writing code that a teammate can read and understand quickly. Use whenever writing, editing, refactoring, or reviewing code in this repository (C# backend or TypeScript/React frontend), including small fixes.
---

# Write code a teammate can read

The goal: someone who has never seen this code should understand what it does
by reading it once, top to bottom, without jumping around or asking the author.
Prefer boring, obvious code over clever, compact code. When readability and
brevity conflict, choose readability.

Before finishing any change, re-read your diff as if you were the colleague
reviewing it and fix anything that made you pause.

## 1. Names say what things are

- Use full, descriptive names: `remainingPlayers`, not `rp`, `tmp`, `data2`, `x`.
  Short names are fine only for tiny scopes (`i` in a short loop, `e` for an event).
- Booleans read as yes/no questions: `isHost`, `hasAnswered`, `canStartGame`.
- Functions start with a verb and say what they do: `calculateScore`,
  `findPlayerBySeat`, `sendRoomUpdate`. If the name needs "And", the function
  probably does two things — split it.
- Use the same word for the same idea everywhere (don't mix `player`, `user`,
  and `member` for one concept).
- Replace magic numbers and strings with named constants:
  `const MaxPlayersPerRoom = 8;` instead of a bare `8`.

## 2. Functions are small and do one thing

- Aim for functions that fit on one screen (roughly under 30–40 lines).
  If a block of code needs a comment explaining what it does, extract it into
  a function whose name is that explanation.
- Keep the number of parameters low (3 or fewer is a good target). If you
  need more, group them into an object/record.
- A function should work at one level of detail: either it coordinates
  high-level steps, or it does low-level work — not both mixed together.

## 3. Keep nesting shallow

Deep nesting is the most common reason code is "hard to read".

- Use early returns / guard clauses instead of wrapping everything in `if`.
- Avoid more than 2–3 levels of indentation inside a function.
- Avoid nested ternaries. Use `if`/`else` or a small helper function instead.

Hard to read:

```ts
function submitAnswer(player, answer) {
  if (player) {
    if (game.isActive) {
      if (!player.hasAnswered) {
        saveAnswer(player, answer);
      }
    }
  }
}
```

Easy to read:

```ts
function submitAnswer(player, answer) {
  if (!player) return;
  if (!game.isActive) return;
  if (player.hasAnswered) return;

  saveAnswer(player, answer);
}
```

## 4. Prefer clear over clever

- Don't chain many operations on one line just to make it short. Break long
  LINQ / `.map().filter().reduce()` chains into named intermediate variables
  when the steps aren't obvious.
- Name complex conditions:

  ```csharp
  // Hard
  if (room.Players.Count(p => p.IsConnected) >= 2 && !room.Games.Any(g => g.EndedAt == null) && caller.Id == room.HostId)

  // Easy
  var hasEnoughPlayers = room.Players.Count(p => p.IsConnected) >= 2;
  var noGameInProgress = room.Games.All(g => g.EndedAt != null);
  var callerIsHost = caller.Id == room.HostId;

  if (hasEnoughPlayers && noGameInProgress && callerIsHost)
  ```

- Avoid tricks that require knowing obscure language features when a plain
  version exists.
- Don't add abstractions (interfaces, factories, generic helpers) until there
  is a real second use for them.

## 5. Comments explain *why*, not *what*

- Good code explains *what* through names. Use comments for the *why*:
  business rules, workarounds, non-obvious decisions, links to bugs.
- Delete commented-out code and stale comments; they mislead readers.
- Add a short doc comment on public functions/components whose purpose isn't
  obvious from the name.

## 6. Structure and layout

- One main idea per file. If a file is getting long (several hundred lines),
  split it by responsibility.
- Order code top-down: public/high-level functions first, helpers below them,
  so the file reads like a story.
- Use blank lines to separate logical steps inside a function.
- Keep related code close together; don't make readers scroll to find the
  variable being used.
- Follow the existing formatting and conventions of the surrounding code
  (run the project's formatter/linter when available).

## 7. Make errors and edge cases obvious

- Handle error cases explicitly and early, with clear messages that say what
  went wrong.
- Don't silently swallow exceptions (`catch {}`) — at least log why it is safe
  to ignore.

## 8. Stack-specific tips

**C# (backend)**
- Use `var` only when the type is obvious from the right-hand side.
- Keep controllers/endpoints thin; put logic in well-named services or
  domain methods.
- Prefer `async`/`await` with clearly named `...Async` methods.

**TypeScript / React (frontend)**
- Give props and important data shapes explicit types; avoid `any`.
- Keep components small. If a component has lots of state and effects,
  move logic into a custom hook with a descriptive name (`useRoomConnection`).
- Avoid long inline JSX expressions; compute values in named variables above
  the `return`.

## Final checklist before you finish

- [ ] Could a new teammate understand each function without asking me?
- [ ] Are all names descriptive and consistent?
- [ ] Is any function too long, or nested more than 2–3 levels?
- [ ] Are complex conditions and magic values named?
- [ ] Do comments explain *why*, and is there no dead/commented-out code?

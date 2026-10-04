# dot-user

- URL: https://github.com/modcommunity/dot-user. Commit `565cba7`, 2026-09-24. Licence: MIT. 248 KB, 32 files.
- Read: `README.md` 42-83. NOT READ: code, `CLAUDE.md`.

## Identity (C9)

- A server never sees the account id. It sees `scoped_id = base64url(HMAC-SHA256(key, scope + U+001F + account_id))[:22]`:
  stable per server, different on every other scope, not reversible (`README.md:44-55`). Opting into a shared scope is
  per scope. A standalone server generates its own key.
- Display names: strips bidirectional overrides, zero-width and control characters and stacked combining marks;
  refuses empty, over-long, all-punctuation names; offensiveness is a policy hook (`name_filter`) (`README.md:69-73`).
- "By default an authenticated player cannot rename themselves ... Guests have no account name, so they can pick one."
  (`README.md:75`). game-playground turns name changes off because of its leaderboard; the lobby turns them on
  (`game-playground/CLAUDE.md:965-966`).
- Storage: "A failed read never destroys a profile": a session-only profile that `save()` refuses to persist
  (`README.md:77-82`).

## Compare

We have no accounts; a seat is `(room, name)` with a PIN (`backend/RandomRoom.Api/Domain/RoomPlayer.cs`). That is
already "scoped" to one room and cannot be correlated across rooms, which is the property their HMAC buys. Names are
chosen by the host at creation (`README.md` "Auth model" in our repo). Whether our name validation strips bidi and
zero-width characters: NOT CHECKED in this run (only a max length is visible in `RoomAdminService.cs:48-50`).

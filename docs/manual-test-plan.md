# Manual test plan: the deployed app on a real phone

Use this after every deploy that changes what players see. It takes about 15 minutes for the short path and about an
hour for everything. You need the phone and one laptop browser (a second phone is better) so you can be two players.

How to use it: copy the checklist into a note, tick items as you go, and write what you saw next to anything that did not
behave as described. For every bug write down: the phone model and browser, the steps, what you expected and what
happened, and a screenshot. Send those to whoever is fixing it.

**Short path (15 minutes):** sections 0, 1, 2, then Word Spies, Bingo and Sketch Guess from section 3, then the lock and
unlock test in section 4. These cover the biggest risks: the secure-connection features, the layouts that were fixed, and
live reconnection.

---

## 0. Does the deployment itself work? (2 minutes)

- [ ] **Render logs show a clean start.** No line saying the database is missing migrations, no SSL or certificate error,
      no crash. If you see "The database is missing N migration(s)", set the environment variable
      `Database__AutoMigrate=true` on the web service and redeploy (see "Known deploy messages" below).
- [ ] **The home page loads on the phone** and the browser tab shows the playground icon.
- [ ] **Cold start:** if the service was idle, note how long the first load took (the free tier sleeps after about 15
      minutes idle and can take up to a minute to wake).
- [ ] Open `https://<your-service>.onrender.com/api/config`. It should show `{"retentionDays":30}`.

## 1. Create a room and bring people in (the most important flow)

- [ ] On the phone, create a **Would You Rather** room with 3 players.
- [ ] On the "Who's playing?" step you see "Rooms are deleted after 30 days without play. The host can delete a room sooner."
- [ ] After creating, the screen shows a **recovery code** once. Copy it somewhere safe; you test it in section 5.
- [ ] **Share:** tap Share on one player's link and send it through WhatsApp. (The Share button only appears on a secure
      address, which the deployed site is.)
- [ ] **Copy link** shows "Copied" and pasting gives the link.
- [ ] **Show code** shows a QR code. Scan it with a second phone (or point the phone camera at the laptop screen showing
      the code). It opens the invite page.
- [ ] **Claim:** open an invite link, choose a PIN. The numeric keyboard appears. You land in the room.
- [ ] A second person joins through their own link; the player list shows both as ready.
- [ ] The join page of the room (opened in a private tab) says how long rooms are kept.

## 2. A full game, with two people

- [ ] The host starts the game; both screens switch to the game at once.
- [ ] Both answer; results show on both screens without a refresh.
- [ ] The host presses Next and Finish; the final screen shows the scoreboard; "Start new game" works.
- [ ] **End game asks first** ("End the game for everyone?") and "Keep playing" cancels it.

## 3. Real games on the phone (do a full round of each)

- [ ] **Would You Rather:** answers, reveal, scoreboard.
- [ ] **Word Spies:** board words never split in the middle of a word; nothing is cut off; no sideways scrolling; the
      spymaster sees the key and the other team does not.
- [ ] **Bingo:** long items wrap inside their cells; the called item is fully readable; "End Bingo" asks before ending.
- [ ] **Sketch Guess:** draw with a finger; the other player sees the drawing grow as you draw; a second finger or a
      resting palm does not scribble across the picture; Undo and Clear work.
- [ ] **Spin the Wheel:** the result only appears after the wheel stops. With the **host spinning**: the host sees no
      "Award a point" button and a short explanation; the other player sees "Give <host> the point". With another player
      spinning: only the host can award. A "Host decisions about scores" list appears after the host gives a point or
      moves on without one.
- [ ] **Forbidden Words:** a host who is not the judge has no "Flag a slip" button; the judge does and sees the card.
- [ ] **Buzzer Round:** the host judges and does not buzz; "Host decisions about scores" lists the points given.
- [ ] **Trivia with the East Africa question set:** read each question and check the answer is right. Write down any
      question you would change.

## 4. Things that only happen on real phones

- [ ] **Lock and unlock:** lock the screen for 30 seconds mid-game, then unlock. The room reconnects and shows the right
      state without a refresh.
- [ ] **Switch apps** (for example open WhatsApp) and come back.
- [ ] **Airplane mode** for 10 seconds, then back on. The "reconnecting" message appears and goes away.
- [ ] **Rotate** to landscape and back. Nothing breaks; the drawing board still fits.
- [ ] **Keyboard:** the on-screen keyboard does not cover the field you are typing in (PIN, answers, story entries).
- [ ] **No sideways scrolling** anywhere, including the narrowest screens.
- [ ] **Mobile data:** repeat a short game on mobile data instead of Wi-Fi. Note anything slow or stuck.
- [ ] **Back button:** pressing Back from the room goes to the home page, not to a used invite page.

## 5. Host tools

- [ ] **Reset a seat:** end the game, open "Someone locked out? Reset a seat or your recovery code", reset another player's
      seat, send the new link. Their old device is signed out with a message that the host reset their seat; with the new
      link they choose a new PIN and are back in. The seat shows "not joined" until they do.
- [ ] **Recovery code:** in a private tab (or another browser) open the room's join page and tap "Use your recovery code".
      Type the code you saved, in lower case with spaces; it is accepted. You are shown a **new** code and cannot continue
      until you tick "I have saved my new recovery code". Choose a new PIN and you are back as host. The old device was
      signed out. The code you used no longer works.
- [ ] **Make a new recovery code** from inside the room; the old one stops working.
- [ ] **Delete room:** as host, press "Delete room" and confirm. The other player's screen changes to a page saying "The host
      deleted this room", and the room's address now says there is no such room.
- [ ] **Not the host:** a player sees none of: Reset a seat, Delete room, host controls.

## 6. Content and limits

- [ ] **Trivia setup:** a question accepts at most 6 options; an option longer than 100 characters is not accepted; the
      "Add another question" button stops at 50 questions in total (yours plus starter questions).
- [ ] **Setup mistakes are explained on the setup step:** for example two wheel segments that differ only by an accent
      ("Cafe" and "Café") say "Each wheel segment must be different".

## 7. Accessibility quick checks (optional)

- [ ] Turn on the phone's screen reader (TalkBack or VoiceOver) and play one Would You Rather round. Headings are read in a
      sensible order, buttons say what they do, and the wheel result is announced once, after it stops.
- [ ] Increase the phone's text size one or two steps; nothing is cut off.

---

## Known deploy messages

- **"The database is missing N migration(s) and this app was not allowed to apply them"**: the environment variable
  `Database__AutoMigrate` is not set to `true` on the web service. The app is working as designed: it refuses to change
  a database it was not told it may change. Set `Database__AutoMigrate=true` in the Render environment and redeploy. This
  also proves the database connection (including TLS) worked.
- **"Cannot load library libgssapi_krb5.so.2"**: the database driver looked for an optional Kerberos library that the slim
  runtime image does not include. It is normally harmless and the driver carries on without it. If the app crashes right
  after it even with `Database__AutoMigrate=true`, the fix is to install `libgssapi-krb5-2` in the Dockerfile's final image.
- **Exited with status 139** straight after one of the messages above: a crash caused by the unhandled startup error, not a
  separate fault. Fix the message above it first.
- **Database refuses the connection with an SSL or certificate error:** add `?sslmode=prefer` to the end of the database
  URL, or `Trust Server Certificate=true`; see the README section "Database encryption".

## Where to report what you find

Write the bugs into a list (phone, browser, steps, expected, actual, screenshot). Anything that blocks playing is urgent;
layout or wording problems can be batched. Add confirmed fixes to `NEXT_STEPS.md` only if they are new work; fix small
bugs directly.

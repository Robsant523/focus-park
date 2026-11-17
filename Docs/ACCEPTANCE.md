# First milestone acceptance

This checklist is for a signed physical iPhone and a logged-in Mac with the signed Safari extension installed. It has not been completed in this environment.

- [ ] Import in Unity 6.3 LTS with no compile errors; run EditMode tests with Unity JSON serialization.
- [ ] In Play mode, complete the 10-second practice session: exactly 25 practice tickets, zero real focus minutes.
- [ ] Place the carousel on the yellow valid footprint. Paths, overlap, out-of-bounds placement, and insufficient tickets are rejected.
- [ ] Reopen Play mode; the purchased attraction and wallet remain. Inspect portrait and landscape UI, including all scrolling setup/build actions and the phone's keyboard.
- [ ] Build Mac game and export iPhone Xcode project; verify automatically embedded Device Activity extension and App Group entitlements.
- [ ] Sign and install the iPhone app and monitor. Authorize Screen Time and select one test distraction app plus a test website.
- [ ] Sign/run Mac helper and extension; select a nonessential app and a Safari domain, enable Safari extension and website permissions, and pair the phone.
- [ ] Invalid token, sleeping Mac, missing phone selections, or unacknowledged Safari rules prevent an active game session and release attempted protection.
- [ ] Start 25 minutes: phone app/site shields apply; the Mac helper intercepts its chosen app without terminating it; selected Safari domain and subdomains are blocked. A work site remains available.
- [ ] Put the iPhone game in the background. At expiry, restrictions release without Unity rendering. Confirm actual Safari release latency and recovery behavior.
- [ ] Return to the game: exactly 25 tickets are credited, once. Reopen repeatedly: no duplicate tickets.
- [ ] Purchase/place the 25-ticket carousel; the wallet is 0 and the attraction remains after restart.
- [ ] End early at 8 min 59 sec:8 tickets. The phone releases even if a simulated save failure prevents reward persistence.
- [ ] Revoke Screen Time authorization mid-session; foreground reconciliation ends the session and saved completed minutes remain.
- [ ] Disconnect Wi-Fi mid-session; coverage warning is truthful and native timers still expire. Reconnect and confirm old stop requests cannot cancel a newer session.
- [ ] Test midnight, timezone/clock changes, phone restart, Mac sleep/restart, Safari closed/reopened, permission removal, and corrupted-save backup recovery.

Keep an emergency exit available: end in the phone game, revoke Screen Time authorization, use the Mac helper's End/Quit menu, or disable its Safari extension.

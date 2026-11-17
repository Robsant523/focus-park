# Focus Park Mac helper

The companion keeps the session policy outside Unity. Selected Mac apps trigger a focus screen without closing or terminating those apps. A Safari web extension blocks selected domain navigations and moves already-open selected tabs to a local focus page. Other browsers are outside this build's coverage. The Mac owner can quit the helper or disable the extension; this is a voluntary focus tool.

## Build and configure

Quick app-interruption build (no Safari extension):

```sh
./build.sh
open Build/FocusParkHelper.app
```

Use **Choose distracting apps…**, choose `.app` bundles, and save. Opening the app also starts the local relay. The menu-bar Ferris wheel provides configuration and emergency end/quit actions. Closing Unity does not stop the helper; its persisted end time expires the app intervention independently. A Mac restart requires reopening the helper before app intervention resumes.

For Safari, open `FocusParkMac.xcodeproj` in Xcode. Select your Apple development team for **both** targets, enable their matching App Group (`group.com.focuspark.shared`), and build/run **FocusParkHelper**. If you change bundle identifiers or the App Group for your team, also update `PolicyStore.group`, both entitlement files, the extension identifier in `Sources/main.swift`, and `nativeApp` in `background.js`. Enable **Focus Park** in Safari Settings → Extensions and grant its website permissions. Keep Safari open when beginning a protected session. Enter domains such as `youtube.com, reddit.com` in the helper. The extension must acknowledge successful rules installation before `websitesReady` becomes true.

`build.sh` builds an ad-hoc signed apps-only helper. The Xcode project embeds the extension and is the build to use for Safari. Both the containing app and extension need access to the same signed App Group. An unsigned compilation verifies code but does not verify extension installation or shared-container authorization.

## Unity integration

Executable after the quick build: `Build/FocusParkHelper.app/Contents/MacOS/FocusParkHelper`.

| Command | Result |
| --- | --- |
| `serve` | Launch the background helper; returns `{"launched":true}`. |
| `configure` | Launch helper and show native picker/pairing token. |
| `status` | JSON local status, including `pairingToken` and `stateDirectory`. |
| `start SESSION_ID END_UNIX` | Persist a session, launch agent if needed, return status. |
| `stop` | Administrative local emergency stop. |

The agent exposes HTTP on port **43821**, reachable at `http://MAC_IP:43821` on a trusted private network. Every request requires `Authorization: Bearer PAIRING_TOKEN`; use the token shown in configuration or returned by the local CLI. The relay uses HTTP without encryption in this prototype. Do not expose this port to the public internet. macOS local-network/firewall approval may be needed on first use.

- `GET /status` returns status without disclosing the token.
- `POST /start` body: `{"sessionId":"unique-id","endUnix":1794900000}`. End time must be in the future and within three hours. A retry with the same ID/time is idempotent; a different active session returns HTTP 409.
- `POST /stop` body: `{"sessionId":"unique-id"}`. Only that active session is cleared. A stale stop returns current status unchanged. Omitting/blanking `sessionId` is an administrative emergency stop.

Status includes `available`, `configured`, `selectedApps`, `selectedWebsites`, `active`, `sessionId`, `endUnix`, `appsReady`, `websitesReady`, `ready`, `error`, `message`, and `coverage`. `available` requires a current helper heartbeat. `ready` requires an active session, live helper, and the Safari acknowledgement matching this session's exact policy if domains are selected. Check `active`/`sessionId` as well as `ready`; do not label an unreachable helper or an unacknowledged browser as protected.

Data defaults to the signed shared container, with an unsigned fallback in `~/Library/Application Support/FocusPark`. `FOCUSPARK_STATE_DIR` overrides storage for isolated development tests. Configuration, session, browser acknowledgement, and heartbeat are separate atomic files; token/configuration files are owner-readable only.

## Expiry and recovery limits

The helper checks app-policy expiry every half second and also detects application activation. On policy changes, the helper sends a native Safari message to prompt immediate reconciliation through the extension's native port. Safari's background script also reconciles on startup, tab activation, extension popup, a two-second live-context poll, and a one-minute browser alarm. If native messaging cannot wake the extension, the Unity client's 15-second readiness wait may time out; opening Safari's extension popup and choosing **Check connection** prompts another attempt. A specific end alarm clears rules. Safari may suspend background work, delay alarms, or retain dynamic rules while closed. Consequently browser release is not guaranteed at an exact second. Reopening Safari or selecting **Check connection** in the extension popup reconciles expired/stopped policy. Disabling the extension is an emergency website-unblock path.

Website readiness records actual successful dynamic-rule application, not an installed-extension assumption. It does not prove that the user left the extension enabled afterward. Mac app intervention is a focus screen rather than an OS launch prohibition; it needs a signed GUI-session test to assess interactions with full-screen apps and multiple displays.

## Verification

```sh
./test.sh
xcodebuild -project FocusParkMac.xcodeproj -scheme FocusParkHelper \
  -configuration Debug -derivedDataPath Build/Xcode CODE_SIGNING_ALLOWED=NO build
```

`test.sh` runs Swift persistence/session/expiry/acknowledgement checks and JavaScript tests for rule installation, failure handling, and release. `Tests/relay.test.py --headless` tests the HTTP relay with an isolated, test-only host after compiling `Tests/RelayHarness.swift` with `Sources/PolicyStore.swift` and `Sources/Relay.swift`. Running the script without `--headless` exercises the real GUI agent with an isolated nonexistent app selection; it requires an ordinary logged-in Mac shell.

Verified here: unsigned Xcode compilation of both targets, the policy/JavaScript tests, and the isolated headless authenticated HTTP relay test (start, readiness, retry idempotency, stale-stop protection, conflict rejection, and stop). AppKit cannot register a GUI application from the Codex shell sandbox (`NSApplication` initialization aborts in LaunchServices), so actual Mac app intervention, signed Safari installation, shared-container entitlement access, and iPhone-to-Mac LAN connectivity still require device testing.

Official references: [application activation notifications](https://developer.apple.com/documentation/appkit/nsworkspace/didactivateapplicationnotification), [Safari native messaging](https://developer.apple.com/documentation/safariservices/messaging-a-web-extension-s-native-app), [Safari declarative blocking](https://developer.apple.com/documentation/safariservices/blocking-content-with-your-safari-web-extension), [native app-to-extension messaging](https://developer.apple.com/documentation/safariservices/messaging-between-the-app-and-javascript-in-a-safari-web-extension).

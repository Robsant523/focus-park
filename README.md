# Focus Park

A Unity game that turns focus minutes into a tiny theme park. Write a task, finish a focus session, earn tickets, and place an attraction. The first complete 25-minute session buys the Daydream Carousel.

**Milestone 1 implementation · Unity 6.3 LTS · iPhone + Mac companion**

The repository includes the Unity game, native iPhone Screen Time integration, a Mac app intervention helper, a Safari blocker extension, and automated checks. The Unity Editor is not installed in the development environment used for this delivery, so the game has not yet been imported, visually tested, or exported by Unity. Native Apple targets compile successfully; actual enforcement on signed devices remains an acceptance step. See [verification](Docs/VERIFICATION.md).

## Try the game

1. Install [Unity Hub and Unity 6.3 LTS](https://unity.com/releases/editor/whats-new/6000.3.0f1), including **iOS Build Support** and **Mac Build Support**. The project records `6000.3.0f1`; a compatible 6.3 LTS patch can be used.
2. Add this repository's root folder as an existing project in Unity Hub.
3. Open `Assets/Scenes/Park.unity` and press **Play**. The scene bootstraps the game automatically; no scene wiring or imported art assets are needed.
4. Name a task and choose **Try a 10-second session** in the default practice park.
5. Wait 10 seconds to receive **25 practice tickets**. Choose **Build your park**, select **Daydream Carousel**, and **Place for 25 tickets**. Use the arrows and rotate button to choose a footprint.
6. Stop and restart Play mode to check persistence. Practice and real parks have separate saves and wallets.

Practice does not block apps, award real focus minutes, or transfer tickets to the real park. The interface labels it throughout.

The world is built procedurally: an isometric island, paths, trees, animated carousel, soda stand, and Ferris wheel. Park editing is available between sessions. Milestone 1 supports purchasing/placing and rotating before placement; moving/storing purchased attractions comes later.

## Run a protected 25-minute session

Protected sessions are initiated in the **iPhone game**. The Mac runs its separate helper; the Unity Mac game can preview/build its own local park. Cross-device park sync is not included in this milestone.

1. Build the Mac helper with Safari support in [Native/macOS](Native/macOS/README.md). Select your development team for both targets, configure the matching App Group, and run the app. Choose Mac applications and/or Safari domains; enable the Safari extension and its website permissions. Keep Safari open if domains are selected.
2. In Unity, choose **Focus Park → Build → iPhone Xcode project**. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode. Set your Apple team, register a unique app identifier and its `.monitor` extension identifier, and enable Family Controls plus the matching App Group for both. The export hook embeds the Device Activity monitor automatically. Follow [iPhone signing details](Native/iOS/README.md).
3. Install on a physical iPhone. Both devices must be on the same trusted Wi-Fi, with the Mac awake and its helper running. Allow local-network access on the iPhone and incoming access for the Mac helper if macOS asks.
4. In the iPhone game's **Protection settings**, grant Screen Time access, choose distractions, enter the Mac's LAN IPv4 address or `.local` hostname, and enter its pairing token. The token is shown in the Mac helper and is held in memory on the phone, so it must be entered after each app launch.
5. Choose **Check devices**, then **Use real park**. Name a task and choose **Focus for 25 minutes**.
6. The session becomes active only after the phone reports its shields and the Mac acknowledges the same session ID. With Safari domains selected, the extension must acknowledge applying the current rules too. A failed start releases the phone and asks the Mac to stop the pending session.
7. Put your phone down and do the task. At the end, each native component clears its restrictions independently. On return, the saved session earns exactly **25 tickets**, once. Place the **Daydream Carousel**, then reopen the app to verify it remains.

A Shield is Apple's restriction screen for a selected app/site. On Mac, application restriction is a focus-screen intervention: it does not terminate selected applications or guarantee an OS-level launch lock. Mac website coverage is Safari only. Both devices retain an emergency end path. Self-authorized restrictions can be revoked by the device owner.

Apple requires the [Family Controls distribution entitlement](https://developer.apple.com/documentation/FamilyControls/requesting-the-family-controls-entitlement) before distributing the phone app and monitor. Signed physical-device testing is necessary before calling this milestone accepted. No credentials, signing profiles, or developer-team identifiers are committed.

## Persistence and recovery

The real park uses `park.json`, and the separate practice park uses `practice-park.json`, under Unity's `Application.persistentDataPath`. Saves use temporary files and atomic replacement with a previous-save backup. Currency, attractions, receipts, and an active session are saved together. An already credited session ID cannot award tickets twice. Unreadable saves are preserved; a valid backup is used when available.

A completed session earns one ticket per planned focus minute. Ending early earns whole elapsed minutes. Practice sessions award 25 practice tickets only on completion. This is an honor-based elapsed-time reward; it cannot prove that a real-world task was performed. The timer survives app suspension/relaunch using a persisted end time; it is not an anti-cheat clock.

If a successful foreground check detects lost native protection, the session ends and retains completed minutes. If the Mac cannot be reached, the interface reports that its coverage cannot be checked; its local expiry remains responsible for release. Background connectivity is not continuously verified by Unity. If stopping a session cannot reach the Mac, its scheduled expiry still applies. See the native README for Safari alarm delays and restart limitations.

## Tests and builds

```sh
# Pure C# contracts, no Unity license required (.NET 8)
dotnet run --project Tools/CoreChecks/CoreChecks.csproj --configuration Release

# Swift policy and mocked Safari rule checks
Native/macOS/test.sh

# Ad-hoc signed apps-only helper; Safari requires the Xcode project
Native/macOS/build.sh
```

Unity: **Window → General → Test Runner → EditMode → Run All** exercises the same core contracts with Unity's actual JSON serializer. Use **Focus Park → Build** to export a Mac game or iPhone Xcode project. Native Xcode build and relay-test commands are in the native READMEs and [verification report](Docs/VERIFICATION.md).

[Architecture and boundaries](Docs/ARCHITECTURE.md) · [Device acceptance checklist](Docs/ACCEPTANCE.md)

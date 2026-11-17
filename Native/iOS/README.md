# iPhone blocking integration

This is real Screen Time integration, independent of the Unity frame loop. It requires a **physical iPhone running iOS 16 or later**, Xcode, and an Apple team whose app identifiers support Family Controls and the shared App Group. The source has been compiled and linked unsigned with Xcode's installed iOS 26.2 SDK. Device enforcement and permission prompts still need physical-device validation.

## Unity export

`Assets/Editor/IOSBuild.cs` runs after an iOS export. It adds the two bridge Swift sources to UnityFramework, creates and embeds a Device Activity Monitor extension, and configures Family Controls and App Group entitlements. The group is `group.<Unity iOS bundle identifier>.shared`; the monitor's identifier is `<Unity iOS bundle identifier>.monitor`.

Before signing, register both identifiers and the shared App Group in your Apple developer account, assign the same team to the app and monitor, and enable Family Controls for both. The build hook derives the team from Unity's iOS player settings. No Apple account credentials or private signing files are included.

For distribution, Apple must approve the Family Controls entitlement for the relevant targets. See [Apple's entitlement requirements](https://developer.apple.com/documentation/FamilyControls/requesting-the-family-controls-entitlement).

## C ABI

```c
void FP_RequestAuthorization(void);
void FP_ShowPicker(void);
void FP_StartSession(const char *sessionId, double endUnixSeconds);
void FP_EndSession(void);
char *FP_GetStatus(void);
void FP_FreeString(char *value);
```

Start/end and presentation operations enqueue on the iOS main thread. Poll status after requesting an operation and confirm that `active == true` **and** `sessionId` matches before showing protected-session success. Native errors never count as confirmation. Every returned status string belongs to the caller and must be freed with `FP_FreeString`.

Status JSON fields: `available`, `authorized`, `selectedApps`, `selectedWebsites`, `selectedCategories`, `active`, `sessionId`, `endUnix`, `error`. `active` confirms that scheduling succeeded and shield settings were applied; it cannot prove which human behavior occurred or that permission remains permanently granted.

Selected tokens stay in the local App Group; app/category/domain names and browsing history are not exported. Category counts are selected categories, not the number of apps inside those categories. The user can revoke individual authorization or remove the app.

Protected sessions must last at least 15 minutes at native scheduling time, matching [Apple's minimum monitoring interval](https://developer.apple.com/documentation/deviceactivity/deviceactivitycenter/monitoringerror/intervaltooshort). The milestone uses 25 minutes. Test mode should not call these APIs for a shortened 10-second session.

## Background expiry and recovery

The monitor extension removes shields at the scheduled interval end while Unity is suspended or terminated. The foreground bridge also reconciles expired state every second and on every status request. Session and selection files are atomically replaced under a cross-process lock. Monitor callbacks only clear the matching session, so a delayed callback cannot clear a newly started session. Explicit End always removes this app's named restrictions.

System scheduling and Screen Time callbacks need testing on supported OS versions; they should not be presented as an exact-to-the-second guarantee. Do not depend on a Unity coroutine, a local notification, or network connectivity to unlock the phone.

## Native probe

The probe project checks the same native source independently of Unity:

```sh
python3 Native/iOS/create_probe_project.py --bundle-id com.yourteam.focusparkprobe --team YOUR_TEAM_ID
open Native/iOS/Probe/FocusParkProbe.xcodeproj
```

Register the generated App Group and both probe identifiers before signing. On a physical iPhone, allow Screen Time access, choose a harmless test app and domain, and start a 25-minute session. Confirm both are shielded. Test End, natural expiry with the phone locked and Unity closed, app relaunch, reboot, permission revocation, and a session that crosses midnight. Confirm essential apps remain available. A probe session does not award Unity tickets; it is only a native integration check.

Compile without signing:

```sh
xcodebuild -project Native/iOS/Probe/FocusParkProbe.xcodeproj -scheme FocusParkProbe \
  -configuration Debug -destination 'generic/platform=iOS' \
  -derivedDataPath /tmp/focuspark-ios-check CODE_SIGNING_ALLOWED=NO build
```

The generated probe project uses no downloaded dependencies. Re-run the generator to change bundle/team settings. `Assets/Editor/IOSBuild.cs` still needs a Unity export/build check once Unity and its iOS Build Support module are installed.

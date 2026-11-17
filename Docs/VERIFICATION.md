# Verification report

Development verification: November 17, 2026. Xcode 26.2 SDKs / Swift 6.2.3; native sources use Swift 5 language mode. Unity project targets 6.3 LTS.

| Check | Result |
| --- | --- |
| Standalone pure C# contract suite | **15/15 passed**, .NET 8.0.428 |
| Core persistence roundtrip | Passed with System.Text.Json in standalone tests; actual Unity JsonUtility runner supplied but not executed |
| Runtime C# syntax checks, Mac and iOS defines | Passed against temporary API facades; this is not compilation against Unity assemblies |
| iPhone native probe app + embedded Device Activity extension | **Unsigned Xcode build succeeded**, generic iOS destination |
| iPhone exported C bridge | All six FP_* symbols present in compiled probe binary |
| Mac helper with embedded Safari extension | **Unsigned Xcode build succeeded** |
| Apps-only Mac helper | Swift compile and ad-hoc signing succeeded |
| Swift policy contracts | Passed: persistence, idempotency, conflict, acknowledgement, expiry, domain validation, token privacy |
| Safari JavaScript contracts | Passed against mocked browser APIs: apply rules, failed-install refusal, stop, expiry |
| Actual authenticated localhost HTTP relay | Passed: wrong-token rejection, no token leak, start/readiness, retry idempotency, conflicting start rejection, stale stop isolation, scoped stop |
| Unity import, Play mode, rendered UI, game exports | **Not run: Unity Editor is not installed** |
| Signed physical iPhone shielding/background expiry | **Not run: requires Apple signing and a physical iPhone** |
| Mac GUI intervention and signed Safari extension installation | **Not run: requires GUI-session/device acceptance** |
| Phone-to-Mac Wi-Fi connectivity | **Not run: requires installed iPhone build and paired Mac** |

The implementation is available for device acceptance; do not interpret native compilation or mocked tests as proof of system-level enforcement.

## Reproduce the Apple builds

From the repository root:

```sh
xcodebuild -project Native/iOS/Probe/FocusParkProbe.xcodeproj \
  -scheme FocusParkProbe -configuration Debug -sdk iphoneos \
  -derivedDataPath /tmp/focuspark-ios CODE_SIGNING_ALLOWED=NO build

xcodebuild -project Native/macOS/FocusParkMac.xcodeproj \
  -scheme FocusParkHelper -configuration Debug \
  -derivedDataPath /tmp/focuspark-mac CODE_SIGNING_ALLOWED=NO build

cd Native/macOS
./test.sh
./build.sh
xcrun swiftc -swift-version 5 -module-cache-path Build/ModuleCache \
  -framework Network -framework CryptoKit Sources/PolicyStore.swift \
  Sources/Relay.swift Tests/RelayHarness.swift -o Build/RelayHarness
python 3 Tests/relay.test.py --headless
```

The headless HTTP test uses an isolated temporary state directory and a nonexistent test app; it does not block any installed application. It binds port 43821, so stop a real helper before running it.

GitHub Actions runs the core, native policy, mocked Safari, Mac helper build, and headless relay checks. The Unity/native device acceptance steps are tracked in [ACCEPTANCE.md](ACCEPTANCE.md).

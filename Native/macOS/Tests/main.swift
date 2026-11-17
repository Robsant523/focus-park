import Foundation

let directory = FileManager.default.temporaryDirectory.appendingPathComponent("FocusParkPolicyTests-" + UUID().uuidString)
let store = PolicyStore(directory: directory)
defer { try? FileManager.default.removeItem(at: directory) }
func check(_ condition: @autoclosure () -> Bool, _ message: String) { if !condition() { fatalError(message) } }
do {
    var config = try store.ensureConfiguration()
    let token = config.pairingToken
    check(token.count == 64, "Pairing token must be random and stable")
    let reloadedToken = try store.ensureConfiguration().pairingToken
    check(reloadedToken == token, "Token must survive reload")
    do { try store.start(id: "empty", end: Date().timeIntervalSince1970 + 1500); fatalError("No-selection session accepted") } catch PolicyError.noSelection {}
    config.apps = ["com.focuspark.test.nonexistent"]; config.domains = ["example.com"]
    try store.write(config, to: "configuration.json")
    let end = Date().timeIntervalSince1970 + 1500
    try store.start(id: "one", end: end)
    try store.start(id: "one", end: end)
    do { try store.start(id: "two", end: end); fatalError("Concurrent session accepted") } catch PolicyError.alreadyActive {}
    store.heartbeat()
    check(store.status()["appsReady"] as? Bool == true, "Agent heartbeat missing")
    check(store.status()["websitesReady"] as? Bool == false, "Unacknowledged website rules marked ready")
    check(store.status()["ready"] as? Bool == false, "Session prematurely ready")
    try store.write(ExtensionAcknowledgement(policyKey: "stale", acknowledgedAt: Date().timeIntervalSince1970), to: "ack.json")
    check(store.status()["websitesReady"] as? Bool == false, "Stale acknowledgement accepted")
    try store.write(ExtensionAcknowledgement(policyKey: store.policyKey, acknowledgedAt: Date().timeIntervalSince1970), to: "ack.json")
    check(store.status()["ready"] as? Bool == true, "Current acknowledgement rejected")
    check(store.status()["pairingToken"] == nil, "HTTP status leaks pairing token")
    check(store.status(includeToken: true)["pairingToken"] as? String == token, "Local CLI must allow pairing")
    try store.stop(); check(store.current == nil, "Stop did not release session")
    try store.write(ParkSession(sessionId: "expired", endUnix: Date().timeIntervalSince1970 - 1), to: "session.json")
    check(store.current == nil, "Expired session remained active")
    check(store.extensionPolicy()["active"] as? Bool == false, "Expired extension policy remained active")
    let normalized = try PolicyStore.normalizeDomains("YouTube.com, reddit.com youtube.com")
    check(normalized == ["reddit.com", "youtube.com"], "Domain normalization incorrect")
    for invalid in ["https://youtube.com", "reddit.com/path", "bad..example", "-bad.example"] {
        do { _ = try PolicyStore.normalizeDomains(invalid); fatalError("Invalid domain accepted") } catch PolicyError.invalidDomain {}
    }
    print("PASS: policy persistence, idempotency, acknowledgement, expiry, pairing privacy, domain validation")
} catch { fatalError(error.localizedDescription) }

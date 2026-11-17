// Test-only HTTP relay host. Does not observe or interrupt any applications.
import Foundation
@main struct RelayHarness {
    static func main() throws {
        let store = PolicyStore(); _ = try store.ensureConfiguration()
        let relay = FocusRelay(store: store); try relay.start()
        store.heartbeat()
        let timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { _ in
            if let error = relay.failure { fputs("Relay listener failed: \(error)\n", stderr); exit(3) }
            store.heartbeat()
        }
        withExtendedLifetime((relay, timer)) { RunLoop.main.run() }
    }
}

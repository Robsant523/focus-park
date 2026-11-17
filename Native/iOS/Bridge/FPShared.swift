import Foundation
import FamilyControls
import ManagedSettings
import DeviceActivity
import Darwin

// Compiled into UnityFramework and the monitor extension. Opaque selections stay on device.
enum FPShared {
    struct Session: Codable {
        var id: String
        var endUnix: Double
        var monitor: String
    }
    static let store = ManagedSettingsStore(named: .init("FocusPark"))
    static var groupID: String { Bundle.main.object(forInfoDictionaryKey: "FPAppGroup") as? String ?? "group.com.focuspark.shared" }
    static var root: URL? { FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: groupID) }
    static func file(_ name: String) throws -> URL {
        guard let root = root else { throw FPError.message("App Group unavailable. Check signing and FPAppGroup entitlements.") }
        return root.appendingPathComponent(name)
    }
    static func locked<T>(_ work: () throws -> T) throws -> T {
        let descriptor = open(try file("focuspark.lock").path, O_CREAT | O_RDWR, S_IRUSR | S_IWUSR)
        guard descriptor >= 0 else { throw FPError.message("Cannot open shared session lock.") }
        defer { close(descriptor) }
        guard flock(descriptor, LOCK_EX) == 0 else { throw FPError.message("Cannot lock shared session state.") }
        defer { flock(descriptor, LOCK_UN) }
        return try work()
    }
    static func session() -> Session? {
        guard let url = try? file("session.json"), let data = try? Data(contentsOf: url) else { return nil }
        return try? JSONDecoder().decode(Session.self, from: data)
    }
    static func save(_ value: Session) throws { try JSONEncoder().encode(value).write(to: file("session.json"), options: .atomic) }
    static func selection() -> FamilyActivitySelection {
        guard let url = try? file("selection.json"), let data = try? Data(contentsOf: url),
              let value = try? JSONDecoder().decode(FamilyActivitySelection.self, from: data) else {
            return FamilyActivitySelection(includeEntireCategory: true)
        }
        return value
    }
    static func saveSelection(_ value: FamilyActivitySelection) throws {
        try JSONEncoder().encode(value).write(to: file("selection.json"), options: .atomic)
    }
    // Caller holds the cross-process lock. Never clear another session from a delayed callback.
    static func clear(matchingMonitor: String? = nil) throws {
        if let expected = matchingMonitor, session()?.monitor != expected { return }
        store.clearAllSettings()
        let url = try file("session.json")
        if FileManager.default.fileExists(atPath: url.path) { try FileManager.default.removeItem(at: url) }
    }
    static func apply(_ selected: FamilyActivitySelection) {
        store.shield.applications = selected.applicationTokens.isEmpty ? nil : selected.applicationTokens
        store.shield.webDomains = selected.webDomainTokens.isEmpty ? nil : selected.webDomainTokens
        store.shield.applicationCategories = selected.categoryTokens.isEmpty ? nil : .specific(selected.categoryTokens)
        store.shield.webDomainCategories = selected.categoryTokens.isEmpty ? nil : .specific(selected.categoryTokens)
    }
}

enum FPError: LocalizedError {
    case message(String)
    var errorDescription: String? { if case .message(let text) = self { return text }; return nil }
}

import Foundation
import CryptoKit

struct ParkConfiguration: Codable {
    var apps: [String] = []
    var domains: [String] = []
    var pairingToken: String = UUID().uuidString.replacingOccurrences(of: "-", with: "") + UUID().uuidString.replacingOccurrences(of: "-", with: "")
}
struct ParkSession: Codable {
    let sessionId: String
    let endUnix: Double
    var active: Bool { endUnix > Date().timeIntervalSince1970 }
}
struct ExtensionAcknowledgement: Codable {
    let policyKey: String
    let acknowledgedAt: Double
}
final class PolicyStore {
    static let group = "group.com.focuspark.shared"
    let directory: URL
    let decoder = JSONDecoder()
    let encoder = JSONEncoder()
    init(directory: URL? = nil) {
        if let directory { self.directory = directory }
        else if let override = ProcessInfo.processInfo.environment["FOCUSPARK_STATE_DIR"] { self.directory = URL(fileURLWithPath: override) }
        else if let shared = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: Self.group) { self.directory = shared }
        else { self.directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("FocusPark") }
    }
    func read<T: Decodable>(_ name: String, as: T.Type) -> T? {
        guard let data = try? Data(contentsOf: directory.appendingPathComponent(name)) else { return nil }
        return try? decoder.decode(T.self, from: data)
    }
    func write<T: Encodable>(_ value: T, to name: String) throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let file = directory.appendingPathComponent(name)
        try encoder.encode(value).write(to: file, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: file.path)
    }
    var configuration: ParkConfiguration { read("configuration.json", as: ParkConfiguration.self) ?? ParkConfiguration() }
    func ensureConfiguration() throws -> ParkConfiguration {
        if let existing = read("configuration.json", as: ParkConfiguration.self) { return existing }
        let value = ParkConfiguration(); try write(value, to: "configuration.json"); return value
    }
    var session: ParkSession? { read("session.json", as: ParkSession.self) }
    var current: ParkSession? { guard let value = session, value.active else { return nil }; return value }
    func start(id: String, end: Double) throws {
        guard !id.isEmpty, id.count <= 128, end.isFinite, end > Date().timeIntervalSince1970, end <= Date().timeIntervalSince1970 + 10800 else { throw PolicyError.invalidSession }
        if let active = current {
            guard active.sessionId == id, abs(active.endUnix - end) < 1 else { throw PolicyError.alreadyActive }
            return
        }
        let config = try ensureConfiguration()
        guard !config.apps.isEmpty || !config.domains.isEmpty else { throw PolicyError.noSelection }
        try write(ParkSession(sessionId: id, endUnix: end), to: "session.json")
        try? FileManager.default.removeItem(at: directory.appendingPathComponent("ack.json"))
    }
    func stop() throws {
        try? FileManager.default.removeItem(at: directory.appendingPathComponent("ack.json"))
        let url = directory.appendingPathComponent("session.json")
        if FileManager.default.fileExists(atPath: url.path) { try FileManager.default.removeItem(at: url) }
    }
    var policyKey: String {
        guard let active = current else { return "inactive" }
        let value = active.sessionId + "|" + String(active.endUnix) + "|" + configuration.domains.sorted().joined(separator: ",")
        return SHA256.hash(data: Data(value.utf8)).map { String(format: "%02x", $0) }.joined()
    }
    func heartbeat() { try? write(Date().timeIntervalSince1970, to: "heartbeat.json") }
    var agentAlive: Bool {
        guard let time = read("heartbeat.json", as: Double.self) else { return false }
        return abs(Date().timeIntervalSince1970 - time) < 5
    }
    func status(includeToken: Bool = false) -> [String: Any] {
        let config = configuration; let active = current
        let appReady = agentAlive
        let ack = read("ack.json", as: ExtensionAcknowledgement.self)
        let webReady = config.domains.isEmpty || (active != nil && ack?.policyKey == policyKey)
        var value: [String: Any] = [
            "available": agentAlive, "configured": !config.apps.isEmpty || !config.domains.isEmpty,
            "selectedApps": config.apps.count, "selectedWebsites": config.domains.count,
            "appCount": config.apps.count, "domainCount": config.domains.count,
            "active": active != nil, "sessionId": active?.sessionId ?? "", "endUnix": active?.endUnix ?? 0,
            "appsReady": appReady, "websitesReady": webReady,
            "ready": active != nil && appReady && webReady,
            "coverage": "Mac apps: focus-screen intervention; websites: Safari extension only",
            "error": "", "message": !agentAlive ? "Mac helper is not running." : (!webReady ? "Waiting for Safari extension to apply this session. Open Safari with the extension enabled." : "Mac helper ready."),
            "policyKey": policyKey
        ]
        if includeToken { value["pairingToken"] = config.pairingToken; value["stateDirectory"] = directory.path }
        return value
    }
    func extensionPolicy() -> [String: Any] {
        ["active": current != nil, "sessionId": current?.sessionId ?? "", "endUnix": current?.endUnix ?? 0,
         "domains": current == nil ? [] : configuration.domains, "policyKey": policyKey]
    }
    static func normalizeDomains(_ raw: String) throws -> [String] {
        let items = raw.components(separatedBy: CharacterSet(charactersIn: ",\n ")).filter { !$0.isEmpty }
        var domains: [String] = []
        for item in items {
            let lower = item.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
            guard !lower.contains("://"), !lower.contains("/"), !lower.contains(":"), !lower.hasPrefix("."), !lower.hasSuffix("."), lower.contains("."), lower.count <= 253,
                  lower.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "." || $0 == "-") }),
                  lower.split(separator: ".", omittingEmptySubsequences: false).allSatisfy({ !$0.isEmpty && !$0.hasPrefix("-") && !$0.hasSuffix("-") && $0.count <= 63 }) else { throw PolicyError.invalidDomain(item) }
            domains.append(lower)
        }
        return Array(Set(domains)).sorted()
    }
}
enum PolicyError: LocalizedError {
    case invalidSession, alreadyActive, noSelection, invalidDomain(String)
    var errorDescription: String? {
        switch self {
        case .invalidSession: return "Use a unique session ID and an end time within the next three hours."
        case .alreadyActive: return "Another focus session is active. End it before starting another."
        case .noSelection: return "Choose at least one distracting app or Safari website first."
        case .invalidDomain(let domain): return "Enter domain names such as youtube.com, without a URL or path. Invalid entry: \(domain)"
        }
    }
}
func jsonData(_ value: [String: Any]) -> Data { (try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys])) ?? Data("{}".utf8) }

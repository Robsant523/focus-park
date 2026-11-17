import Foundation
import Network

final class FocusRelay {
    let store: PolicyStore
    var listener: NWListener?
    var failure: String?
    init(store: PolicyStore) { self.store = store }
    func start() throws {
        let listener = try NWListener(using: .tcp, on: 43821)
        self.listener = listener
        listener.newConnectionHandler = { [weak self] connection in
            guard let self else { return }; connection.start(queue: .main)
            self.receive(connection, accumulated: Data())
        }
        listener.stateUpdateHandler = { [weak self] state in
            if case .failed(let error) = state { self?.failure = error.localizedDescription }
        }
        listener.start(queue: .main)
    }
    func receive(_ connection: NWConnection, accumulated: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 16384) { [weak self] data, _, complete, error in
            guard let self else { return }
            var buffer = accumulated; if let data { buffer.append(data) }
            guard buffer.count <= 32768 else { self.respond(connection, code: 413, value: ["error": "Request too large"]); return }
            if let marker = buffer.range(of: Data("\r\n\r\n".utf8)) {
                guard let header = String(data: buffer[..<marker.lowerBound], encoding: .utf8) else { connection.cancel(); return }
                let lines = header.components(separatedBy: "\r\n")
                let fields = lines.first?.split(separator: " ").map(String.init) ?? []
                var headers: [String: String] = [:]
                for line in lines.dropFirst() {
                    guard let colon = line.firstIndex(of: ":") else { continue }
                    headers[String(line[..<colon]).lowercased()] = String(line[line.index(after: colon)...]).trimmingCharacters(in: .whitespaces)
                }
                let length = Int(headers["content-length"] ?? "0") ?? -1
                guard length >= 0 && length <= 16384 && headers["transfer-encoding"] == nil else { self.respond(connection, code: 400, value: ["error": "Invalid request length"]); return }
                if buffer.count >= marker.upperBound + length {
                    guard fields.count == 3, headers["authorization"] == "Bearer " + self.store.configuration.pairingToken else { self.respond(connection, code: 401, value: ["error": "Pairing token required"]); return }
                    let body = Data(buffer[marker.upperBound..<(marker.upperBound + length)])
                    self.route(connection, method: fields[0], path: fields[1], body: body); return
                }
            }
            if complete || error != nil { connection.cancel(); return }
            self.receive(connection, accumulated: buffer)
        }
    }
    func route(_ connection: NWConnection, method: String, path: String, body: Data) {
        do {
            if method == "GET" && path == "/status" { respond(connection, value: store.status()); return }
            if method == "POST" && path == "/start" {
                guard let object = try JSONSerialization.jsonObject(with: body) as? [String: Any], let id = object["sessionId"] as? String, let end = object["endUnix"] as? Double else { respond(connection, code: 400, value: ["error": "Expected sessionId and endUnix"]); return }
                try store.start(id: id, end: end); respond(connection, value: store.status()); return
            }
            if method == "POST" && path == "/stop" {
                let object = (try? JSONSerialization.jsonObject(with: body)) as? [String: Any]
                let id = object?["sessionId"] as? String ?? ""
                if id.isEmpty || store.current?.sessionId == id { try store.stop() }
                respond(connection, value: store.status()); return
            }
            respond(connection, code: 404, value: ["error": "Unknown route"])
        } catch { respond(connection, code: 409, value: ["error": error.localizedDescription]) }
    }
    func respond(_ connection: NWConnection, code: Int = 200, value: [String: Any]) {
        let body = jsonData(value)
        let header = "HTTP/1.1 \(code) \(code == 200 ? "OK" : "Error")\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nContent-Length: \(body.count)\r\nConnection: close\r\n\r\n"
        connection.send(content: Data(header.utf8) + body, completion: .contentProcessed { _ in connection.cancel() })
    }
}

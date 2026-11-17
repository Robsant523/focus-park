import Foundation
import SafariServices

final class SafariWebExtensionHandler: NSObject, NSExtensionRequestHandling {
    func beginRequest(with context: NSExtensionContext) {
        let input = context.inputItems.first as? NSExtensionItem
        let message = input?.userInfo?[SFExtensionMessageKey] as? [String: Any] ?? [:]
        let store = PolicyStore()
        var response = store.extensionPolicy()
        if message["action"] as? String == "ack", let key = message["policyKey"] as? String, key == store.policyKey {
            do {
                try store.write(ExtensionAcknowledgement(policyKey: key, acknowledgedAt: Date().timeIntervalSince1970), to: "ack.json")
                response["acknowledged"] = true
            } catch { response["error"] = error.localizedDescription }
        }
        let item = NSExtensionItem(); item.userInfo = [SFExtensionMessageKey: response]
        context.completeRequest(returningItems: [item])
    }
}

import Foundation
import UIKit
import SwiftUI
import FamilyControls
import ManagedSettings
import DeviceActivity
import Darwin

@MainActor
private final class FPBlocking {
    static let shared = FPBlocking()
    var error = ""
    var picker: UIViewController?
    let center = DeviceActivityCenter()
    var timer: Timer?
    private init() {
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { _ in
            Task { @MainActor in FPBlocking.shared.reconcile() }
        }
    }
    var authorized: Bool { AuthorizationCenter.shared.authorizationStatus == .approved }
    func reconcile() {
        do {
            try FPShared.locked {
                if FPShared.session() == nil {
                    // Recover safely if a session file was lost/corrupted after shields were applied.
                    try FPShared.clear()
                    let orphaned = center.activities.filter { $0.rawValue.hasPrefix("focuspark.") }
                    if !orphaned.isEmpty { center.stopMonitoring(orphaned) }
                }
                if let session = FPShared.session(), session.endUnix <= Date().timeIntervalSince1970 || !authorized {
                    try FPShared.clear()
                    center.stopMonitoring([.init(session.monitor)])
                }
                if !authorized {
                    // Revocation invalidates selection tokens even when no session was running.
                    let old = FPShared.selection()
                    if !old.applicationTokens.isEmpty || !old.webDomainTokens.isEmpty || !old.categoryTokens.isEmpty {
                        try FPShared.saveSelection(FamilyActivitySelection(includeEntireCategory: true))
                    }
                }
            }
        } catch { self.error = error.localizedDescription }
    }
    func requestAuthorization() {
        error = ""
        Task {
            do { try await AuthorizationCenter.shared.requestAuthorization(for: .individual) }
            catch { self.error = error.localizedDescription }
        }
    }
    func showPicker() {
        error = ""
        reconcile()
        guard authorized else { error = "Allow Screen Time access first."; return }
        guard FPShared.session() == nil else { error = "End your current session before changing distractions."; return }
        guard picker == nil else { return }
        guard let scene = UIApplication.shared.connectedScenes.compactMap({ $0 as? UIWindowScene }).first(where: { $0.activationState == .foregroundActive }),
              var presenter = scene.windows.first(where: { $0.isKeyWindow })?.rootViewController else { error = "No active window for distraction picker."; return }
        while let presented = presenter.presentedViewController { presenter = presented }
        let view = FPPicker(initial: FPShared.selection()) { [weak self] selection in
            guard let self = self else { return }
            if let selection = selection {
                do { try FPShared.saveSelection(selection) }
                catch { self.error = error.localizedDescription }
            }
            self.picker?.dismiss(animated: true)
            self.picker = nil
        }
        let controller = UIHostingController(rootView: view)
        controller.isModalInPresentation = true
        picker = controller
        presenter.present(controller, animated: true)
    }
    func start(id: String, end: Double) {
        error = ""
        reconcile()
        guard authorized else { error = "Screen Time access is not authorized."; return }
        guard !id.isEmpty, id.count <= 120, end.isFinite else { error = "Invalid focus session."; return }
        let remaining = end - Date().timeIntervalSince1970
        guard remaining >= 900, remaining <= 86400 else { error = "Protected sessions must last at least 15 minutes and at most 24 hours."; return }
        let selected = FPShared.selection()
        guard !selected.applicationTokens.isEmpty || !selected.webDomainTokens.isEmpty || !selected.categoryTokens.isEmpty else {
            error = "Choose at least one distracting app or website first."; return
        }
        do {
            try FPShared.locked {
                if let current = FPShared.session() {
                    guard current.id != id else { return }
                    throw FPError.message("Another focus session is already active.")
                }
                let monitor = "focuspark." + UUID().uuidString
                let calendar = Calendar.current
                let components: Set<Calendar.Component> = [.era, .year, .month, .day, .hour, .minute, .second]
                let schedule = DeviceActivitySchedule(
                    intervalStart: calendar.dateComponents(components, from: Date()),
                    intervalEnd: calendar.dateComponents(components, from: Date(timeIntervalSince1970: end)),
                    repeats: false)
                // Schedule first; never apply restrictions if the monitor could not be registered.
                try center.startMonitoring(.init(monitor), during: schedule)
                do {
                    try FPShared.save(.init(id: id, endUnix: end, monitor: monitor))
                    FPShared.apply(selected)
                } catch {
                    center.stopMonitoring([.init(monitor)])
                    FPShared.store.clearAllSettings()
                    throw error
                }
            }
        } catch { self.error = error.localizedDescription }
    }
    func end() {
        error = ""
        do {
            try FPShared.locked {
                let session = FPShared.session()
                try FPShared.clear()
                if let session = session { center.stopMonitoring([.init(session.monitor)]) }
            }
        } catch {
            // The escape action still clears this app's restrictions if shared storage is unavailable.
            FPShared.store.clearAllSettings()
            center.stopMonitoring(center.activities.filter { $0.rawValue.hasPrefix("focuspark.") })
            self.error = error.localizedDescription
        }
    }
    func status() -> String {
        reconcile()
        let selected = FPShared.selection()
        let session = FPShared.session()
        let object: [String: Any] = [
            "available": FPShared.root != nil,
            "authorized": authorized,
            "selectedApps": selected.applicationTokens.count,
            "selectedWebsites": selected.webDomainTokens.count,
            "selectedCategories": selected.categoryTokens.count,
            "active": session != nil,
            "sessionId": session?.id ?? "",
            "endUnix": session?.endUnix ?? 0,
            "error": error
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: object), let string = String(data: data, encoding: .utf8) else { return "{}" }
        return string
    }
}

private struct FPPicker: View {
    @State var selection: FamilyActivitySelection
    var done: (FamilyActivitySelection?) -> Void
    init(initial: FamilyActivitySelection, done: @escaping (FamilyActivitySelection?) -> Void) {
        _selection = State(initialValue: initial)
        self.done = done
    }
    var body: some View {
        NavigationView {
            FamilyActivityPicker(selection: $selection)
                .navigationTitle("Choose distractions")
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) { Button("Cancel") { done(nil) } }
                    ToolbarItem(placement: .confirmationAction) { Button("Save") { done(selection) } }
                }
        }
    }
}

// Unity invokes these C symbols. All UIKit/Screen Time operations run on the iOS main thread.
@_cdecl("FP_RequestAuthorization")
public func FP_RequestAuthorization() { DispatchQueue.main.async { FPBlocking.shared.requestAuthorization() } }
@_cdecl("FP_ShowPicker")
public func FP_ShowPicker() { DispatchQueue.main.async { FPBlocking.shared.showPicker() } }
@_cdecl("FP_StartSession")
public func FP_StartSession(_ id: UnsafePointer<CChar>?, _ endUnix: Double) {
    let copiedID = id.map { String(cString: $0) } ?? ""
    DispatchQueue.main.async { FPBlocking.shared.start(id: copiedID, end: endUnix) }
}
@_cdecl("FP_EndSession")
public func FP_EndSession() { DispatchQueue.main.async { FPBlocking.shared.end() } }
@_cdecl("FP_GetStatus")
public func FP_GetStatus() -> UnsafeMutablePointer<CChar>? {
    let result: String
    if Thread.isMainThread { result = MainActor.assumeIsolated { FPBlocking.shared.status() } }
    else { result = DispatchQueue.main.sync { FPBlocking.shared.status() } }
    return strdup(result)
}
@_cdecl("FP_FreeString")
public func FP_FreeString(_ value: UnsafeMutablePointer<CChar>?) { free(value) }

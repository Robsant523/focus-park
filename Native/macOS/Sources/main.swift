import AppKit
import SafariServices
import Foundation

final class FocusAgent: NSObject, NSApplicationDelegate {
    let store = PolicyStore()
    var relay: FocusRelay?
    var timer: Timer?
    var configWindow: NSWindow?
    var shield: NSWindow?
    var domainField: NSTextField?
    var appLabel: NSTextField?
    var notice: NSTextField?
    var chosenApps: [String] = []
    var lastAllowed: NSRunningApplication?
    var lastPolicyKey: String?
    func applicationDidFinishLaunching(_ notification: Notification) {
        do {
            _ = try store.ensureConfiguration()
            relay = FocusRelay(store: store); try relay?.start()
        } catch { fputs("Focus Park: \(error.localizedDescription)\n", stderr) }
        DistributedNotificationCenter.default().addObserver(self, selector: #selector(showConfiguration), name: NSNotification.Name("com.focuspark.configure"), object: nil)
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(activation(_:)), name: NSWorkspace.didActivateApplicationNotification, object: nil)
        timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in self?.tick() }
        tick()
        if CommandLine.arguments.count == 1 || CommandLine.arguments.contains("--configure") { showConfiguration() }
        makeMenu()
    }
    var menuItem: NSStatusItem?
    func makeMenu() {
        menuItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        menuItem?.button?.title = "🎡"
        let menu = NSMenu()
        let config = menu.addItem(withTitle: "Configure Focus Park…", action: #selector(showConfiguration), keyEquivalent: "")
        config.target = self
        let stop = menu.addItem(withTitle: "End session and unblock", action: #selector(stopSession), keyEquivalent: "")
        stop.target = self
        let quit = menu.addItem(withTitle: "Quit helper and unblock", action: #selector(quit), keyEquivalent: "")
        quit.target = self
        menuItem?.menu = menu
    }
    @objc func quit() { try? store.stop(); NSApp.terminate(nil) }
    @objc func stopSession() { try? store.stop(); shield?.orderOut(nil) }
    @objc func activation(_ notification: Notification) { tick() }
    func tick() {
        store.heartbeat()
        let key = store.policyKey
        if key != lastPolicyKey {
            let hadPolicy = lastPolicyKey != nil
            lastPolicyKey = key
            if !store.configuration.domains.isEmpty && (hadPolicy || store.session != nil) {
                SFSafariApplication.dispatchMessage(withName: "focus-policy-changed", toExtensionWithIdentifier: "com.focuspark.helper.safari", userInfo: nil) { _ in
                    // Readiness still requires the browser's acknowledgement after installing rules.
                }
            }
        }
        if let failure = relay?.failure { notice?.stringValue = "Relay unavailable: \(failure)" }
        guard store.current != nil else { shield?.orderOut(nil); return }
        guard let front = NSWorkspace.shared.frontmostApplication, let bundle = front.bundleIdentifier else { return }
        if store.configuration.apps.contains(bundle) { presentShield(for: front.localizedName ?? bundle) }
        else if bundle != Bundle.main.bundleIdentifier { lastAllowed = front; shield?.orderOut(nil) }
    }
    func presentShield(for name: String) {
        if shield == nil {
            let screen = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 900, height: 600)
            let window = NSWindow(contentRect: screen, styleMask: [.titled], backing: .buffered, defer: false)
            window.title = "Focus Park — session in progress"
            window.level = .floating; window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
            window.isReleasedWhenClosed = false
            let stack = NSStackView(); stack.orientation = .vertical; stack.spacing = 24; stack.translatesAutoresizingMaskIntoConstraints = false
            let title = NSTextField(labelWithString: "Your next attraction is growing."); title.font = .systemFont(ofSize: 32, weight: .bold)
            let text = NSTextField(wrappingLabelWithString: "This app is on your distraction list. Return to your task, or end the session to unblock.")
            text.alignment = .center; text.preferredMaxLayoutWidth = 500
            let back = NSButton(title: "Return to my task", target: self, action: #selector(hideShield))
            let stop = NSButton(title: "End session and unblock", target: self, action: #selector(stopSession))
            for view in [title, text, back, stop] { stack.addArrangedSubview(view) }
            window.contentView?.addSubview(stack)
            NSLayoutConstraint.activate([stack.centerXAnchor.constraint(equalTo: window.contentView!.centerXAnchor), stack.centerYAnchor.constraint(equalTo: window.contentView!.centerYAnchor), stack.widthAnchor.constraint(lessThanOrEqualToConstant: 650)])
            shield = window
        }
        shield?.title = "\(name) is a distraction — Focus Park"
        shield?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    @objc func hideShield() {
        shield?.orderOut(nil); NSApp.hide(nil)
        let target = lastAllowed ?? NSRunningApplication.runningApplications(withBundleIdentifier: "com.apple.finder").first
        target?.activate(options: [])
    }
    @objc func showConfiguration() {
        if let configWindow { configWindow.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); return }
        let config = store.configuration; chosenApps = config.apps
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 640, height: 540), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "Focus Park — Mac distractions"; window.center(); window.isReleasedWhenClosed = false
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 16; stack.translatesAutoresizingMaskIntoConstraints = false
        let title = NSTextField(labelWithString: "Make room for focus"); title.font = .systemFont(ofSize: 25, weight: .bold)
        let explanation = NSTextField(wrappingLabelWithString: "Choose apps for focus-screen interruptions and domains for Safari blocking. Your apps stay open. Enable the Focus Park Safari extension before selecting websites.")
        let choose = NSButton(title: "Choose distracting apps…", target: self, action: #selector(chooseApps))
        let apps = NSTextField(wrappingLabelWithString: chosenApps.isEmpty ? "No apps selected" : chosenApps.joined(separator: ", ")); appLabel = apps
        let label = NSTextField(labelWithString: "Safari domains, separated by commas")
        let domains = NSTextField(string: config.domains.joined(separator: ", ")); domains.placeholderString = "youtube.com, reddit.com"; domainField = domains
        let tokenLabel = NSTextField(labelWithString: "Pair your iPhone using this Mac’s IP and port 43821")
        let token = NSTextField(string: config.pairingToken); token.isEditable = false; token.isSelectable = true
        let privacy = NSTextField(wrappingLabelWithString: "Pair only on a trusted private network. The pairing token authorizes starting and ending sessions. This prototype uses local HTTP without encryption.")
        privacy.font = .systemFont(ofSize: 11); privacy.textColor = .secondaryLabelColor
        let save = NSButton(title: "Save distraction list", target: self, action: #selector(saveConfiguration))
        let notice = NSTextField(wrappingLabelWithString: "Lists can be changed between sessions."); self.notice = notice
        for view in [title, explanation, choose, apps, label, domains, tokenLabel, token, privacy, save, notice] { stack.addArrangedSubview(view) }
        window.contentView?.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 28), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -28), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 28), domains.widthAnchor.constraint(equalTo: stack.widthAnchor), token.widthAnchor.constraint(equalTo: stack.widthAnchor)])
        configWindow = window; window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    @objc func chooseApps() {
        guard store.current == nil else { notice?.stringValue = "End your current session before changing the list."; return }
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.applicationBundle]; panel.allowsMultipleSelection = true
        panel.directoryURL = URL(fileURLWithPath: "/Applications")
        if panel.runModal() == .OK {
            chosenApps = panel.urls.compactMap { Bundle(url: $0)?.bundleIdentifier }.filter { $0 != Bundle.main.bundleIdentifier && $0 != "com.apple.finder" }
            appLabel?.stringValue = chosenApps.isEmpty ? "No apps selected" : chosenApps.joined(separator: ", ")
        }
    }
    @objc func saveConfiguration() {
        do {
            guard store.current == nil else { notice?.stringValue = "End your current session before changing the list."; return }
            var config = try store.ensureConfiguration(); config.apps = Array(Set(chosenApps)).sorted(); config.domains = try PolicyStore.normalizeDomains(domainField?.stringValue ?? "")
            try store.write(config, to: "configuration.json"); notice?.stringValue = "Saved. Start your session from Focus Park."
        } catch { notice?.stringValue = error.localizedDescription }
    }
}

func printJSON(_ object: [String: Any]) { print(String(data: jsonData(object), encoding: .utf8) ?? "{}") }
func launchAgent(configure: Bool) throws {
    let executable = URL(fileURLWithPath: CommandLine.arguments[0]).standardizedFileURL
    let app = executable.deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
    let process = Process()
    if app.pathExtension == "app" {
        process.executableURL = URL(fileURLWithPath: "/usr/bin/open")
        process.arguments = ["-g", "-a", app.path, "--args", "--agent"] + (configure ? ["--configure"] : [])
    } else { process.executableURL = executable; process.arguments = ["--agent"] + (configure ? ["--configure"] : []) }
    process.standardOutput = FileHandle.nullDevice; process.standardError = FileHandle.nullDevice
    try process.run()
    if configure { DistributedNotificationCenter.default().postNotificationName(NSNotification.Name("com.focuspark.configure"), object: nil, deliverImmediately: true) }
}
let arguments = Array(CommandLine.arguments.dropFirst())
if arguments.isEmpty || arguments.first == "--agent" {
    let application = NSApplication.shared
    application.setActivationPolicy(.accessory)
    let delegate = FocusAgent(); application.delegate = delegate; application.run()
} else {
    let store = PolicyStore()
    do {
        _ = try store.ensureConfiguration()
        switch arguments.first ?? "configure" {
        case "status": printJSON(store.status(includeToken: true))
        case "serve": try launchAgent(configure: false); printJSON(["launched": true])
        case "configure": try launchAgent(configure: true); printJSON(["launched": true])
        case "start":
            guard arguments.count == 3, let end = Double(arguments[2]) else { throw PolicyError.invalidSession }
            try store.start(id: arguments[1], end: end)
            if !store.agentAlive { try launchAgent(configure: false) }
            printJSON(store.status(includeToken: true))
        case "stop": try store.stop(); printJSON(store.status(includeToken: true))
        default: printJSON(["error": "Commands: configure, serve, status, start SESSION_ID END_UNIX, stop"]); exit(2)
        }
    } catch { printJSON(["error": error.localizedDescription]); exit(1) }
}

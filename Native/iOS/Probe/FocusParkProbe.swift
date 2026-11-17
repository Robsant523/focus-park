import SwiftUI

// A small device-validation app; it uses the exact bridge that Unity exports.
@main
struct FocusParkProbe: App {
    var body: some Scene { WindowGroup { ProbeView() } }
}

struct ProbeView: View {
    @State private var status = ""
    private let ticker = Timer.publish(every: 1, on: .main, in: .common).autoconnect()
    var body: some View {
        NavigationView {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    Text("Screen Time integration probe").font(.title2.bold())
                    Text("This checks native blocking independently of the Unity park. Use a physical iPhone signed by your Apple team.")
                    Button("1. Allow Screen Time") { FP_RequestAuthorization() }
                    Button("2. Select apps and websites") { FP_ShowPicker() }
                    Button("3. Start 25-minute protected session") {
                        let id = UUID().uuidString
                        id.withCString { FP_StartSession($0, Date().timeIntervalSince1970 + 1500) }
                    }
                    Button("End session and unblock", role: .destructive) { FP_EndSession() }
                    Text(status).font(.system(.caption, design: .monospaced)).textSelection(.enabled)
                }.padding(24)
            }.navigationTitle("Focus Park")
        }.onReceive(ticker) { _ in
            if let pointer = FP_GetStatus() { status = String(cString: pointer); FP_FreeString(pointer) }
        }
    }
}

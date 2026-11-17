import DeviceActivity
import Foundation

// The OS runs this extension while Unity is suspended or terminated.
final class FocusParkMonitor: DeviceActivityMonitor {
    override func intervalDidStart(for activity: DeviceActivityName) {
        super.intervalDidStart(for: activity)
        do {
            try FPShared.locked {
                guard let session = FPShared.session(), session.monitor == activity.rawValue else { return }
                if session.endUnix <= Date().timeIntervalSince1970 { try FPShared.clear(matchingMonitor: activity.rawValue) }
                else { FPShared.apply(FPShared.selection()) }
            }
        } catch { NSLog("FocusPark monitor start failed: %@", error.localizedDescription) }
    }
    override func intervalDidEnd(for activity: DeviceActivityName) {
        super.intervalDidEnd(for: activity)
        // All mutations share a file lock with the foreground app. Old callbacks cannot unlock a new session.
        do { try FPShared.locked { try FPShared.clear(matchingMonitor: activity.rawValue) } }
        catch { NSLog("FocusPark monitor cleanup failed: %@", error.localizedDescription) }
    }
}

#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace FocusPark.Editor
{
    // No manual extension-target setup is needed after a Unity iOS export.
    public static class IOSBuild
    {
        [PostProcessBuild(100)]
        public static void Configure(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var nativeRoot = Path.Combine(projectRoot, "Native/iOS");
            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var app = project.GetUnityMainTargetGuid();
            var framework = project.GetUnityFrameworkTargetGuid();
            var bundle = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            if (string.IsNullOrEmpty(bundle)) throw new Exception("Set an iOS bundle identifier before building Focus Park.");
            var group = "group." + bundle + ".shared";
            var team = PlayerSettings.iOS.appleDeveloperTeamID;
            var destination = Path.Combine(output, "FocusParkNative");
            Directory.CreateDirectory(Path.Combine(destination, "Monitor"));
            Directory.CreateDirectory(Path.Combine(destination, "Bridge"));

            AddSource(project, framework, output, nativeRoot, "Bridge/FPShared.swift");
            AddSource(project, framework, output, nativeRoot, "Bridge/FocusParkBlocking.swift");
            var monitorPlist = "FocusParkNative/Monitor/Info.plist";
            File.Copy(Path.Combine(nativeRoot, "Monitor/Info.plist"), Path.Combine(output, monitorPlist), true);
            var monitor = project.TargetGuidByName("FocusParkMonitor");
            if (string.IsNullOrEmpty(monitor))
                monitor = project.AddAppExtension(app, "FocusParkMonitor", bundle + ".monitor", monitorPlist);
            AddSource(project, monitor, output, nativeRoot, "Bridge/FPShared.swift");
            AddSource(project, monitor, output, nativeRoot, "Monitor/FocusParkMonitor.swift");

            foreach (var t in new[] { framework, monitor })
            {
                project.SetBuildProperty(t, "SWIFT_VERSION", "5.0");
                project.SetBuildProperty(t, "CLANG_ENABLE_MODULES", "YES");
                project.SetBuildProperty(t, "IPHONEOS_DEPLOYMENT_TARGET", "16.0");
                project.SetBuildProperty(t, "ENABLE_BITCODE", "NO");
                project.AddFrameworkToProject(t, "FamilyControls.framework", false);
                project.AddFrameworkToProject(t, "ManagedSettings.framework", false);
                project.AddFrameworkToProject(t, "DeviceActivity.framework", false);
                project.AddFrameworkToProject(t, "Foundation.framework", false);
            }
            project.AddFrameworkToProject(framework, "SwiftUI.framework", false);
            project.AddFrameworkToProject(framework, "UIKit.framework", false);
            project.SetBuildProperty(app, "IPHONEOS_DEPLOYMENT_TARGET", "16.0");
            project.SetBuildProperty(app, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            project.SetBuildProperty(framework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.SetBuildProperty(monitor, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(monitor, "SKIP_INSTALL", "YES");
            project.SetBuildProperty(monitor, "FP_APP_GROUP", group);
            project.SetBuildProperty(monitor, "TARGETED_DEVICE_FAMILY", "1,2");
            project.SetBuildProperty(monitor, "CODE_SIGN_STYLE", "Automatic");
            if (!string.IsNullOrEmpty(team)) project.SetBuildProperty(monitor, "DEVELOPMENT_TEAM", team);
            Entitlements(project, app, output, "FocusParkNative/FocusPark.entitlements", group);
            Entitlements(project, monitor, output, "FocusParkNative/Monitor/FocusParkMonitor.entitlements", group);

            var plistPath = Path.Combine(output, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("FPAppGroup", group);
            plist.root.SetString("NSLocalNetworkUsageDescription", "Connect to your Mac on the same Wi-Fi to protect both devices during a focus session.");
            PlistElement atsValue;
            var ats = plist.root.values.TryGetValue("NSAppTransportSecurity", out atsValue)
                ? atsValue.AsDict() : plist.root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsLocalNetworking", true);
            // iOS 17+ supports numeric IP/CIDR exceptions. Scope HTTP to private LAN addresses.
            PlistElement exceptionValue;
            var exceptions = ats.values.TryGetValue("NSExceptionDomains", out exceptionValue)
                ? exceptionValue.AsDict() : ats.CreateDict("NSExceptionDomains");
            foreach (var range in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8" })
            {
                PlistElement entry;
                var rule = exceptions.values.TryGetValue(range, out entry) ? entry.AsDict() : exceptions.CreateDict(range);
                rule.SetBoolean("NSExceptionAllowsInsecureHTTPLoads", true);
            }
            plist.WriteToFile(plistPath);
            project.WriteToFile(projectPath);
            Debug.Log("Focus Park: embedded Device Activity monitor. Register App Group " + group +
                      " and enable Family Controls for both " + bundle + " and " + bundle + ".monitor in your Apple team.");
        }

        static void AddSource(PBXProject project, string target, string output, string nativeRoot, string relative)
        {
            var path = "FocusParkNative/" + relative;
            File.Copy(Path.Combine(nativeRoot, relative), Path.Combine(output, path), true);
            var guid = project.FindFileGuidByProjectPath(path);
            if (string.IsNullOrEmpty(guid)) guid = project.AddFile(path, path, PBXSourceTree.Source);
            project.AddFileToBuild(target, guid);
        }

        static void Entitlements(PBXProject project, string target, string output, string relative, string group)
        {
            var doc = new PlistDocument();
            var previous = project.GetBuildPropertyForAnyConfig(target, "CODE_SIGN_ENTITLEMENTS");
            if (!string.IsNullOrEmpty(previous))
            {
                var existing = Path.IsPathRooted(previous) ? previous : Path.Combine(output, previous);
                if (File.Exists(existing)) doc.ReadFromFile(existing);
            }
            doc.root.SetBoolean("com.apple.developer.family-controls", true);
            PlistElement value;
            PlistElementArray groups;
            if (doc.root.values.TryGetValue("com.apple.security.application-groups", out value)) groups = value.AsArray();
            else groups = doc.root.CreateArray("com.apple.security.application-groups");
            bool hasGroup = false;
            foreach (var item in groups.values) if (item.AsString() == group) hasGroup = true;
            if (!hasGroup) groups.AddString(group);
            doc.WriteToFile(Path.Combine(output, relative));
            project.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", relative);
        }
    }
}
#endif

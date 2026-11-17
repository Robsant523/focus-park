#!/usr/bin/env python3
"""Generate a dependency-free native Xcode probe project for physical-device validation."""
from pathlib import Path
import hashlib
import plistlib
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--bundle-id', default='com.focuspark.probe')
parser.add_argument('--team', default='')
args = parser.parse_args()
root = Path(__file__).resolve().parent
probe = root / 'Probe'
project = probe / 'FocusParkProbe.xcodeproj'
project.mkdir(parents=True, exist_ok=True)
group = 'group.' + args.bundle_id + '.shared'

def ident(name):
    return hashlib.sha1(name.encode()).hexdigest()[:24].upper()

def q(value):
    return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"') + '"'

objects = []
def obj(name, body):
    objects.append(f'{ident(name)} = {{ {body} }};')
    return ident(name)
def arr(items):
    return '(' + ','.join(items) + ',)'

sources = {
    'shared': '../Bridge/FPShared.swift',
    'bridge': '../Bridge/FocusParkBlocking.swift',
    'app': 'FocusParkProbe.swift',
    'monitor': '../Monitor/FocusParkMonitor.swift',
}
for name, path in sources.items():
    obj('file-' + name, f'isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = {q(path)}; sourceTree = "<group>";')
for target, names in [('app', ['shared', 'bridge', 'app']), ('monitor', ['shared', 'monitor'])]:
    builds = []
    for name in names:
        builds.append(obj('build-' + target + '-' + name, f'isa = PBXBuildFile; fileRef = {ident("file-" + name)};'))
    obj('sources-' + target, f'isa = PBXSourcesBuildPhase; buildActionMask = 2147483647; files = {arr(builds)}; runOnlyForDeploymentPostprocessing = 0;')
    obj('frameworks-' + target, 'isa = PBXFrameworksBuildPhase; buildActionMask = 2147483647; files = (); runOnlyForDeploymentPostprocessing = 0;')
    obj('resources-' + target, 'isa = PBXResourcesBuildPhase; buildActionMask = 2147483647; files = (); runOnlyForDeploymentPostprocessing = 0;')

obj('product-app', 'isa = PBXFileReference; explicitFileType = wrapper.application; path = FocusParkProbe.app; sourceTree = BUILT_PRODUCTS_DIR;')
obj('product-monitor', 'isa = PBXFileReference; explicitFileType = "wrapper.app-extension"; path = FocusParkMonitor.appex; sourceTree = BUILT_PRODUCTS_DIR;')
obj('products', f'isa = PBXGroup; children = {arr([ident("product-app"), ident("product-monitor")])}; name = Products; sourceTree = "<group>";')
obj('root', f'isa = PBXGroup; children = {arr([ident("file-" + n) for n in sources] + [ident("products")])}; sourceTree = "<group>";')
obj('embed-file', f'isa = PBXBuildFile; fileRef = {ident("product-monitor")}; settings = {{ ATTRIBUTES = (RemoveHeadersOnCopy,); }};')
obj('embed', f'isa = PBXCopyFilesBuildPhase; buildActionMask = 2147483647; dstPath = ""; dstSubfolderSpec = 13; files = {arr([ident("embed-file")])}; name = "Embed App Extensions"; runOnlyForDeploymentPostprocessing = 0;')
obj('proxy', f'isa = PBXContainerItemProxy; containerPortal = {ident("project")}; proxyType = 1; remoteGlobalIDString = {ident("target-monitor")}; remoteInfo = FocusParkMonitor;')
obj('dependency', f'isa = PBXTargetDependency; target = {ident("target-monitor")}; targetProxy = {ident("proxy")};')

common = {'SDKROOT': 'iphoneos', 'IPHONEOS_DEPLOYMENT_TARGET': '16.0', 'SWIFT_VERSION': '5.0',
          'CLANG_ENABLE_MODULES': 'YES', 'CODE_SIGN_STYLE': 'Automatic', 'DEVELOPMENT_TEAM': args.team,
          'TARGETED_DEVICE_FAMILY': '1,2', 'FP_APP_GROUP': group}
for target, name, bundle, info in [('app', 'FocusParkProbe', args.bundle_id, 'Info.plist'),
                                  ('monitor', 'FocusParkMonitor', args.bundle_id + '.monitor', '../Monitor/Info.plist')]:
    configs = []
    for config in ['Debug', 'Release']:
        settings = dict(common, PRODUCT_NAME=name, PRODUCT_BUNDLE_IDENTIFIER=bundle, INFOPLIST_FILE=info,
                        CODE_SIGN_ENTITLEMENTS=name + '.entitlements', SWIFT_OPTIMIZATION_LEVEL='-Onone' if config == 'Debug' else '-O')
        if target == 'monitor':
            settings.update(APPLICATION_EXTENSION_API_ONLY='YES', SKIP_INSTALL='YES')
        else:
            settings.update(ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES='YES')
        configs.append(obj('config-' + target + '-' + config, f'isa = XCBuildConfiguration; buildSettings = {{ ' + ' '.join(f'{k} = {q(v)};' for k,v in settings.items()) + f' }}; name = {config};'))
    obj('configs-' + target, f'isa = XCConfigurationList; buildConfigurations = {arr(configs)}; defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;')
    phases = [ident('sources-' + target), ident('frameworks-' + target), ident('resources-' + target)]
    if target == 'app': phases.append(ident('embed'))
    deps = arr([ident('dependency')]) if target == 'app' else '()'
    obj('target-' + target, f'isa = PBXNativeTarget; buildConfigurationList = {ident("configs-" + target)}; buildPhases = {arr(phases)}; buildRules = (); dependencies = {deps}; name = {name}; productName = {name}; productReference = {ident("product-" + target)}; productType = {q("com.apple.product-type.application" if target == "app" else "com.apple.product-type.app-extension")};')
    with (probe / (name + '.entitlements')).open('wb') as f:
        plistlib.dump({'com.apple.developer.family-controls': True, 'com.apple.security.application-groups': [group]}, f)

pconfigs = [obj('project-' + c, f'isa = XCBuildConfiguration; buildSettings = {{ }}; name = {c};') for c in ['Debug', 'Release']]
obj('project-configs', f'isa = XCConfigurationList; buildConfigurations = {arr(pconfigs)}; defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;')
obj('project', f'isa = PBXProject; attributes = {{ LastUpgradeCheck = 1600; }}; buildConfigurationList = {ident("project-configs")}; compatibilityVersion = "Xcode 14.0"; developmentRegion = en; hasScannedForEncodings = 0; knownRegions = (en,Base,); mainGroup = {ident("root")}; productRefGroup = {ident("products")}; projectDirPath = ""; projectRoot = ""; targets = {arr([ident("target-app"), ident("target-monitor")])};')
(project / 'project.pbxproj').write_text('// !$*UTF8*$!\n{ archiveVersion = 1; classes = {}; objectVersion = 56; objects = {\n' + '\n'.join(objects) + f'\n}}; rootObject = {ident("project")}; }}\n')
with (probe / 'Info.plist').open('wb') as f:
    plistlib.dump({'CFBundleDisplayName': 'Focus Park Probe', 'CFBundleIdentifier': '$(PRODUCT_BUNDLE_IDENTIFIER)',
                  'CFBundleName': '$(PRODUCT_NAME)', 'CFBundleExecutable': '$(EXECUTABLE_NAME)',
                  'CFBundlePackageType': 'APPL', 'CFBundleShortVersionString': '1.0', 'CFBundleVersion': '1',
                  'UILaunchScreen': {}, 'FPAppGroup': group,
                  'UIApplicationSceneManifest': {'UIApplicationSupportsMultipleScenes': False}}, f)
print(project)

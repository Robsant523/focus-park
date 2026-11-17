#!/usr/bin/env python3
"""Create the native Mac app + embedded Safari extension without extra packages."""
from pathlib import Path
import hashlib
root = Path(__file__).resolve().parent
objects = {}
def key(name): return hashlib.sha1(name.encode()).hexdigest()[:24].upper()
def add(name, value): objects[key(name)] = value; return key(name)
def refs(values): return '(' + ', '.join(values) + ',)'
def settings(d): return '{' + ''.join(f'{k} = "{v}"; ' for k, v in d.items()) + '}'
files = {}
for path in ['Sources/PolicyStore.swift','Sources/Relay.swift','Sources/main.swift','SafariExtension/SafariWebExtensionHandler.swift']:
    files[path] = add(path, f'{{isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = "{path}"; sourceTree = "<group>";}}')
resources = ['manifest.json','background.js','popup.html','popup.js','blocked.html']
for name in resources:
    path = 'SafariExtension/Resources/' + name
    files[path] = add(path, f'{{isa = PBXFileReference; lastKnownFileType = text; path = "{path}"; sourceTree = "<group>";}}')
appProduct = add('appProduct','{isa = PBXFileReference; explicitFileType = wrapper.application; path = FocusParkHelper.app; sourceTree = BUILT_PRODUCTS_DIR;}')
extProduct = add('extProduct','{isa = PBXFileReference; explicitFileType = "wrapper.app-extension"; path = FocusParkSafari.appex; sourceTree = BUILT_PRODUCTS_DIR;}')
products = add('products',f'{{isa = PBXGroup; children = {refs([appProduct,extProduct])}; name = Products; sourceTree = "<group>";}}')
group = add('mainGroup',f'{{isa = PBXGroup; children = {refs(list(files.values()) + [products])}; sourceTree = "<group>";}}')
def phase(name, kind, paths):
    builds = [add(name+p, f'{{isa = PBXBuildFile; fileRef = {files[p]};}}') for p in paths]
    return add(name, f'{{isa = {kind}; buildActionMask = 2147483647; files = {refs(builds)}; runOnlyForDeploymentPostprocessing = 0;}}')
appSources = phase('appSources','PBXSourcesBuildPhase',['Sources/PolicyStore.swift','Sources/Relay.swift','Sources/main.swift'])
extSources = phase('extSources','PBXSourcesBuildPhase',['Sources/PolicyStore.swift','SafariExtension/SafariWebExtensionHandler.swift'])
extResources = phase('extResources','PBXResourcesBuildPhase',['SafariExtension/Resources/'+r for r in resources])
embedFile = add('embedFile', f'{{isa = PBXBuildFile; fileRef = {extProduct}; settings = {{ATTRIBUTES = (RemoveHeadersOnCopy,);}};}}')
embed = add('embed', f'{{isa = PBXCopyFilesBuildPhase; buildActionMask = 2147483647; dstPath = ""; dstSubfolderSpec = 13; files = {refs([embedFile])}; name = "Embed App Extensions"; runOnlyForDeploymentPostprocessing = 0;}}')
proxy = add('proxy', f'{{isa = PBXContainerItemProxy; containerPortal = {key("project")}; proxyType = 1; remoteGlobalIDString = {key("extension")}; remoteInfo = FocusParkSafari;}}')
dependency = add('dependency',f'{{isa = PBXTargetDependency; target = {key("extension")}; targetProxy = {proxy};}}')
def configs(name, extra):
    ids=[]
    for mode in ['Debug','Release']:
        build={'SWIFT_VERSION':'5.0','MACOSX_DEPLOYMENT_TARGET':'13.0','SDKROOT':'macosx','CODE_SIGN_STYLE':'Automatic','CLANG_ENABLE_MODULES':'YES','SWIFT_OPTIMIZATION_LEVEL':'-Onone' if mode=='Debug' else '-O',**extra}
        ids.append(add(name+mode,f'{{isa = XCBuildConfiguration; buildSettings = {settings(build)}; name = {mode};}}'))
    return add(name+'Configs',f'{{isa = XCConfigurationList; buildConfigurations = {refs(ids)}; defaultConfigurationIsVisible = 0; defaultConfigurationName = Release;}}')
appConfig=configs('app',{'PRODUCT_BUNDLE_IDENTIFIER':'com.focuspark.helper','PRODUCT_NAME':'FocusParkHelper','INFOPLIST_FILE':'Info.plist','CODE_SIGN_ENTITLEMENTS':'Helper.entitlements','ENABLE_HARDENED_RUNTIME':'YES'})
extConfig=configs('ext',{'PRODUCT_BUNDLE_IDENTIFIER':'com.focuspark.helper.safari','PRODUCT_NAME':'FocusParkSafari','INFOPLIST_FILE':'SafariExtension/Info.plist','CODE_SIGN_ENTITLEMENTS':'SafariExtension/Extension.entitlements','APPLICATION_EXTENSION_API_ONLY':'YES','SKIP_INSTALL':'YES','ENABLE_HARDENED_RUNTIME':'YES'})
projectConfig=configs('project',{})
add('app',f'{{isa = PBXNativeTarget; buildConfigurationList = {appConfig}; buildPhases = {refs([appSources,embed])}; buildRules = (); dependencies = {refs([dependency])}; name = FocusParkHelper; productName = FocusParkHelper; productReference = {appProduct}; productType = "com.apple.product-type.application";}}')
add('extension',f'{{isa = PBXNativeTarget; buildConfigurationList = {extConfig}; buildPhases = {refs([extSources,extResources])}; buildRules = (); dependencies = (); name = FocusParkSafari; productName = FocusParkSafari; productReference = {extProduct}; productType = "com.apple.product-type.app-extension";}}')
add('project',f'{{isa = PBXProject; attributes = {{LastUpgradeCheck = 1600;}}; buildConfigurationList = {projectConfig}; compatibilityVersion = "Xcode 14.0"; developmentRegion = en; hasScannedForEncodings = 0; knownRegions = (en, Base,); mainGroup = {group}; productRefGroup = {products}; projectDirPath = ""; projectRoot = ""; targets = {refs([key("app"),key("extension")])};}}')
project = root / 'FocusParkMac.xcodeproj'
project.mkdir(exist_ok=True)
(project / 'project.pbxproj').write_text('// !$*UTF8*$!\n{archiveVersion = 1; classes = {}; objectVersion = 56; objects = {\n'+'\n'.join(f'{k} = {v};' for k,v in objects.items())+f'\n}}; rootObject = {key("project")};}}\n')
print(project)

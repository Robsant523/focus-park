/* Native policy is the authority; never report success before Safari installs rules. */
const nativeApp = "com.focuspark.helper";
let inFlight = null;
async function clearRules() {
  const rules = await browser.declarativeNetRequest.getDynamicRules();
  if (rules.length) await browser.declarativeNetRequest.updateDynamicRules({removeRuleIds: rules.map(r => r.id)});
  await browser.storage.local.remove("policyKey");
}
async function reconcile() {
  if (inFlight) return inFlight;
  inFlight = (async () => {
    const policy = await browser.runtime.sendNativeMessage(nativeApp, {action: "policy"});
    if (!policy || policy.error) throw new Error(policy?.error || "Mac helper policy unavailable");
    if (!policy.active || policy.endUnix * 1000 <= Date.now()) {
      await clearRules(); await browser.alarms.clear("focus-end"); return policy;
    }
    const domains = Array.isArray(policy.domains) ? policy.domains : [];
    const stored = await browser.storage.local.get("policyKey");
    const existing = await browser.declarativeNetRequest.getDynamicRules();
    const matches = existing.length === domains.length && domains.every((domain, index) => existing.some(rule => rule.id === index + 1 && rule.action.type === "block" && rule.condition.urlFilter === "||" + domain + "^"));
    if (stored.policyKey !== policy.policyKey || !matches) {
      await browser.declarativeNetRequest.updateDynamicRules({
        removeRuleIds: existing.map(r => r.id),
        addRules: domains.map((domain, index) => ({id: index + 1, priority: 1, action: {type: "block"}, condition: {urlFilter: "||" + domain + "^", resourceTypes: ["main_frame", "sub_frame"]}}))
      });
      await browser.storage.local.set({policyKey: policy.policyKey});
      // Existing pages are already loaded; move selected tabs to a local focus screen.
      const tabs = await browser.tabs.query({});
      for (const tab of tabs) {
        try {
          const hostname = new URL(tab.url).hostname.toLowerCase();
          if (domains.some(domain => hostname === domain || hostname.endsWith("." + domain))) {
            await browser.tabs.update(tab.id, {url: browser.runtime.getURL("blocked.html")});
          }
        } catch (_) { /* Safari may withhold a tab URL without host permission. */ }
      }
    }
    await browser.alarms.create("focus-end", {when: policy.endUnix * 1000});
    await browser.runtime.sendNativeMessage(nativeApp, {action: "ack", policyKey: policy.policyKey});
    return policy;
  })().finally(() => { inFlight = null; });
  return inFlight;
}
function refresh() { reconcile().catch(error => console.warn("Focus Park:", error.message)); }
browser.alarms.onAlarm.addListener(alarm => { if (alarm.name === "focus-end") clearRules().then(refresh); else refresh(); });
let nativePort;
function connectNativePort() {
  try {
    nativePort = browser.runtime.connectNative(nativeApp);
    nativePort.onMessage.addListener(refresh);
    nativePort.onDisconnect.addListener(() => { nativePort = null; setTimeout(connectNativePort, 2000); });
  } catch (_) { setTimeout(connectNativePort, 2000); }
}
connectNativePort();
browser.runtime.onStartup.addListener(refresh);
browser.runtime.onInstalled.addListener(refresh);
browser.tabs.onActivated.addListener(refresh);
browser.runtime.onMessage.addListener(message => message.action === "status" ? reconcile() : undefined);
browser.alarms.create("focus-policy", {periodInMinutes: 1});
refresh();
// While Safari keeps this background context alive, acknowledge new sessions promptly.
setInterval(refresh, 2000);

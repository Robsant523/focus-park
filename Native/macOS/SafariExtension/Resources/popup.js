async function check() {
  try {
    const p = await browser.runtime.sendMessage({action: "status"});
    document.querySelector("#status").textContent = p?.active ? `Blocking ${(p.domains || []).length} selected domains until ${new Date(p.endUnix * 1000).toLocaleTimeString()}.` : "Connected. No active focus session.";
  } catch (e) { document.querySelector("#status").textContent = "Could not connect. Open the Focus Park Mac helper and try again."; }
}
document.querySelector("#refresh").addEventListener("click", check); check();

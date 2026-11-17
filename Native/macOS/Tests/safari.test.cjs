const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
let policy = {active:true,sessionId:'one',endUnix:Date.now()/1000+1500,domains:['example.com'],policyKey:'one'};
let rules=[], storage={}, acknowledgements=[], failUpdate=false;
const event = {addListener() {}};
const browser = {
 runtime:{connectNative:()=>({onMessage:event,onDisconnect:event}),sendNativeMessage:async(_,message)=>{ if(message.action==='ack') acknowledgements.push(message.policyKey); return {...policy}; },onStartup:event,onInstalled:event,onMessage:event,getURL:path=>'safari-web-extension://test/'+path},
 declarativeNetRequest:{getDynamicRules:async()=>rules,updateDynamicRules:async update=>{if(failUpdate)throw Error('Permission denied');rules=rules.filter(r=>!update.removeRuleIds.includes(r.id)).concat(update.addRules||[]);}},
 storage:{local:{get:async()=>({...storage}),set:async value=>Object.assign(storage,value),remove:async key=>{delete storage[key];}}},
 tabs:{query:async()=>[],update:async()=>{},onActivated:event},alarms:{create:async()=>{},clear:async()=>{},onAlarm:event}
};
const context=vm.createContext({browser,console,Date,URL,setInterval(){},setTimeout(){}});
vm.runInContext(fs.readFileSync('SafariExtension/Resources/background.js','utf8'),context);
(async()=>{
 await context.reconcile(); assert.equal(rules.length,1);assert.equal(acknowledgements.at(-1),'one');
 policy={...policy,policyKey:'two'};failUpdate=true;
 await assert.rejects(context.reconcile());assert(!acknowledgements.includes('two'),'Must not acknowledge failed rule installation');
 failUpdate=false;await context.reconcile();assert.equal(acknowledgements.at(-1),'two');
 policy={...policy,active:false,policyKey:'inactive'};await context.reconcile();assert.equal(rules.length,0,'Stop must remove persisted rules');
 policy={...policy,active:true,policyKey:'expired',endUnix:Date.now()/1000-1};await context.reconcile();assert.equal(rules.length,0,'Expiry must remove rules');
 console.log('PASS: Safari rule application, failed-install acknowledgement, stop, expiry');
})().catch(error=>{console.error(error);process.exit(1);});

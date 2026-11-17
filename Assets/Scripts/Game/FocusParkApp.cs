using System;
using System.Collections;
using UnityEngine;
using FocusPark.Core;
using FocusPark.Platform;

namespace FocusPark.Game
{
    public sealed class FocusParkApp : MonoBehaviour
    {
        ParkState state;
        ParkSave save;
        ParkView park;
        BlockingBridge blocking;
        bool practice = true, busy, polling, setup, choosing, endConfirm, saveBroken;
        string task = "Read one chapter", notice = "Small steps. A wonderful place.", selected = "carousel", fatal = "";
        int px=2,pz=2,rotation;
        float nextPoll;
        long settlementAt=-1;
        GUIStyle title,heading,body,small,button,input,number;
        Texture2D panel,navy,coral,mint;
        float width,height,scale;
        Rect sidebar;
        Vector2 scroll;
        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static readonly Color Ink=new Color(.12f,.23f,.29f), Muted=new Color(.38f,.46f,.47f), Paper=new Color(.98f,.97f,.91f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if(FindFirstObjectByType<FocusParkApp>()==null) new GameObject("Focus Park").AddComponent<FocusParkApp>(); }
        void Awake()
        {
            Application.targetFrameRate=30;
            blocking=gameObject.AddComponent<BlockingBridge>();
            blocking.MacHost=PlayerPrefs.GetString("macHost","127.0.0.1");
            // Pairing tokens are deliberately kept in memory. Enter once each app launch.
            park=gameObject.AddComponent<ParkView>(); park.Initialize();
            LoadPark(PlayerPrefs.GetInt("realPark",0)==0);
        }
        void LoadPark(bool usePractice)
        {
            practice=usePractice; PlayerPrefs.SetInt("realPark",practice?0:1); PlayerPrefs.Save(); save=new ParkSave(practice);
            try { state=save.Load(); saveBroken=false; fatal=""; }
            catch(Exception e) { fatal=e.Message; saveBroken=true; state=new ParkState(); }
            park.Refresh(state); choosing=false; scroll=Vector2.zero; settlementAt=-1; park.Preview(null,0,0,0,false);
            notice=practice?"Practice park · 10 seconds previews 25 minutes.":"Your real park · one ticket per focus minute.";
            blocking.ReadPhone();
            if(!practice && state.session==null && blocking.Phone.active) blocking.StopPhone();
        }
        bool Persist(Action mutate)
        {
            if(saveBroken) return false;
            string before=JsonUtility.ToJson(state);
            try { mutate(); save.Write(state); return true; }
            catch(Exception e) { state=JsonUtility.FromJson<ParkState>(before); notice="Could not save: "+e.Message; return false; }
        }
        void Update()
        {
            if(state==null || saveBroken) return;
            park.Frame(Screen.height>Screen.width);
            if(state.session!=null && Now>=state.session.endUnix && !busy) Finish(false);
            if(!practice && Time.unscaledTime>=nextPoll && !busy && !polling)
            {
                nextPoll=Time.unscaledTime+6;
                StartCoroutine(CheckProtection());
            }
        }
        IEnumerator CheckProtection()
        {
            if (polling || busy) yield break;
            polling=true;
            blocking.ReadPhone();
            yield return blocking.MacRequest("status");
            if(state.session!=null && Now<state.session.endUnix)
            {
                bool phoneLost=!blocking.Phone.authorized || !blocking.Phone.active || blocking.Phone.sessionId!=state.session.id;
                bool macLost=string.IsNullOrEmpty(blocking.NetworkError) && (!blocking.Mac.active || blocking.Mac.sessionId!=state.session.id || !blocking.Mac.ready);
                if(phoneLost || macLost) { polling=false; Finish(true); if(state.session==null) notice="Protection stopped. Your completed minutes were saved."; yield break; }
                if(!string.IsNullOrEmpty(blocking.NetworkError)) notice="Mac connection unavailable. Its local timer still expires on schedule.";
            }
            polling=false;
        }
        void StartFocus()
        {
            if(busy || polling || state.session!=null || saveBroken) return;
            FocusSession pending;
            try { pending=ParkRules.Begin(state,task,Now,25,practice); }
            catch(Exception e) { notice=e.Message; return; }
            if(practice)
            {
                if(Persist(()=>ParkRules.Activate(state,pending))) { setup=false; notice="Practice session · your carousel is on its way."; }
            }
            else StartCoroutine(StartProtected(pending));
        }
        IEnumerator StartProtected(FocusSession pending)
        {
            busy=true; notice="Checking both devices…";
            blocking.ReadPhone();
            if(!blocking.IsPhone || !blocking.Phone.authorized || blocking.Phone.SelectionCount==0)
            { notice="On your iPhone, grant Screen Time access and choose distractions first."; busy=false; setup=true; yield break; }
            yield return blocking.MacRequest("status");
            if(!blocking.Mac.available || !blocking.Mac.configured || blocking.Mac.SelectionCount==0 || !string.IsNullOrEmpty(blocking.NetworkError))
            { notice=string.IsNullOrEmpty(blocking.NetworkError)?"Configure at least one distraction in the Mac helper.":blocking.NetworkError; busy=false; setup=true; yield break; }
            blocking.StartPhone(pending.id,pending.endUnix);
            yield return blocking.MacRequest("start",pending.id,pending.endUnix);
            float deadline=Time.realtimeSinceStartup+15;
            bool acknowledged=false;
            while(Time.realtimeSinceStartup<deadline)
            {
                blocking.ReadPhone();
                yield return blocking.MacRequest("status");
                acknowledged=blocking.Phone.authorized && blocking.Phone.active && blocking.Phone.sessionId==pending.id && blocking.Mac.active && blocking.Mac.sessionId==pending.id && blocking.Mac.ready;
                if(acknowledged) break;
                yield return new WaitForSecondsRealtime(.7f);
            }
            if(acknowledged && Persist(()=>ParkRules.Activate(state,pending)))
            { notice="Both devices confirmed. Your next 25 minutes are yours."; setup=false; }
            else
            {
                blocking.StopPhone(); yield return blocking.MacRequest("stop",pending.id);
                notice="Session did not start. "+blocking.Phone.error+" "+blocking.Mac.message+" "+blocking.NetworkError;
            }
            busy=false;
        }
        void Finish(bool early)
        {
            if(state.session==null) return;
            bool wasPractice=state.session.practice;
            if(settlementAt<0) settlementAt=Now;
            SessionReceipt receipt=null;
            if(!wasPractice) { blocking.StopPhone(); StartCoroutine(StopMac(state.session.id)); }
            if(!Persist(()=>receipt=ParkRules.Settle(state,settlementAt,early))) { notice="Protection released. Could not save your reward; keep the app open and retry."; return; }
            if(receipt==null) return;
            settlementAt=-1;
            endConfirm=false;
            notice=(receipt.completed?"Well done. ":"You can begin again. ")+"+"+receipt.minutes+" "+(wasPractice?"practice ":"")+"tickets saved.";
        }
        IEnumerator StopMac(string sessionId) { busy=true; yield return blocking.MacRequest("stop",sessionId); busy=false; }
        void Preview()
        {
            bool valid=ParkRules.CanPlace(state,selected,px,pz,rotation,out _);
            park.Preview(selected,px,pz,rotation,valid);
        }
        Texture2D Solid(Color c) { var t=new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }
        void Styles()
        {
            if(panel!=null) return;
            panel=Solid(Paper); navy=Solid(Ink); coral=Solid(new Color(.94f,.44f,.34f)); mint=Solid(new Color(.77f,.88f,.79f));
            title=new GUIStyle(GUI.skin.label){fontSize=44,fontStyle=FontStyle.Bold,wordWrap=true}; title.normal.textColor=Ink;
            heading=new GUIStyle(title){fontSize=24};
            body=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true}; body.normal.textColor=Ink;
            small=new GUIStyle(body){fontSize=15}; small.normal.textColor=Muted;
            number=new GUIStyle(title){fontSize=64};
            button=new GUIStyle(GUI.skin.button){fontSize=18,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,wordWrap=true,border=new RectOffset(),padding=new RectOffset(12,12,8,8)};
            button.normal.background=navy;button.normal.textColor=Paper;button.hover.background=coral;button.hover.textColor=Paper;button.active.background=coral;
            input=new GUIStyle(GUI.skin.textField){fontSize=19,padding=new RectOffset(12,12,12,12)};
            input.normal.background=mint;input.normal.textColor=Ink;input.focused.background=mint;input.focused.textColor=Ink;
        }
        bool Button(float x,float y,float w,string text,Action action,float h=46)
        {
            bool clicked=GUI.Button(new Rect(x,y,w,h),text,button);
            if(clicked) action(); return clicked;
        }
        void Label(float x,float y,float w,float h,string text,GUIStyle style=null) { GUI.Label(new Rect(x,y,w,h),text,style??body); }
        void OnGUI()
        {
            Styles();
            bool portrait=Screen.height>Screen.width;
            scale=portrait?Screen.width/800f:Mathf.Min(Screen.width/1440f,Screen.height/900f);
            width=Screen.width/scale;height=Screen.height/scale;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
            // Respect notches without squeezing the 3D park.
            float safeTop=(Screen.height-Screen.safeArea.yMax)/scale;
            sidebar=portrait?new Rect(0,height*.57f,width,height*.43f):new Rect(0,0,width*.30f,height);
            GUI.DrawTexture(sidebar,panel);
            float contentHeight=setup?1100:choosing?930:900;
            scroll=GUI.BeginScrollView(sidebar,scroll,new Rect(0,0,sidebar.width-20,contentHeight));
            float x=28, y=26+(portrait?0:safeTop), w=sidebar.width-76;
            if(portrait) { title.fontSize=36; number.fontSize=52; } else { title.fontSize=44; number.fontSize=64; }
            if(saveBroken)
            {
                Label(x,y,w,65,"Park save needs attention",heading); Label(x,y+75,w,250,fatal,body); GUI.EndScrollView(); return;
            }
            if(setup) { DrawSetup(x,y,w,portrait); GUI.EndScrollView(); return; }
            if(choosing) { DrawBuild(x,y,w,portrait); GUI.EndScrollView(); return; }
            Label(x,y,w,25,practice?"FOCUS PARK  /  PRACTICE":"FOCUS PARK  /  YOUR TIME",small);y+=32;
            Label(x,y,w,100,state.session!=null?"A little focus.\nA little magic.":"Make time.\nMake a park.",title);y+=portrait?105:125;
            if(state.session!=null)
            {
                var s=state.session;
                long remaining=Math.Max(0,s.endUnix-Now);
                Label(x,y,w,80,(remaining/60).ToString("00")+":"+(remaining%60).ToString("00"),number);y+=85;
                Label(x,y,w,65,s.task,heading);y+=68;
                Label(x,y,w,65,practice?"10-second preview · rewards stay in this practice park.":"iPhone shields + Mac helper\nYou can put your phone down.",small);y+=70;
                if(endConfirm) { Label(x,y,w,45,"Keep completed minutes and finish?",small); y+=48;
                    Button(x,y,w*.48f,"Finish",()=>Finish(true)); Button(x+w*.52f,y,w*.48f,"Keep going",()=>endConfirm=false); }
                else Button(x,y,w,"Finish early",()=>endConfirm=true);
            }
            else
            {
                Label(x,y,w,40,state.tickets+" tickets",heading);y+=43;
                Label(x,y,w,28,practice?"Your practice wallet":"From "+state.lifetimeMinutes+" focused minutes",small);y+=portrait?34:46;
                Label(x,y,w,28,"What will you focus on?",small);y+=30;
                task=GUI.TextField(new Rect(x,y,w,50),task,100,input);y+=62;
                GUI.enabled=!busy && !polling;
                Button(x,y,w,busy?"Checking devices…":practice?"Try a 10-second session":"Focus for 25 minutes",StartFocus);y+=57;
                Button(x,y,w,"Build your park",()=>{choosing=true;scroll=Vector2.zero;Preview();});y+=57;
                Button(x,y,w,practice?"Set up real protection":"Protection settings",()=>{setup=true;scroll=Vector2.zero;});y+=55;
                GUI.enabled=true;
            }
            Label(x,Mathf.Max(y+16,contentHeight-110),w,95,notice,small);
            GUI.EndScrollView();
            if(!portrait)
            {
                Label(sidebar.xMax+35,35,600,35,"YOUR CORNER OF THE WORLD",small);
                Label(sidebar.xMax+35,75,600,40,state.attractions.Count+" attractions  ·  Built one session at a time",heading);
                Label(sidebar.xMax+35,height-50,700,30,practice?"Practice mode · no apps or websites are blocked":"Build between sessions. Let your time do the growing.",small);
            }
        }
        void DrawSetup(float x,float y,float w,bool portrait)
        {
            Label(x,y,w,45,"Protect your focus",heading);y+=50;
            Label(x,y,w,65,"Protected sessions start on your iPhone. Keep both devices on the same trusted Wi-Fi.",small);y+=70;
            Button(x,y,w,"1. Allow Screen Time",()=>blocking.Authorize());y+=54;
            Button(x,y,w,"2. Choose iPhone distractions",()=>blocking.Pick());y+=54;
            blocking.ReadPhone();
            Label(x,y,w,45,blocking.IsPhone?(blocking.Phone.authorized?"iPhone authorized · "+blocking.Phone.SelectionCount+" selections":"iPhone needs authorization"):"Signed iPhone build required for phone shields.",small);y+=50;
            if(!portrait) { Button(x,y,w,"Open Mac helper settings",()=>blocking.ConfigureMac()); y+=54; }
            Label(x,y,w,25,"Mac IP address or hostname",small);y+=27;
            blocking.MacHost=GUI.TextField(new Rect(x,y,w,44),blocking.MacHost,100,input);y+=49;
            Label(x,y,w,25,"Pairing token from Mac helper",small);y+=27;
            blocking.Token=GUI.PasswordField(new Rect(x,y,w,44),blocking.Token,'•',128,input);y+=53;
            GUI.enabled=!busy && !polling;
            Button(x,y,w,busy?"Checking…":"Check devices",()=>{PlayerPrefs.SetString("macHost",blocking.MacHost);StartCoroutine(CheckProtection());});y+=54;
            Label(x,y,w,70,string.IsNullOrEmpty(blocking.NetworkError)?"Mac: "+(blocking.Mac.configured?blocking.Mac.SelectionCount+" selections":"not checked")+". "+blocking.Mac.message:blocking.NetworkError,small);y+=75;
            Button(x,y,w,"Use real park",()=>{if(state.session==null) LoadPark(false);setup=false;scroll=Vector2.zero;});y+=54;
            Button(x,y,w,"Back to "+(practice?"practice":"park"),()=>{setup=false;scroll=Vector2.zero;});y+=54;
            if(!practice) Button(x,y,w,"Open practice park",()=>{LoadPark(true);setup=false;scroll=Vector2.zero;});
        }
        void DrawBuild(float x,float y,float w,bool portrait)
        {
            Label(x,y,w,40,"Build something lovely",heading);y+=46;
            Label(x,y,w,30,state.tickets+" "+(practice?"practice ":"")+"tickets available",small);y+=38;
            foreach(var item in ParkRules.Catalog)
            {
                Button(x,y,w,(selected==item.Id?"•  ":"")+item.Name+"  /  "+item.Cost,()=>{selected=item.Id;Preview();});y+=54;
            }
            Label(x,y,w,60,ParkRules.Find(selected).Description,small);y+=63;
            Label(x,y,w,30,"Position  "+(px+1)+", "+(pz+1),heading);y+=37;
            float q=(w-18)/4;
            Button(x,y,q,"←",()=>{px=Mathf.Max(0,px-1);Preview();});
            Button(x+q+6,y,q,"→",()=>{px=Mathf.Min(11,px+1);Preview();});
            Button(x+(q+6)*2,y,q,"↓",()=>{pz=Mathf.Max(0,pz-1);Preview();});
            Button(x+(q+6)*3,y,q,"↑",()=>{pz=Mathf.Min(11,pz+1);Preview();});y+=55;
            Button(x,y,w,"Rotate 90°",()=>{rotation=(rotation+1)%4;Preview();});y+=55;
            bool valid=ParkRules.CanPlace(state,selected,px,pz,rotation,out var reason);
            Label(x,y,w,50,reason,small);y+=52;
            GUI.enabled=valid;
            Button(x,y,w,"Place for "+ParkRules.Find(selected).Cost+" tickets",()=>{
                if(Persist(()=>ParkRules.Place(state,selected,px,pz,rotation))) { park.Refresh(state); notice="A new attraction, earned by you.";Preview(); }
            });y+=55;
            GUI.enabled=true;
            Button(x,y,w,"Back to focus",()=>{choosing=false;scroll=Vector2.zero;park.Preview(null,0,0,0,false);});
        }
    }
}

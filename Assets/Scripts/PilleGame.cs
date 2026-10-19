using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    public sealed class PilleGame : MonoBehaviour
    {
        public MatchSimulation simulation;
        public Packet view;
        public bool authority, session, offline;
        public int localId=-1;
        public string status="Crea una sala LAN o practica con bots.";
        LanTransport network;
        readonly Dictionary<int,PlayerMotor> replicas=new Dictionary<int,PlayerMotor>();
        readonly Dictionary<int,int> peerPlayers=new Dictionary<int,int>();
        int nextId=1, botId=100;
        string playerName="Jugador", address="127.0.0.1", addresses;
        MatchMode selectedMode=MatchMode.Infection;
        float nextSend, nextSnapshot, messageUntil;
        bool jump, dash, interact, menu, paused, mute, showLan, practiceAsInfected;
        Vector3 movement;
        Camera gameCamera;
        AudioSource sound;
        AudioClip tagSound;
        string previousMessage;
        GUIStyle title, subtitle, heading, body, small, button, inputStyle, row, timer, chip, centeredSmall;
        Texture2D panel, accent, muted, coral;
        readonly string[] modes={"CLÁSICO","INFECCIÓN","RELEVO"};
        readonly string[] descriptions={"Pasa la mancha. Gana quien acumule menos tiempo pillando.","Supervivientes: escapen y salven a sus compañeros.\nInfectados: atrapen a todos antes de que termine el tiempo.","Pasa la bomba antes de 20 s. El último superviviente gana."};

        void Awake()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;Time.fixedDeltaTime=.02f;
            QualitySettings.vSyncCount=0;
            ArenaBuilder.Build();
            SpeedPad.EnsurePads();
            gameCamera=Camera.main;
            if(gameCamera==null){var go=new GameObject("Main Camera");go.tag="MainCamera";gameCamera=go.AddComponent<Camera>();go.AddComponent<AudioListener>();}
            gameCamera.fieldOfView=52;gameCamera.nearClipPlane=.1f;gameCamera.farClipPlane=140;
            gameCamera.backgroundColor=new Color(.055f,.075f,.095f);gameCamera.clearFlags=CameraClearFlags.SolidColor;
            if(FindObjectOfType<Light>()==null) {
                var light=new GameObject("Luz_Arena").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.shadows=LightShadows.Soft;
                light.transform.rotation=Quaternion.Euler(50,-30,0);
            }
            RenderSettings.ambientLight=new Color(.57f,.64f,.7f);
            gameCamera.transform.position=new Vector3(28,33,-32);gameCamera.transform.LookAt(Vector3.zero);
            playerName=PlayerPrefs.GetString("Pille.Name","Jugador");address=PlayerPrefs.GetString("Pille.Address","127.0.0.1");
            addresses=LanTransport.LocalAddresses();
            sound=gameObject.AddComponent<AudioSource>();tagSound=CreateTone();
            StartCoroutine(CommandLine());
        }
        IEnumerator CommandLine()
        {
            yield return null;
            string[] args=Environment.GetCommandLineArgs();
            if(args.Contains("-pilleInfection"))SetMode(MatchMode.Infection);
            if(args.Contains("-pilleTest")) {gameObject.AddComponent<RuntimeVerification>();yield break;}
            if(args.Contains("-pilleBotTest")) {gameObject.AddComponent<BotVerification>();yield break;}
            if(args.Contains("-pillePolishPreview")) {gameObject.AddComponent<PolishPreview>();yield break;}
            if(args.Contains("-pilleHost")) {Host(false);AddBot();}
            if(args.Contains("-pilleClient")) {address="127.0.0.1";Join();}
            if(args.Contains("-pillePreview")) {Host(true);StartRound();}
            if(args.Contains("-pilleQuickPlay")) {SetMode(MatchMode.Infection);Host(true);StartRound();}
            if(args.Contains("-pilleCapture")||args.Contains("-pilleNetAudit"))gameObject.AddComponent<PreviewCapture>();
        }
        public void Host(bool practice)
        {
            Leave();
            try {
                if(!practice) {network=new LanTransport();network.Host(Rules.Port);}
                authority=true;offline=practice;session=true;localId=0;menu=false;
                simulation=new MatchSimulation();simulation.mode=selectedMode;simulation.Add(0,CleanName(playerName),false);
                if(practice)for(int i=0;i<3;i++)AddBot();
                view=simulation.Snapshot();status=practice?"Práctica local":"Sala LAN abierta en puerto 7777.";
                PlayerPrefs.SetString("Pille.Name",playerName);PlayerPrefs.Save();
            }catch(Exception ex){Leave();status="No se pudo crear la sala: "+ex.Message;}
        }
        public void AddBot()
        {
            if(!authority||simulation==null||simulation.phase==MatchPhase.Playing||simulation.players.Count>=Rules.MaxPlayers)return;
            int id=botId++;simulation.Add(id,"Bot "+(id-99),true);view=simulation.Snapshot();
        }
        void RemoveBot()
        {
            if(simulation.phase==MatchPhase.Playing)return;
            var bot=simulation.players.Values.LastOrDefault(x=>x.state.bot);
            if(bot!=null)simulation.Remove(bot.state.id);view=simulation.Snapshot();
        }
        public void StartRound()
        {
            if(!authority)return;
            int preferred=-1;
            if(offline&&selectedMode==MatchMode.Infection) {
                var bot=simulation.players.Values.FirstOrDefault(p=>p.state.bot);
                preferred=practiceAsInfected?localId:bot!=null?bot.state.id:-1;
            }
            simulation.StartRound(selectedMode,preferred);view=simulation.Snapshot();menu=false;paused=false;
        }
        public void SetMode(MatchMode mode){selectedMode=mode;}
        void Join()
        {
            Leave();network=new LanTransport();session=true;authority=false;offline=false;localId=-1;
            status="Conectando con "+address+"…";network.Join(address.Trim(),Rules.Port);
            PlayerPrefs.SetString("Pille.Name",playerName);PlayerPrefs.SetString("Pille.Address",address);PlayerPrefs.Save();
        }
        static string CleanName(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Jugador";
            var clean=new string(value.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='_'||c=='-').Take(16).ToArray()).Trim();
            return clean.Length==0?"Jugador":clean;
        }
        void Update()
        {
            PumpNetwork();
            if((Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.F1))&&session) {menu=!menu;if(offline)paused=menu;}
            if(Input.GetKeyDown(KeyCode.M))mute=!mute;
            movement=Vector3.zero;interact=false;
            if(session&&view!=null&&view.phase==(int)MatchPhase.Playing&&!menu) {
                float x=(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0);
                float z=(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0);
                var forward=gameCamera.transform.forward;forward.y=0;forward.Normalize();
                var right=gameCamera.transform.right;right.y=0;right.Normalize();
                movement=Vector3.ClampMagnitude(right*x+forward*z,1);
                jump|=Input.GetKeyDown(KeyCode.Space);dash|=Input.GetKeyDown(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.RightShift);
                var local=view.players.FirstOrDefault(p=>p.id==localId);
                interact=view.mode==(int)MatchMode.Infection&&local!=null&&local.alive&&!local.tagged&&!local.trapped&&Input.GetKey(KeyCode.E);
            }
            else {jump=false;dash=false;}
            if(!authority&&session&&localId>=0&&Time.unscaledTime>=nextSend) {
                Send(0,new Packet{type="input",input=new InputFrame{x=movement.x,z=movement.z,jump=jump,dash=dash,interact=interact}});
                jump=false;dash=false;nextSend=Time.unscaledTime+.02f;
            }
            if(view!=null&&previousMessage!=view.message) {
                if(previousMessage!=null&&!mute&&view.phase==(int)MatchPhase.Playing)sound.PlayOneShot(tagSound,.17f);
                previousMessage=view.message;messageUntil=Time.unscaledTime+3;
            }
            var all=authority&&simulation!=null?simulation.players:replicas;
            foreach(var p in all.Values)p.Visual(!authority,localId);
            SpeedPad.ApplyAll(view!=null?view.pads:null,session&&view!=null&&view.phase==(int)MatchPhase.Playing);
        }
        void FixedUpdate()
        {
            if(!authority||simulation==null||paused)return;
            if(simulation.players.TryGetValue(localId,out var player)) {
                player.input=new InputFrame{x=movement.x,z=movement.z,jump=jump,dash=dash,interact=interact};player.lastInput=Time.realtimeSinceStartup;
            }
            jump=false;dash=false;
            simulation.Tick(Time.fixedDeltaTime);view=simulation.Snapshot();
            if(network!=null&&Time.unscaledTime>=nextSnapshot) {network.Broadcast(JsonUtility.ToJson(view));nextSnapshot=Time.unscaledTime+.05f;}
        }
        void LateUpdate()
        {
            var all=authority&&simulation!=null?simulation.players:replicas;
            Vector3 target=Vector3.zero;
            bool playing=view!=null&&view.phase==(int)MatchPhase.Playing;
            if(playing&&all.TryGetValue(localId,out var p)) {
                target=p.transform.position;
                target.x=Mathf.Clamp(target.x,-7,7);target.z=Mathf.Clamp(target.z,-6,8);target.y=Mathf.Clamp(target.y,0,2);
            }
            var desired=playing?target+new Vector3(0,23,-13):new Vector3(28,33,-32);
            gameCamera.transform.position=Vector3.Lerp(gameCamera.transform.position,desired,1-Mathf.Exp(-5*Time.deltaTime));
            var look=Quaternion.LookRotation((target+Vector3.up*.2f)-gameCamera.transform.position);
            gameCamera.transform.rotation=Quaternion.Slerp(gameCamera.transform.rotation,look,1-Mathf.Exp(-9*Time.deltaTime));
        }
        void PumpNetwork()
        {
            if(network==null)return;
            LanTransport.Event evt;int count=0;
            while(count++<256&&network!=null&&network.events.TryDequeue(out evt)) {
                if(evt.kind=="error") {status="Red: "+evt.json;if(!authority){Leave();status="No se pudo conectar: "+evt.json;}continue;}
                if(evt.kind=="connected") {if(!authority)Send(0,new Packet{type="hello",name=CleanName(playerName),version=Rules.ProtocolVersion});continue;}
                if(evt.kind=="disconnected") {
                    if(authority) {if(peerPlayers.TryGetValue(evt.peer,out var id)){simulation.Remove(id);peerPlayers.Remove(evt.peer);}}
                    else {Leave();status="Se perdió la conexión con el anfitrión. Puedes volver a unirte.";}
                    continue;
                }
                try {
                    var packet=JsonUtility.FromJson<Packet>(evt.json);if(packet==null)continue;
                    if(authority)HandleServer(evt.peer,packet);else HandleClient(packet);
                }catch(Exception ex){Debug.LogWarning("Paquete LAN descartado: "+ex.Message);}
            }
        }
        void HandleServer(int peer,Packet packet)
        {
            if(packet.type=="hello"&&!peerPlayers.ContainsKey(peer)) {
                if(packet.version!=Rules.ProtocolVersion) {
                    Send(peer,new Packet{type="rejected",message="Versiones incompatibles. Todos deben abrir la misma versión del juego con supervelocidad y rescates."});return;
                }
                if(simulation.phase==MatchPhase.Playing||simulation.players.Count>=Rules.MaxPlayers) {
                    Send(peer,new Packet{type="rejected",message="La sala está llena o la ronda está en curso. Intenta al terminar."});return;
                }
                int id=nextId++;peerPlayers[peer]=id;simulation.Add(id,CleanName(packet.name),false);
                Send(peer,new Packet{type="welcome",id=id,version=Rules.ProtocolVersion});Send(peer,simulation.Snapshot());
            }
            if(packet.type=="input"&&packet.input!=null&&peerPlayers.TryGetValue(peer,out var playerId)&&simulation.players.TryGetValue(playerId,out var p)) {
                var v=packet.input;
                if(float.IsNaN(v.x)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.z))return;
                p.input.x=Mathf.Clamp(v.x,-1,1);p.input.z=Mathf.Clamp(v.z,-1,1);
                p.input.jump|=v.jump;p.input.dash|=v.dash;p.input.interact=v.interact;p.lastInput=Time.realtimeSinceStartup;
            }
        }
        void HandleClient(Packet packet)
        {
            if(packet.type=="welcome") {
                if(packet.version!=Rules.ProtocolVersion) {Leave();status="Versiones incompatibles. Todos deben abrir la misma versión del juego.";return;}
                localId=packet.id;status="Conectado. Esperando al anfitrión.";
            }
            if(packet.type=="rejected"){Leave();status=packet.message;return;}
            if(packet.type!="state")return;
            view=packet;
            if(packet.players==null)return;
            foreach(var state in packet.players) {
                if(!replicas.ContainsKey(state.id))replicas[state.id]=PlayerMotor.Create(state,false);
                replicas[state.id].Apply(state);
            }
            foreach(var id in replicas.Keys.Where(id=>!packet.players.Any(s=>s.id==id)).ToArray()) {Destroy(replicas[id].gameObject);replicas.Remove(id);}
        }
        void Send(int peer,Packet packet){if(network!=null)network.Send(peer,JsonUtility.ToJson(packet));}
        public void Leave()
        {
            if(network!=null){network.Dispose();network=null;}
            if(simulation!=null){simulation.Clear();simulation=null;}
            foreach(var p in replicas.Values)if(p!=null)Destroy(p.gameObject);replicas.Clear();peerPlayers.Clear();
            session=false;authority=false;offline=false;localId=-1;view=null;menu=false;paused=false;nextId=1;botId=100;
            jump=false;dash=false;interact=false;movement=Vector3.zero;previousMessage=null;messageUntil=0;
            SpeedPad.ApplyAll(null,false);
        }
        void OnDestroy(){Leave();if(tagSound!=null)Destroy(tagSound);}
        static AudioClip CreateTone()
        {
            const int rate=22050;float[] samples=new float[4410];
            for(int i=0;i<samples.Length;i++){float t=(float)i/rate;samples[i]=Mathf.Sin(t*2*Mathf.PI*(700-t*1600))*(1-(float)i/samples.Length)*.4f;}
            var clip=AudioClip.Create("Contagio",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void Styles()
        {
            if(title!=null)return;
            panel=Texture(new Color(.035f,.055f,.072f,.86f));accent=Texture(new Color(.25f,.85f,.82f));muted=Texture(new Color(.15f,.20f,.24f));coral=Texture(new Color(1,.35f,.23f));
            title=Style(58,FontStyle.Bold,Color.white);subtitle=Style(12,FontStyle.Bold,new Color(.3f,.87f,.85f));
            heading=Style(23,FontStyle.Bold,Color.white);body=Style(16,FontStyle.Normal,new Color(.84f,.89f,.92f));
            small=Style(12,FontStyle.Normal,new Color(.70f,.78f,.82f));row=Style(15,FontStyle.Normal,Color.white);
            timer=Style(24,FontStyle.Bold,Color.white);timer.alignment=TextAnchor.MiddleCenter;timer.wordWrap=false;
            chip=Style(15,FontStyle.Bold,Color.white);chip.alignment=TextAnchor.MiddleLeft;chip.wordWrap=false;
            centeredSmall=Style(11,FontStyle.Normal,new Color(.70f,.78f,.82f));centeredSmall.alignment=TextAnchor.MiddleCenter;centeredSmall.wordWrap=false;
            button=new GUIStyle(GUI.skin.button){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,border=new RectOffset(0,0,0,0)};
            button.normal.background=muted;button.normal.textColor=Color.white;button.hover.background=accent;button.hover.textColor=new Color(.035f,.07f,.09f);button.active.background=accent;
            inputStyle=new GUIStyle(GUI.skin.textField){fontSize=18,padding=new RectOffset(12,12,10,10)};inputStyle.normal.background=muted;inputStyle.normal.textColor=Color.white;inputStyle.focused.background=muted;inputStyle.focused.textColor=Color.white;
        }
        static GUIStyle Style(int size,FontStyle font,Color color){var style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=font,wordWrap=true};style.normal.textColor=color;return style;}
        static Texture2D Texture(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
        void Text(float x,float y,float w,float h,string value,GUIStyle style){GUI.Label(new Rect(x,y,w,h),value,style);}
        bool Button(float x,float y,float w,float h,string text){return GUI.Button(new Rect(x,y,w,h),text,button);}
        void Panel(float x,float y,float w,float h){GUI.DrawTexture(new Rect(x,y,w,h),panel);}
        void OnGUI(){DrawInterface();}
        public void DrawInterface()
        {
            Styles();float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            float ox=(Screen.width-1280*scale)/2,oy=(Screen.height-720*scale)/2;
            var originalMatrix=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(new Vector3(ox,oy,0),Quaternion.identity,Vector3.one*scale);
            if(!session) MainMenu();
            else if(view==null) {Panel(330,230,620,230);Text(365,264,550,80,status,heading);if(Button(365,380,550,48,"CANCELAR"))Leave();}
            else if(view.phase==(int)MatchPhase.Lobby) Lobby();
            else if(view.phase==(int)MatchPhase.Results) Results();
            else Hud();
            if(menu&&session&&view!=null&&view.phase==(int)MatchPhase.Playing) PauseMenu();
            GUI.matrix=originalMatrix;
        }
        void MainMenu()
        {
            Panel(54,42,524,636);
            Text(86,64,450,25,"CORRE. ATRAPA. SALVA A TU EQUIPO.",subtitle);
            Text(81,96,480,80,"PILLE PILLE",title);
            GUI.DrawTexture(new Rect(86,181,62,3),accent);
            Text(86,204,450,22,"TU NOMBRE",small);
            playerName=GUI.TextField(new Rect(86,230,460,42),playerName,16,inputStyle);
            Text(86,287,460,22,"MODO DE JUEGO",small);
            for(int i=0;i<3;i++)if(Button(86+i*156,313,148,36,(selectedMode==(MatchMode)i?"● ":"")+modes[i]))selectedMode=(MatchMode)i;
            if(selectedMode==MatchMode.Infection) {
                Text(86,365,460,20,"TU PAPEL EN PRÁCTICA",small);
                if(Button(86,391,224,35,(!practiceAsInfected?"● ":"")+"SUPERVIVIENTE"))practiceAsInfected=false;
                if(Button(322,391,224,35,(practiceAsInfected?"● ":"")+"INFECTADO"))practiceAsInfected=true;
            } else Text(86,369,460,49,descriptions[(int)selectedMode],body);
            if(Button(86,447,460,52,"JUGAR CON BOTS")){Host(true);StartRound();}
            if(Button(86,513,460,35,showLan?"−  OCULTAR OPCIONES LAN":"+  JUGAR EN LAN"))showLan=!showLan;
            if(showLan) {
                if(Button(86,561,222,38,"CREAR SALA"))Host(false);
                if(Button(322,561,224,38,"UNIRSE POR IP"))Join();
                Text(86,614,28,32,"IP",small);address=GUI.TextField(new Rect(116,607,430,38),address,64,inputStyle);
                Text(652,404,568,40,status,small);
            } else Text(86,575,460,62,status,small);
            Panel(652,453,568,225);
            Text(682,477,508,28,modes[(int)selectedMode],heading);
            Text(682,517,508,56,descriptions[(int)selectedMode],body);
            GUI.DrawTexture(new Rect(682,587,508,1),muted);
            Text(682,603,508,47,"Flechas en el suelo: supervelocidad durante 10 s.\nESC o F1: controles y ayuda durante la partida.",small);
        }
        void Lobby()
        {
            Panel(54,48,590,618);Text(86,76,520,40,"SALA / "+(offline?"PRÁCTICA":"LAN"),heading);
            Text(86,126,520,54,offline?"Bots locales · Hasta 8 participantes":"IP del anfitrión: "+(authority?addresses:address)+"\nPuerto TCP 7777 · Hasta 8 participantes",small);
            Text(86,197,520,30,"JUGADORES  "+view.players.Length+" / 8",subtitle);
            for(int i=0;i<view.players.Length;i++)Text(86,236+i*30,510,28,(i+1).ToString("00")+"   "+view.players[i].name+(view.players[i].bot?"  / BOT":view.players[i].id==localId?"  / TÚ":"  / LAN"),body);
            if(authority) {
                if(Button(86,505,245,43,"+ AÑADIR BOT"))AddBot();if(Button(345,505,245,43,"− QUITAR BOT"))RemoveBot();
                if(Button(86,567,504,54,"INICIAR RONDA"))StartRound();
            } else Text(86,558,510,64,"El anfitrión elegirá el modo e iniciará la ronda.",body);
            Panel(684,48,542,410);Text(715,77,470,40,"REGLAS DE LA RONDA",heading);
            for(int i=0;i<3;i++) {
                if(authority&&Button(715,137+i*62,466,48,(selectedMode==(MatchMode)i?"●  ":"")+modes[i])){selectedMode=(MatchMode)i;simulation.mode=selectedMode;}
                else if(!authority)Text(715,137+i*62,466,45,(view.mode==i?"●  ":"")+modes[i],body);
            }
            Text(715,343,466,85,descriptions[authority?(int)selectedMode:view.mode],body);
            Text(714,477,470,112,"Flechas: +50 % de velocidad durante 10 s.\nSolo los supervivientes libres pueden rescatar.\nEl infectado persigue y contagia.\nESC o F1: controles y reglas completas.",body);
            if(Button(714,602,466,48,"VOLVER AL MENÚ"))Leave();
        }
        void Hud()
        {
            var me=view.players.FirstOrDefault(x=>x.id==localId);
            string role=me==null?"ESPECTADOR":!me.alive?"ELIMINADO":me.trapped?"ATRAPADO":me.tagged?(view.mode==(int)MatchMode.Infection?"INFECTADO · ATRAPA":"TÚ LA LLEVAS"):view.mode==(int)MatchMode.Infection?"SUPERVIVIENTE · ESCAPA":"ESCAPA";
            Panel(18,18,254,48);
            GUI.DrawTexture(new Rect(18,18,3,48),me!=null&&me.tagged?coral:accent);
            Text(33,22,226,36,role,chip);
            Panel(576,18,128,58);Text(576,20,128,15,"RONDA "+view.round,centeredSmall);Text(576,36,128,34,Clock(view.timeLeft),timer);
            Panel(1000,18,262,48);
            string count=view.mode==(int)MatchMode.Relay?"BOMBA  "+Mathf.CeilToInt(view.fuse)+" s":view.mode==(int)MatchMode.Infection?view.players.Count(x=>x.alive&&!x.tagged&&!x.trapped)+" libres  ·  "+view.players.Count(x=>x.alive&&x.trapped)+" atrapados":view.players.Count(x=>x.alive&&!x.tagged&&!x.trapped)+" evasores";
            Text(1015,22,235,36,count,chip);
            if(me!=null&&me.alive&&!me.trapped) {
                Panel(18,647,203,53);
                Text(32,653,178,27,me.dashCooldown<=0?"SHIFT   Dash listo":"Dash   "+me.dashCooldown.ToString("0.0")+" s",small);
                GUI.DrawTexture(new Rect(32,687,175,3),muted);GUI.DrawTexture(new Rect(32,687,175*Mathf.Clamp01(1-me.dashCooldown/Rules.DashRecharge),3),accent);
                if(me.speedSeconds>0) {
                    Panel(232,647,216,53);Text(246,653,188,23,"SUPERVELOCIDAD",subtitle);Text(246,677,188,18,me.speedSeconds.ToString("0.0")+" s",small);
                }
                if(me.safeSeconds>0)Text(32,622,230,21,"Protección  "+me.safeSeconds.ToString("0.0")+" s",small);
            }
            bool rescuePrompt=RescueHud(me);
            if(!menu&&Time.unscaledTime<messageUntil&&!string.IsNullOrEmpty(view.message)) {
                string notification=view.message;
                // Broadcasts describe match events; only survivors receive a rescue action.
                if(me!=null&&(me.tagged||me.trapped)) {
                    int instruction=notification.IndexOf("¡Rescátalo",StringComparison.Ordinal);
                    if(instruction>=0)notification=notification.Substring(0,instruction).Trim();
                }
                float y=rescuePrompt?548:640;
                Panel(470,y,520,42);Text(486,y+9,488,29,notification,small);
            }
            Text(1042,674,220,25,"TAB clasificación  ·  ESC ayuda",small);
            if(Input.GetKey(KeyCode.Tab)) Scoreboard(402,112,477);
        }
        bool RescueHud(PlayerState me)
        {
            if(me==null||view.mode!=(int)MatchMode.Infection||!me.alive)return false;
            if(me.trapped) {
                Panel(430,608,420,78);
                Text(449,616,382,26,"ATRAPADO · INFECCIÓN EN "+Mathf.CeilToInt(me.trapSeconds)+" s",subtitle);
                Text(449,646,382,28,"Espera a que un superviviente te libere.",small);
                return true;
            }
            if(me.tagged)return false;
            var all=authority&&simulation!=null?simulation.players:replicas;
            PlayerMotor target=null;
            if(all.TryGetValue(localId,out var local)) {
                target=all.Values.Where(p=>MatchSimulation.CanRescue(local,p)).OrderBy(p=>(p.transform.position-local.transform.position).sqrMagnitude).FirstOrDefault();
                if(me.rescueTargetId>=0&&all.TryGetValue(me.rescueTargetId,out var active)&&MatchSimulation.CanRescue(local,active))target=active;
            }
            if(target!=null) {
                float progress=me.rescueTargetId==target.state.id?me.rescueProgress:0;
                Panel(430,608,420,78);
                Text(449,615,382,26,"MANTÉN E · SALVAR A "+target.state.name,subtitle);
                Text(449,644,382,22,progress.ToString("0.0")+" / "+Rules.RescueDuration.ToString("0")+" s",small);
                GUI.DrawTexture(new Rect(449,674,382,4),muted);GUI.DrawTexture(new Rect(449,674,382*Mathf.Clamp01(progress/Rules.RescueDuration),4),accent);
                return true;
            }
            return false;
        }
        void Scoreboard(float x,float y,float w)
        {
            Panel(x,y,w,100+view.players.Length*36);Text(x+22,y+17,w-44,35,view.mode==(int)MatchMode.Infection?"INFECCIÓN / ESTADO DEL EQUIPO":"CLASIFICACIÓN / TIEMPO PILLANDO",subtitle);
            var sorted=view.players.OrderBy(p=>p.taggedSeconds).ToArray();
            for(int i=0;i<sorted.Length;i++) {
                var p=sorted[i];
                string detail=view.mode==(int)MatchMode.Infection?(p.trapped?"AYUDA / "+Mathf.CeilToInt(p.trapSeconds)+" s":p.tagged?"INFECTADO":"LIBRE"):(p.taggedSeconds.ToString("0.0")+" s");
                Text(x+22,y+62+i*36,w-44,32,(i+1)+".  "+p.name+"   "+detail+(!p.alive?" / FUERA":""),row);
            }
        }
        void Results()
        {
            Panel(240,44,800,632);Text(280,76,720,30,"RONDA "+view.round+" / "+modes[view.mode],subtitle);
            Text(280,121,720,46,"FIN DE LA RONDA",heading);Text(280,175,720,68,view.message,body);
            var sorted=view.players.OrderBy(p=>p.taggedSeconds).ToArray();
            for(int i=0;i<sorted.Length;i++) {
                var p=sorted[i];
                string detail=view.mode==(int)MatchMode.Infection?(p.trapped?"ATRAPADO":p.tagged?"INFECTADO":"SUPERVIVIENTE"):(p.taggedSeconds.ToString("0.0")+" s pillando");
                Text(280,264+i*32,720,30,(i+1).ToString("00")+"   "+p.name+"    "+detail+(!p.alive?" / ELIMINADO":""),body);
            }
            if(authority) {
                if(Button(280,561,345,48,"REVANCHA"))StartRound();
                if(Button(644,561,350,48,"CAMBIAR MODO / SALA")){simulation.phase=MatchPhase.Lobby;view=simulation.Snapshot();}
            }else Text(280,562,714,40,"Esperando la siguiente ronda del anfitrión…",body);
            if(Button(280,621,714,34,"SALIR AL MENÚ"))Leave();
        }
        void PauseMenu()
        {
            Panel(320,116,640,493);
            Text(350,139,580,37,offline?"PAUSA":"AYUDA / LA RONDA CONTINÚA",heading);
            Text(350,190,580,30,"WASD / flechas: moverte    ESPACIO: saltar    SHIFT: dash",body);
            Text(350,225,580,30,"Flechas en el suelo: +50 % velocidad durante 10 segundos.",small);
            if(view.mode==(int)MatchMode.Infection) {
                var me=view.players.FirstOrDefault(p=>p.id==localId);
                bool infected=me!=null&&me.tagged;
                Text(350,275,580,27,infected?"ERES INFECTADO":"EQUIPO SUPERVIVIENTE",subtitle);
                Text(350,309,580,70,infected?"Persigue y atrapa a los supervivientes. Solo los jugadores libres pueden salvar a sus compañeros. Ganas cuando no queda ninguno libre.":"Escapa hasta que termine el tiempo. Si un compañero queda atrapado, acércate y mantén E durante "+Rules.RescueDuration.ToString("0")+" s. Solo un superviviente libre puede rescatar.",body);
                Text(350,388,580,42,"Cada captura: 10 s para recibir ayuda. Puedes ser rescatado varias veces; solo te infectas si nadie te salva a tiempo.",small);
            } else Text(350,285,580,115,descriptions[view.mode],body);
            if(Button(350,452,580,44,"CONTINUAR")){menu=false;paused=false;}
            if(Button(350,508,282,40,mute?"ACTIVAR SONIDO":"SILENCIAR SONIDO"))mute=!mute;
            if(Button(648,508,282,40,"SALIR AL MENÚ"))Leave();
            Text(350,566,580,24,"TAB clasificación    M sonido    ESC / F1 cerrar ayuda",small);
        }
        static string Clock(float time){int t=Mathf.CeilToInt(time);return (t/60).ToString("00")+":"+(t%60).ToString("00");}
    }
}

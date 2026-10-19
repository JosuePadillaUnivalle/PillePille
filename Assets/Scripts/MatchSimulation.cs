using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    public sealed class MatchSimulation
    {
        public readonly Dictionary<int,PlayerMotor> players=new Dictionary<int,PlayerMotor>();
        public MatchMode mode;
        public MatchPhase phase=MatchPhase.Lobby;
        public float timeLeft=Rules.RoundSeconds, fuse=Rules.FuseSeconds;
        public int round;
        public string message="Reúne a los jugadores y comienza la ronda.";
        public int TagCount {get;private set;}
        public readonly PadState[] pads=SpeedPad.Positions.Select((p,i)=>new PadState{id=i}).ToArray();
        readonly HashSet<long> padOccupants=new HashSet<long>();
        readonly HashSet<long> nextPadOccupants=new HashSet<long>();
        readonly BotNavigator bots=new BotNavigator();

        public PlayerMotor Add(int id,string name,bool bot)
        {
            var state=new PlayerState {id=id,name=name,bot=bot,alive=true,position=ArenaBuilder.Spawns[players.Count%8]};
            var player=PlayerMotor.Create(state,true);players.Add(id,player);return player;
        }
        public void Remove(int id)
        {
            if(!players.TryGetValue(id,out var p))return;
            bool wasTagged=p.state.tagged;players.Remove(id);bots.Remove(id);Object.Destroy(p.gameObject);
            foreach(var other in players.Values)if(other.state.rescueTargetId==id)ResetRescue(other.state);
            if(phase!=MatchPhase.Playing)return;
            if(players.Values.Count(x=>x.state.alive)<2) {Finish("Ronda terminada: quedan menos de dos jugadores.");return;}
            if(wasTagged&&!players.Values.Any(x=>x.state.alive&&x.state.tagged)) {
                var next=players.Values.Where(x=>x.state.alive).OrderBy(x=>x.state.trapped).ThenBy(x=>x.state.id).First();
                Infect(next.state);
                message=next.state.name+" recibe la mancha tras una desconexión.";
            }
            if(mode==MatchMode.Infection&&!players.Values.Any(x=>IsFree(x.state)))Finish("¡No quedan supervivientes libres! Gana la infección.");
        }
        public void StartRound(MatchMode nextMode,int preferredChaserId=-1)
        {
            if(players.Count<2) {message="Se necesitan al menos dos jugadores. Añade un bot o conecta otro equipo.";return;}
            mode=nextMode;phase=MatchPhase.Playing;round++;timeLeft=Rules.RoundSeconds;fuse=Rules.FuseSeconds;TagCount=0;
            foreach(var pad in pads)pad.cooldown=0;
            padOccupants.Clear();nextPadOccupants.Clear();bots.Reset();
            int i=0;
            foreach(var p in players.Values.OrderBy(x=>x.state.id)) {
                p.state.tagged=false;p.state.alive=true;p.state.taggedSeconds=0;p.state.safeSeconds=0;
                p.state.trapped=false;p.state.trapSeconds=0;p.state.rescuesUsed=0;p.state.speedSeconds=0;ResetRescue(p.state);
                p.ResetAt(ArenaBuilder.Spawns[i++%8]);
            }
            var ordered=players.Values.OrderBy(x=>x.state.id).ToArray();
            var chaser=ordered.FirstOrDefault(p=>p.state.id==preferredChaserId)??ordered[(round-1)%ordered.Length];chaser.state.tagged=true;chaser.state.safeSeconds=Rules.Immunity;
            message=chaser.state.name+" empieza pillando. ¡Corre!";
        }
        public void Tick(float dt)
        {
            if(phase!=MatchPhase.Playing)return;
            foreach(var p in players.Values) {
                if(p.state.bot)p.input=BotInput(p,dt);
                else if(Time.realtimeSinceStartup-p.lastInput>.5f)p.input=new InputFrame();
                p.Tick(dt);
                if(p.state.alive&&p.state.tagged)p.state.taggedSeconds+=dt;
            }
            TickPads(dt);
            // Capsule contact, including vertical separation; walls block tagging.
            var active=players.Values.Where(x=>x.state.alive).ToArray();
            foreach(var chaser in active) {
                if(!chaser.state.tagged||chaser.state.trapped||chaser.state.safeSeconds>0)continue;
                foreach(var runner in active) {
                    if(runner==chaser||runner.state.tagged||runner.state.trapped||runner.state.safeSeconds>0)continue;
                    var delta=runner.transform.position-chaser.transform.position;
                    var horizontal=new Vector2(delta.x,delta.z).magnitude;
                    float verticalGap=Mathf.Max(0,Mathf.Abs(delta.y)-1f);
                    if(horizontal*horizontal+verticalGap*verticalGap>1.12f*1.12f)continue;
                    // Character controllers themselves are ignored in this visibility check.
                    if(HasLineOfSight(chaser,runner) && TryTag(chaser.state.id,runner.state.id))break;
                }
            }
            if(mode==MatchMode.Infection)TickRescues(dt);
            timeLeft=Mathf.Max(0,timeLeft-dt);
            if(mode==MatchMode.Infection && !active.Any(x=>IsFree(x.state))) {Finish("¡No quedan supervivientes libres! Gana la infección.");return;}
            if(mode==MatchMode.Relay) {
                fuse-=dt;
                if(fuse<=0) {
                    var carrier=active.FirstOrDefault(x=>x.state.tagged);
                    if(carrier!=null) {carrier.state.alive=false;carrier.state.tagged=false;message=carrier.state.name+" quedó eliminado por la explosión.";}
                    var survivors=active.Where(x=>x.state.alive).ToArray();
                    if(survivors.Length<=1) {Finish(survivors.Length==1?"¡"+survivors[0].state.name+" gana el relevo!":"Fin del relevo.");return;}
                    var next=survivors[(round+TagCount)%survivors.Length];next.state.tagged=true;next.state.safeSeconds=Rules.Immunity;fuse=Rules.FuseSeconds;
                }
            }
            if(timeLeft<=0) {
                if(mode==MatchMode.Classic) {
                    float best=active.Min(x=>x.state.taggedSeconds);
                    var winners=active.Where(x=>Mathf.Abs(x.state.taggedSeconds-best)<.021f).Select(x=>x.state.name);
                    Finish("Menor tiempo pillando: "+string.Join(" + ",winners.ToArray())+".");
                } else if(mode==MatchMode.Infection) Finish("¡Sobreviven "+string.Join(", ",active.Where(x=>IsFree(x.state)).Select(x=>x.state.name).ToArray())+"!");
                else Finish("Tiempo agotado. Sobreviven: "+string.Join(", ",active.Where(x=>x.state.alive).Select(x=>x.state.name).ToArray())+".");
            }
        }
        public bool TryTag(int chaserId,int runnerId)
        {
            if(phase!=MatchPhase.Playing||!players.ContainsKey(chaserId)||!players.ContainsKey(runnerId))return false;
            var a=players[chaserId].state;var b=players[runnerId].state;
            if(chaserId==runnerId||!a.alive||!b.alive||a.trapped||b.trapped||!a.tagged||b.tagged||a.safeSeconds>0||b.safeSeconds>0)return false;
            if(mode!=MatchMode.Infection)a.tagged=false;
            // Both participants receive a dispersion window: no immediate pass-back or chain.
            a.safeSeconds=Rules.Immunity;b.safeSeconds=Rules.Immunity;ResetRescue(b);TagCount++;
            if(mode==MatchMode.Infection) {
                b.trapped=true;b.trapSeconds=Rules.TrapDuration;b.tagged=false;
                message=b.name+" está atrapado. ¡Rescátalo con E antes de 10 s!";
            } else {
                b.tagged=true;
                message=a.name+" pilló a "+b.name+".";
            }
            return true;
        }

        static bool IsFree(PlayerState state) {return state.alive&&!state.tagged&&!state.trapped;}
        static void ResetRescue(PlayerState state) {state.rescueTargetId=-1;state.rescueProgress=0;}
        static void Infect(PlayerState state)
        {
            state.trapped=false;state.trapSeconds=0;state.tagged=true;state.safeSeconds=Rules.Immunity;ResetRescue(state);
        }
        static bool HasLineOfSight(PlayerMotor from,PlayerMotor to)
        {
            var delta=to.transform.position-from.transform.position;
            return delta.sqrMagnitude<.0001f||!Physics.RaycastAll(from.transform.position,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Any(hit=>hit.collider.GetComponentInParent<PlayerMotor>()==null);
        }
        public static bool CanRescue(PlayerMotor rescuer,PlayerMotor target)
        {
            return rescuer!=null&&target!=null&&rescuer!=target&&IsFree(rescuer.state)&&target.state.alive&&target.state.trapped&&!target.state.tagged
                &&target.state.trapSeconds>0
                &&(rescuer.transform.position-target.transform.position).sqrMagnitude<=Rules.RescueRange*Rules.RescueRange&&HasLineOfSight(rescuer,target);
        }
        void TickPads(float dt)
        {
            nextPadOccupants.Clear();
            foreach(var pad in pads) {
                pad.cooldown=Mathf.Max(0,pad.cooldown-dt);
                var pos=SpeedPad.Positions[pad.id];
                foreach(var player in players.Values.OrderBy(x=>x.state.id)) {
                    if(!player.state.alive||player.state.trapped)continue;
                    var delta=player.transform.position-pos;
                    if(Mathf.Abs(delta.x)>1.5f||Mathf.Abs(delta.z)>1.5f||Mathf.Abs(delta.y-1f)>.55f)continue;
                    long key=((long)pad.id<<32)|(uint)player.state.id;nextPadOccupants.Add(key);
                    if(pad.cooldown>0||padOccupants.Contains(key))continue;
                    player.state.speedSeconds=Rules.SpeedDuration;pad.cooldown=Rules.PadRecharge;
                    message=player.state.name+" activa supervelocidad: +50% durante 10 s.";
                }
            }
            padOccupants.Clear();padOccupants.UnionWith(nextPadOccupants);
        }
        void TickRescues(float dt)
        {
            // Countdown wins at its deadline. Contact is resolved before rescue, so catching a rescuer cancels it.
            foreach(var target in players.Values)if(target.state.trapped) {
                target.state.trapSeconds=Mathf.Max(0,target.state.trapSeconds-dt);
                // Use the same tolerance as rescue completion for simultaneous deadlines.
                if(target.state.trapSeconds<=.0001f)target.state.trapSeconds=0;
            }
            var claimed=new HashSet<int>();
            // A newcomer must not steal a target from an uninterrupted rescue already in progress.
            foreach(var rescuer in players.Values.OrderByDescending(x=>x.input.interact&&x.state.rescueProgress>0
                &&players.TryGetValue(x.state.rescueTargetId,out var current)&&CanRescue(x,current)).ThenBy(x=>x.state.id)) {
                var state=rescuer.state;
                if(!IsFree(state)||!rescuer.input.interact) {ResetRescue(state);continue;}
                PlayerMotor target=null;
                if(players.TryGetValue(state.rescueTargetId,out var previous)&&!claimed.Contains(previous.state.id)&&CanRescue(rescuer,previous))target=previous;
                if(target==null)target=players.Values.Where(x=>!claimed.Contains(x.state.id)&&CanRescue(rescuer,x))
                    .OrderBy(x=>(x.transform.position-rescuer.transform.position).sqrMagnitude).ThenBy(x=>x.state.id).FirstOrDefault();
                if(target==null) {ResetRescue(state);continue;}
                if(state.rescueTargetId!=target.state.id) {ResetRescue(state);state.rescueTargetId=target.state.id;}
                claimed.Add(target.state.id);state.rescueProgress+=dt;
                if(state.rescueProgress+.0001f<Rules.RescueDuration)continue;
                var saved=target.state;saved.trapped=false;saved.trapSeconds=0;saved.rescuesUsed++;saved.safeSeconds=Rules.Immunity;
                target.input=new InputFrame();state.safeSeconds=Rules.Immunity;ResetRescue(state);
                message=state.name+" rescató a "+saved.name+". ¡3 s de inmunidad para ambos!";
            }
            foreach(var target in players.Values)if(target.state.trapped&&target.state.trapSeconds<=0) {
                Infect(target.state);target.input=new InputFrame();message=target.state.name+" se convirtió en infectado.";
            }
        }
        void Finish(string result) {phase=MatchPhase.Results;message=result;}
        InputFrame BotInput(PlayerMotor p,float dt)
        {
            return bots.Input(p,players,mode,pads,dt);
        }
        public Packet Snapshot()
        {
            return new Packet{type="state",version=Rules.ProtocolVersion,mode=(int)mode,phase=(int)phase,round=round,timeLeft=timeLeft,fuse=fuse,message=message,players=players.Values.Select(x=>x.state).OrderBy(x=>x.id).ToArray(),pads=pads};
        }
        public void Clear() {foreach(var p in players.Values)if(p!=null)Object.Destroy(p.gameObject);players.Clear();bots.Reset();}
    }
}

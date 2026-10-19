// Rule-only harness. Compiles the REAL Protocol.cs + MatchSimulation.cs.
// These adapters replace native Unity movement, rendering, physics and arena setup.
// A passing result does not validate CharacterController collisions, visuals or LAN.
using System;
using System.Linq;
using PillePille;
using UnityEngine;

namespace UnityEngine {
    public class Object { public static void Destroy(object x) {} }
    public class GameObject {}
    public class Transform { public Vector3 position; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float magnitude {get{return (float)Math.Sqrt(x*x+y*y);}} }
    public struct Vector3 {
        public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero {get{return new Vector3();}} public static Vector3 up {get{return new Vector3(0,1,0);}}
        public float sqrMagnitude {get{return x*x+y*y+z*z;}} public float magnitude {get{return (float)Math.Sqrt(sqrMagnitude);}}
        public Vector3 normalized {get{return magnitude>.000001f?this/magnitude:zero;}} public void Normalize(){this=normalized;}
        public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator -(Vector3 a){return new Vector3(-a.x,-a.y,-a.z);}
        public static Vector3 operator *(Vector3 a,float f){return new Vector3(a.x*f,a.y*f,a.z*f);}
        public static Vector3 operator /(Vector3 a,float f){return new Vector3(a.x/f,a.y/f,a.z/f);}
        public static bool operator ==(Vector3 a,Vector3 b){return (a-b).sqrMagnitude<.000001f;}
        public static bool operator !=(Vector3 a,Vector3 b){return !(a==b);}
        public override bool Equals(object o){return o is Vector3 && this==(Vector3)o;} public override int GetHashCode(){return x.GetHashCode()^y.GetHashCode()^z.GetHashCode();}
    }
    public struct Quaternion { public static Quaternion Euler(float x,float y,float z){return new Quaternion();} public static Vector3 operator *(Quaternion q,Vector3 v){return v;} }
    public static class Mathf {
        public static float Max(float a,float b){return Math.Max(a,b);} public static float Abs(float a){return Math.Abs(a);}
        public static float Sin(float a){return (float)Math.Sin(a);} public static float Cos(float a){return (float)Math.Cos(a);} public static float Sign(float a){return a>=0?1:-1;}
    }
    public static class Time { public static float realtimeSinceStartup; }
    public enum QueryTriggerInteraction { Ignore }
    public class Collider {public object owner; public T GetComponent<T>() where T:class{return owner as T;} public T GetComponentInParent<T>() where T:class{return owner as T;} }
    public struct RaycastHit { public Collider collider; }
    public static class Physics {
        public const int DefaultRaycastLayers=-5; public static bool Blocked;
        public static RaycastHit[] RaycastAll(Vector3 a,Vector3 b,float d,int layer,QueryTriggerInteraction t){return Blocked?new[]{new RaycastHit{collider=new Collider()}}:new RaycastHit[0];}
        public static bool SphereCast(Vector3 a,float r,Vector3 b,out RaycastHit hit,float d){hit=new RaycastHit();return false;}
    }
}
namespace PillePille {
    // These rule fixtures contain no bots. Native AI is verified separately by BotVerification.
    public sealed class BotNavigator {
        public void Reset() {} public void Remove(int id) {}
        public InputFrame Input(PlayerMotor player,System.Collections.Generic.IDictionary<int,PlayerMotor> players,MatchMode mode,PadState[] pads,float dt){return new InputFrame();}
    }
    public sealed class PlayerMotor {
        public PlayerState state; public InputFrame input=new InputFrame(); public float lastInput;
        public Transform transform=new Transform(); public GameObject gameObject=new GameObject();
        public static PlayerMotor Create(PlayerState s,bool authority){var p=new PlayerMotor{state=s};p.transform.position=s.position;return p;}
        public void ResetAt(Vector3 p){transform.position=p;state.position=p;state.dashCooldown=0;input=new InputFrame();}
        public void Tick(float dt){if(!state.alive)return;state.safeSeconds=Math.Max(0,state.safeSeconds-dt);state.speedSeconds=Math.Max(0,state.speedSeconds-dt);state.position=transform.position;}
    }
    public static class ArenaBuilder {public static readonly Vector3[] Spawns=Enumerable.Range(0,8).Select(i=>new Vector3(-30+i*6,1,25)).ToArray();}
    // Fixture positions mirror SpeedPad.cs; no pad presentation is evaluated.
    public static class SpeedPad {public static readonly Vector3[] Positions={new Vector3(-8,.025f,-13),new Vector3(8,.025f,12),new Vector3(12,.025f,-5)};}
}
public static class RulesHarness {
    static int passed,failed;
    static void Check(bool condition,string explanation){if(!condition)throw new Exception(explanation);}
    static void Near(float actual,float expected,string label){Check(Math.Abs(actual-expected)<.002f,label+": expected "+expected+", got "+actual);}
    static void Test(string name,Action action){try{Time.realtimeSinceStartup=0;Physics.Blocked=false;action();Console.WriteLine("PASS | "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL | "+name+" | "+e.Message);failed++;}}
    static MatchSimulation New(int n=4,MatchMode mode=MatchMode.Infection){var s=new MatchSimulation();for(int i=0;i<n;i++)s.Add(i,"Player"+i,false);s.StartRound(mode);foreach(var p in s.players.Values){p.state.safeSeconds=0;p.lastInput=0;}return s;}
    static void At(MatchSimulation s,int id,float x,float z,float y=1){s.players[id].transform.position=new Vector3(x,y,z);s.players[id].state.position=s.players[id].transform.position;}
    static void SetupRescue(MatchSimulation s){Check(s.TryTag(0,1),"initial tag");At(s,0,-30,-30);At(s,1,0,0);At(s,2,1.8f,0);if(s.players.ContainsKey(3))At(s,3,10,10);}
    static void Hold(MatchSimulation s,int id,bool held=true){s.players[id].input.interact=held;s.players[id].lastInput=Time.realtimeSinceStartup;}
    static void Step(MatchSimulation s,float duration){for(float t=0;t<duration-.0001f;t+=.1f)s.Tick(Math.Min(.1f,duration-t));}
    static void OnPad(MatchSimulation s,int id,int pad){var p=SpeedPad.Positions[pad];At(s,id,p.x,p.z,p.y+1);}
    public static int Main(){
        Console.WriteLine("PILLE PILLE - RULE LOGIC VERIFICATION");
        Console.WriteLine("Real project sources: Protocol.cs and MatchSimulation.cs. Minimal fake UnityEngine/PlayerMotor adapters.");
        Console.WriteLine("Not evaluated: native physics, CharacterController movement, rendering, Unity scene import, executable build or LAN transport.");
        Test("First capture enters trapped state for 10 seconds",()=>{var s=New();Check(s.TryTag(0,1),"tag accepted");var p=s.players[1].state;Check(p.trapped&&!p.tagged,"trapped runner must not yet be infected");Near(p.trapSeconds,10,"trap duration");Check(!s.TryTag(0,1),"cannot re-tag trapped player");});
        Test("Trap deadline converts player and resets rescue state",()=>{var s=New();SetupRescue(s);Step(s,10);var p=s.players[1].state;Check(p.tagged&&!p.trapped,"conversion");Near(p.trapSeconds,0,"timer");Check(p.rescueTargetId==-1,"rescue state cleared");});
        Test("Holding E for exactly 1 second frees teammate with unchanged 3-second immunity",()=>{var s=New();SetupRescue(s);Near(Rules.RescueDuration,1,"rescue hold duration");Near(Rules.Immunity,3,"protection duration");Hold(s,2);Step(s,Rules.RescueDuration-.1f);Check(s.players[1].state.trapped&&s.players[1].state.rescuesUsed==0,"must not rescue before one second");s.Tick(.1f);var p=s.players[1].state;Check(!p.trapped&&!p.tagged,"rescued role");Check(p.rescuesUsed==1,"rescue count");Near(p.safeSeconds,Rules.Immunity,"target protection");Near(s.players[2].state.safeSeconds,Rules.Immunity,"rescuer protection");Check(s.players[2].state.rescueTargetId==-1,"progress reset");});
        Test("Releasing E cancels accumulated rescue progress",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.3f);Near(s.players[2].state.rescueProgress,Rules.RescueDuration*.3f,"partial progress");Hold(s,2,false);s.Tick(.1f);Near(s.players[2].state.rescueProgress,0,"cancel");Hold(s,2);Step(s,Rules.RescueDuration*.8f);Check(s.players[1].state.trapped,"must hold a fresh full second");Near(s.players[2].state.rescueProgress,Rules.RescueDuration*.8f,"fresh progress");});
        Test("Leaving range cancels rescue before completion",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.3f);At(s,2,6,0);s.Tick(.1f);Near(s.players[2].state.rescueProgress,0,"range cancellation");Check(s.players[1].state.trapped,"target remains trapped");});
        Test("Blocked line of sight prevents and cancels rescue",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.3f);Physics.Blocked=true;Check(!MatchSimulation.CanRescue(s.players[2],s.players[1]),"blocker");s.Tick(.1f);Near(s.players[2].state.rescueProgress,0,"LOS cancellation");});
        Test("Stale held input cancels an incomplete rescue",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.3f);Near(s.players[2].state.rescueProgress,Rules.RescueDuration*.3f,"partial progress before timeout");Time.realtimeSinceStartup+=.6f;s.Tick(.1f);Check(!s.players[2].input.interact,"stale E released");Near(s.players[2].state.rescueProgress,0,"stale input cancellation");Check(s.players[2].state.rescueTargetId==-1&&s.players[1].state.trapped,"target remains trapped");});
        Test("Infected or trapped player cannot rescue; initial infected cannot be cured",()=>{var s=New();SetupRescue(s);At(s,0,1.8f,0);Check(!MatchSimulation.CanRescue(s.players[0],s.players[1]),"infected cannot rescue");Check(!MatchSimulation.CanRescue(s.players[2],s.players[0]),"infected is not valid rescue target");Check(!MatchSimulation.CanRescue(s.players[1],s.players[1]),"self rescue rejected");Hold(s,2);Step(s,3);Check(s.players[0].state.tagged,"initial infected remains infected");});
        Test("Capture of rescuer interrupts progress before rescue resolves",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.8f);At(s,0,2.7f,0);s.players[0].state.safeSeconds=0;s.Tick(Rules.RescueDuration*.2f);Check(s.players[2].state.trapped,"rescuer caught");Check(s.players[1].state.trapped,"original target not freed");Near(s.players[2].state.rescueProgress,0,"interrupted");});
        Test("Trap expiration wins a simultaneous rescue deadline",()=>{var s=New();SetupRescue(s);s.players[1].state.trapSeconds=Rules.RescueDuration;Hold(s,2);s.Tick(Rules.RescueDuration);Check(s.players[1].state.tagged&&!s.players[1].state.trapped,"deadline conversion takes priority");Check(s.players[1].state.rescuesUsed==0,"no completed rescue");});
        Test("Fixed 50-Hz ticks preserve deadline priority and exact hold duration",()=>{var s=New();SetupRescue(s);s.players[1].state.trapSeconds=Rules.RescueDuration;Hold(s,2);int frames=(int)Math.Round(Rules.RescueDuration/.02f);for(int i=0;i<frames-1;i++)s.Tick(.02f);Check(s.players[1].state.trapped&&!s.players[1].state.tagged,"still trapped before final tick");s.Tick(.02f);Check(s.players[1].state.tagged&&!s.players[1].state.trapped&&s.players[1].state.rescuesUsed==0,"conversion wins on final tick");});
        Test("Repeated captures always trap and allow multiple one-second rescues",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration);for(int count=2;count<=5;count++){s.players[0].state.safeSeconds=0;s.players[1].state.safeSeconds=0;s.players[2].state.safeSeconds=0;Check(s.TryTag(0,1),"capture after rescue");Check(s.players[1].state.trapped&&!s.players[1].state.tagged,"captured again, never instantly infected");Near(s.players[1].state.trapSeconds,Rules.TrapDuration,"fresh full rescue window");Check(MatchSimulation.CanRescue(s.players[2],s.players[1]),"previous rescue count must not prevent help");Hold(s,2);Step(s,Rules.RescueDuration);Check(!s.players[1].state.trapped&&!s.players[1].state.tagged&&s.players[1].state.rescuesUsed==count,"repeated rescue completes and count remains statistics only");Near(s.players[1].state.safeSeconds,Rules.Immunity,"repeated target protection");Near(s.players[2].state.safeSeconds,Rules.Immunity,"repeated rescuer protection");}});
        Test("Previously rescued player converts only when full capture countdown expires",()=>{var s=New();s.players[1].state.rescuesUsed=5;SetupRescue(s);Step(s,Rules.TrapDuration-.1f);Check(s.players[1].state.trapped&&!s.players[1].state.tagged,"still trapped before full deadline regardless of history");s.Tick(.1f);Check(s.players[1].state.tagged&&!s.players[1].state.trapped&&s.players[1].state.rescuesUsed==5,"infection comes only from expired rescue timer");});
        Test("No free survivors ends infection even with trapped players",()=>{var s=New(2);Check(s.TryTag(0,1),"capture");s.Tick(.1f);Check(s.phase==MatchPhase.Results,"round ended");Check(s.players[1].state.trapped,"need not wait for conversion");});
        Test("Only free survivors count at round timeout",()=>{var s=New();SetupRescue(s);s.timeLeft=.05f;s.Tick(.1f);Check(s.phase==MatchPhase.Results,"timeout");Check(s.message.Contains("Player2")&&!s.message.Contains("Player1"),"trapped player excluded from winners");});
        Test("Disconnect removes stale rescue reference",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,.5f);s.Remove(1);Check(s.players[2].state.rescueTargetId==-1,"removed target reference");Near(s.players[2].state.rescueProgress,0,"removed target progress");});
        Test("Disconnect of last infected assigns replacement and keeps valid roles",()=>{var s=New();SetupRescue(s);s.Remove(0);Check(s.players[2].state.tagged,"lowest-id free survivor selected before trapped");Check(s.players[1].state.trapped&&!s.players[1].state.tagged,"trapped role preserved");});
        Test("Disconnect with fewer than two players ends round",()=>{var s=New(2);s.Remove(1);Check(s.phase==MatchPhase.Results,"round closes");});
        Test("Round restart clears trap, rescue, boost and pad cooldown",()=>{var s=New();SetupRescue(s);Hold(s,2);s.Tick(.5f);s.players[3].state.speedSeconds=5;s.players[1].state.rescuesUsed=1;s.pads[0].cooldown=18;s.StartRound(MatchMode.Infection);foreach(var p in s.players.Values){Check(!p.state.trapped&&p.state.trapSeconds==0&&p.state.rescuesUsed==0&&p.state.rescueProgress==0&&p.state.rescueTargetId==-1&&p.state.speedSeconds==0,"round state reset");}Check(s.pads.All(p=>p.cooldown==0),"pad reset");Check(s.players.Values.Count(p=>p.state.tagged)==1,"exactly one infected");});
        Test("Pad entry grants exactly 10 seconds and global 20-second cooldown",()=>{var s=New(4,MatchMode.Classic);OnPad(s,1,0);s.Tick(.1f);Near(s.players[1].state.speedSeconds,10,"boost");Near(s.pads[0].cooldown,20,"cooldown");OnPad(s,2,0);s.Tick(.1f);Near(s.players[2].state.speedSeconds,0,"shared pad unavailable");});
        Test("Camping does not reactivate a pad when cooldown expires",()=>{var s=New(4,MatchMode.Classic);OnPad(s,1,0);s.Tick(.1f);Step(s,20.1f);Near(s.pads[0].cooldown,0,"pad ready");Near(s.players[1].state.speedSeconds,0,"camping does not trigger");At(s,1,0,0);s.Tick(.1f);OnPad(s,1,0);s.Tick(.1f);Near(s.players[1].state.speedSeconds,10,"re-entry triggers");});
        Test("A different ready pad refreshes boost without stacking duration",()=>{var s=New(4,MatchMode.Classic);OnPad(s,1,0);s.Tick(.1f);Step(s,2);OnPad(s,1,1);s.Tick(.1f);Near(s.players[1].state.speedSeconds,10,"refresh exactly 10, not 18");});
        Test("Boost pickups work for both pursuer and runner",()=>{var s=New(4,MatchMode.Classic);OnPad(s,0,0);OnPad(s,1,1);s.Tick(.1f);Near(s.players[0].state.speedSeconds,10,"pursuer boost");Near(s.players[1].state.speedSeconds,10,"runner boost");});
        Test("Trapped or airborne players cannot activate ground pads",()=>{var s=New();SetupRescue(s);OnPad(s,1,0);OnPad(s,2,1);s.players[2].transform.position+=Vector3.up*3;s.Tick(.1f);Near(s.pads[0].cooldown,0,"trapped ignored");Near(s.pads[1].cooldown,0,"airborne ignored");});
        Test("Lower-ID newcomer does not steal an uninterrupted rescue",()=>{var s=New(5);SetupRescue(s);At(s,3,1.8f,0);At(s,2,10,10);Hold(s,3);Step(s,Rules.RescueDuration*.4f);Near(s.players[3].state.rescueProgress,Rules.RescueDuration*.4f,"existing rescue");At(s,2,-1.8f,0);Hold(s,2);Step(s,Rules.RescueDuration*.1f);Check(s.players[1].state.trapped,"competition begins before rescue completes");Near(s.players[3].state.rescueProgress,Rules.RescueDuration*.5f,"existing rescuer retains progress");Near(s.players[2].state.rescueProgress,0,"newcomer cannot steal progress");Step(s,Rules.RescueDuration*.5f);Check(!s.players[1].state.trapped,"existing rescue finished on time");Check(s.players[1].state.rescuesUsed==1,"exactly one rescue");Near(s.players[2].state.rescueProgress,0,"newcomer does not accumulate conflicting rescue");});
        Test("Classic and Relay preserve direct tag transfer",()=>{foreach(var mode in new[]{MatchMode.Classic,MatchMode.Relay}){var s=New(4,mode);Check(s.TryTag(0,1),"tag accepted");Check(!s.players[0].state.tagged&&s.players[1].state.tagged&&!s.players[1].state.trapped,"original behavior "+mode);}});
        Test("Snapshot includes protocol version and synchronized half-rescue progress",()=>{var s=New();SetupRescue(s);Hold(s,2);Step(s,Rules.RescueDuration*.5f);s.players[2].state.speedSeconds=7;s.pads[0].cooldown=12;var p=s.Snapshot();Check(p.version==Rules.ProtocolVersion&&p.version==3,"protocol version");Check(p.players.Single(x=>x.id==1).trapped,"trap snapshot");Near(p.players.Single(x=>x.id==2).rescueProgress,Rules.RescueDuration*.5f,"half-complete rescue snapshot");Check(p.players.Single(x=>x.id==2).rescueTargetId==1,"target snapshot");Near(p.players.Single(x=>x.id==2).speedSeconds,7,"boost snapshot");Near(p.pads[0].cooldown,12,"pad snapshot");});
        Console.WriteLine("TOTAL: "+passed+" passed, "+failed+" failed.");
        return failed==0?0:1;
    }
}

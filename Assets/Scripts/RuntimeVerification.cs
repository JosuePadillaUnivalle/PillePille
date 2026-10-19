using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    // Integration tests run inside the real Windows player with -pilleTest.
    public sealed class RuntimeVerification : MonoBehaviour
    {
        readonly List<string> results = new List<string>();
        const float Dt = .02f;
        MatchSimulation sim;
        PlayerMotor chaser, victim, helper;
        int failed;
        void Check(string name, bool condition, string detail = "")
        {
            string line = (condition ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " | " + detail : "");
            results.Add(line); Debug.Log(line); if (!condition) failed++;
        }
        void Run(string name, Action test)
        {
            try { test(); }
            catch (Exception ex) { Check(name + " sin excepciones", false, ex.ToString()); }
        }
        // Unlike ResetAt, fixture teleports preserve the current dash and held input.
        void Warp(PlayerMotor p, Vector3 position)
        {
            bool enabled = p.controller.enabled; p.controller.enabled = false;
            p.transform.position = position; p.state.position = position;
            p.controller.enabled = enabled; Physics.SyncTransforms();
        }
        void Step(int count = 1)
        {
            for (int i = 0; i < count; i++) {
                // A fresh held input on every simulated frame avoids the LAN idle timeout.
                foreach (var p in sim.players.Values) p.lastInput = Time.realtimeSinceStartup;
                Physics.SyncTransforms(); sim.Tick(Dt);
            }
        }
        int RescueFrames(float fraction = 1f)
        {
            return Mathf.RoundToInt(Rules.RescueDuration * fraction / Dt);
        }
        void NewRound(MatchMode mode)
        {
            sim.StartRound(mode);
            chaser = sim.players.Values.Single(p => p.state.tagged);
            var runners = sim.players.Values.Where(p => !p.state.tagged).OrderBy(p => p.state.id).ToArray();
            victim = runners[0]; helper = runners[1];
            foreach (var p in sim.players.Values) { p.input = new InputFrame(); p.state.safeSeconds = 0; }
            // Clear lanes, away from ramps and speed pads.
            Warp(chaser, new Vector3(14, 1.05f, -13));
            Warp(victim, new Vector3(-12, 1.05f, -13));
            Warp(helper, new Vector3(-12, 1.05f, 13)); Step(3);
        }
        void BeginRescue()
        {
            NewRound(MatchMode.Infection);
            Check("Primer contacto permite rescate", sim.TryTag(chaser.state.id, victim.state.id));
            Warp(helper, victim.transform.position + Vector3.right * 1.8f);
            helper.input = new InputFrame { interact = true };
        }
        void EnterPad(PlayerMotor p, int index)
        {
            var center = SpeedPad.Positions[index];
            Warp(p, new Vector3(center.x - 2.5f, 1.05f, center.z)); Step();
            Warp(p, new Vector3(center.x, 1.05f, center.z)); Step();
        }
        float Distance(PlayerMotor p, bool tagged, bool boosted, bool dash, int frames)
        {
            p.ResetAt(new Vector3(-14, 1.1f, -13)); p.state.tagged = tagged; p.state.trapped = false;
            for (int i = 0; i < 15; i++) p.Tick(Dt);
            p.state.speedSeconds = boosted ? Rules.SpeedDuration : 0;
            var start = p.transform.position;
            for (int i = 0; i < frames; i++) { p.input = new InputFrame { x = 1, dash = dash && i == 0 }; p.Tick(Dt); }
            return p.transform.position.x - start.x;
        }
        void VerifyMotor()
        {
            var p = sim.players[0];
            float d = Distance(p, false, false, false, 50);
            Check("Evasor 6 m/s", Mathf.Abs(d - Rules.RunnerSpeed) < .1f, d.ToString("F3"));
            d = Distance(p, true, false, false, 50);
            Check("Perseguidor 6.9 m/s", Mathf.Abs(d - Rules.ChaserSpeed) < .1f, d.ToString("F3"));
            d = Distance(p, false, true, false, 50);
            Check("Supervelocidad evasor 9 m/s", Mathf.Abs(d - Rules.RunnerSpeed * Rules.SpeedMultiplier) < .1f, d.ToString("F3"));
            d = Distance(p, true, true, false, 50);
            Check("Supervelocidad perseguidor 10.35 m/s", Mathf.Abs(d - Rules.ChaserSpeed * Rules.SpeedMultiplier) < .1f, d.ToString("F3"));
            float normalDash = Distance(p, false, false, true, 8), boostedDash = Distance(p, false, true, true, 8);
            Check("Supervelocidad conserva dash", Mathf.Abs(normalDash - boostedDash) < .01f && normalDash > 2.3f, normalDash.ToString("F3") + " / " + boostedDash.ToString("F3"));
            Check("Dash mantiene recarga", p.state.dashCooldown > 2.2f);
            for (int i = 0; i < 130; i++) { p.input = new InputFrame(); p.Tick(Dt); }
            Check("Recarga dash", p.state.dashCooldown == 0);
            p.ResetAt(new Vector3(-14, 1.1f, -13)); p.state.tagged = false; p.state.speedSeconds = .2f;
            for (int i = 0; i < 20; i++) { p.input = new InputFrame(); p.Tick(Dt); }
            var start = p.transform.position;
            for (int i = 0; i < 50; i++) { p.input = new InputFrame { x = 1 }; p.Tick(Dt); }
            Check("Expiracion vuelve a velocidad normal", p.state.speedSeconds == 0 && Mathf.Abs(p.transform.position.x - start.x - Rules.RunnerSpeed) < .1f);
            p.ResetAt(new Vector3(-12, 1.1f, -13));
            for (int i = 0; i < 15; i++) p.Tick(Dt);
            float ground = p.transform.position.y, peak = ground; p.input = new InputFrame { jump = true };
            for (int i = 0; i < 50; i++) { p.Tick(Dt); peak = Mathf.Max(peak, p.transform.position.y); }
            Check("Salto y aterrizaje", peak - ground > 1.2f && Mathf.Abs(p.transform.position.y - ground) < .1f, (peak - ground).ToString("F3"));
            p.ResetAt(new Vector3(16, 1.1f, -13));
            for (int i = 0; i < 100; i++) { p.input = new InputFrame { x = 1 }; p.Tick(Dt); }
            Check("Colision de limite", p.transform.position.x < 17.1f && p.transform.position.x > 16.5f, p.transform.position.x.ToString("F3"));
            p.ResetAt(new Vector3(-15, 1.1f, 0)); float rampPeak = 0;
            for (int i = 0; i < 115; i++) { p.input = new InputFrame { x = 1 }; p.Tick(Dt); rampPeak = Mathf.Max(rampPeak, p.transform.position.y); }
            Check("Rampa alcanza pasarela", rampPeak > 3.9f && p.transform.position.x > -4, p.transform.position.ToString());
        }
        void VerifyPads()
        {
            NewRound(MatchMode.Classic);
            Check("Tres baldosas disponibles", sim.pads.Length == 3 && sim.pads.All(p => p.cooldown == 0));
            EnterPad(victim, 0);
            Check("Baldosa concede diez segundos", victim.state.speedSeconds > 9.9f && victim.state.speedSeconds <= Rules.SpeedDuration);
            Check("Baldosa inicia recarga compartida", sim.pads[0].cooldown > 19.9f && sim.pads[0].cooldown <= Rules.PadRecharge);
            Step(505); Check("Buff termina antes que recarga", victim.state.speedSeconds == 0 && sim.pads[0].cooldown > 9.7f);
            Step(505); Check("Esperar encima no reactiva", victim.state.speedSeconds == 0 && sim.pads[0].cooldown == 0);
            EnterPad(victim, 0); Check("Salir y entrar reactiva", victim.state.speedSeconds > 9.9f);
            NewRound(MatchMode.Classic); EnterPad(victim, 0); Warp(victim, new Vector3(-14, 1.05f, -13)); EnterPad(helper, 0);
            Check("Otra persona respeta recarga", helper.state.speedSeconds == 0 && sim.pads[0].cooldown > 19.8f);
            NewRound(MatchMode.Classic); EnterPad(chaser, 0);
            Check("Perseguidores usan baldosas", chaser.state.speedSeconds > 9.9f);
            NewRound(MatchMode.Classic); var center = SpeedPad.Positions[0]; Warp(victim, new Vector3(center.x, 5, center.z)); Step();
            Check("Pasar por encima no activa", victim.state.speedSeconds == 0 && sim.pads[0].cooldown == 0);
            NewRound(MatchMode.Classic); EnterPad(victim, 0); Step(100); EnterPad(victim, 1);
            Check("Otra baldosa refresca sin acumular", victim.state.speedSeconds > 9.9f && victim.state.speedSeconds <= Rules.SpeedDuration);
            Step(502); Check("Buff refrescado expira", victim.state.speedSeconds == 0);
            NewRound(MatchMode.Infection); sim.TryTag(chaser.state.id, victim.state.id); EnterPad(victim, 0);
            Check("Atrapado no consume baldosas", victim.state.trapped && victim.state.speedSeconds == 0 && sim.pads[0].cooldown == 0);
            NewRound(MatchMode.Relay); victim.state.alive = false; EnterPad(victim, 0);
            Check("Eliminado no consume baldosas", victim.state.speedSeconds == 0 && sim.pads[0].cooldown == 0);
        }
        void VerifyRescue()
        {
            NewRound(MatchMode.Infection); bool caught = sim.TryTag(chaser.state.id, victim.state.id);
            Check("Captura crea estado atrapado", caught && chaser.state.tagged && victim.state.trapped && !victim.state.tagged && victim.state.alive && victim.state.trapSeconds > 9.9f);
            var start = victim.transform.position; victim.input = new InputFrame { x = 1, jump = true, dash = true, interact = true }; Step(10);
            Check("Atrapado no corre salta ni hace dash", Vector2.Distance(new Vector2(start.x, start.z), new Vector2(victim.transform.position.x, victim.transform.position.z)) < .001f && Mathf.Abs(victim.transform.position.y - start.y) < .1f && victim.state.dashCooldown == 0);
            Check("No hay captura repetida de atrapado", !sim.TryTag(chaser.state.id, victim.state.id) && sim.TagCount == 1);
            Check("Partida sigue con rescatador libre", sim.phase == MatchPhase.Playing);
            BeginRescue(); Step(RescueFrames(.5f));
            Check("E acumula medio rescate", victim.state.trapped && helper.state.rescueTargetId == victim.state.id && Mathf.Abs(helper.state.rescueProgress - Rules.RescueDuration * .5f) < .001f, helper.state.rescueProgress.ToString("F3"));
            Step(RescueFrames() - RescueFrames(.5f) - 1);
            Check("Rescate no termina antes de un segundo", victim.state.trapped && victim.state.rescuesUsed == 0 && helper.state.rescueProgress < Rules.RescueDuration);
            Step();
            Check("Un segundo libera al atrapado", Rules.RescueDuration == 1f && !victim.state.trapped && !victim.state.tagged && victim.state.rescuesUsed == 1 && victim.state.trapSeconds == 0);
            Check("Rescate limpia progreso", helper.state.rescueTargetId == -1 && helper.state.rescueProgress == 0);
            Check("Ambos conservan tres segundos de inmunidad", Rules.Immunity == 3f && Mathf.Abs(victim.state.safeSeconds - Rules.Immunity) < .001f && Mathf.Abs(helper.state.safeSeconds - Rules.Immunity) < .001f);
            Check("Inmunidad evita captura inmediata", !sim.TryTag(chaser.state.id, victim.state.id));
            for(int rescueNumber=2;rescueNumber<=3;rescueNumber++) {
                helper.input=new InputFrame();Step(Mathf.CeilToInt(Rules.Immunity/Dt)+1);
                bool recaptured=sim.TryTag(chaser.state.id,victim.state.id);
                Check("Captura "+rescueNumber+" vuelve a atrapar sin infectar",recaptured&&victim.state.trapped&&!victim.state.tagged&&Mathf.Abs(victim.state.trapSeconds-Rules.TrapDuration)<.001f);
                Check("Se permite rescatar de nuevo tras "+(rescueNumber-1)+" rescates",MatchSimulation.CanRescue(helper,victim));
                helper.input=new InputFrame{interact=true};Step(RescueFrames());
                Check("Rescate repetido "+rescueNumber+" libera en un segundo",!victim.state.trapped&&!victim.state.tagged&&victim.state.rescuesUsed==rescueNumber&&victim.state.safeSeconds>2.9f&&helper.state.safeSeconds>2.9f);
            }
            helper.input=new InputFrame();Step(Mathf.CeilToInt(Rules.Immunity/Dt)+1);
            Check("Tras varios rescates cada captura mantiene ventana completa",sim.TryTag(chaser.state.id,victim.state.id)&&victim.state.trapped&&!victim.state.tagged);
            Step(Mathf.CeilToInt(Rules.TrapDuration/Dt)-1);
            Check("Rescatado varias veces sigue atrapado antes de diez segundos",victim.state.trapped&&!victim.state.tagged);
            Step();
            Check("Tras varios rescates solo se infecta al vencer el plazo",victim.state.tagged&&!victim.state.trapped&&victim.state.rescuesUsed==3);
            BeginRescue(); Step(RescueFrames(.3f)); helper.input = new InputFrame(); Step();
            Check("Soltar E cancela", helper.state.rescueProgress == 0 && helper.state.rescueTargetId == -1 && victim.state.trapped);
            helper.input = new InputFrame { interact = true }; Step(RescueFrames(.8f));
            Check("Volver a pulsar empieza desde cero", victim.state.trapped && Mathf.Abs(helper.state.rescueProgress - Rules.RescueDuration * .8f) < .001f);
            Warp(helper, victim.transform.position + Vector3.right * 3); Step();
            Check("Alejarse cancela rescate", victim.state.trapped && helper.state.rescueProgress == 0 && helper.state.rescueTargetId == -1);
            Step(155); Check("E fuera de alcance no rescata", victim.state.trapped && helper.state.rescueProgress == 0);
            BeginRescue(); Step(RescueFrames(.3f));
            helper.lastInput = Time.realtimeSinceStartup - 1;
            Physics.SyncTransforms(); sim.Tick(Dt);
            Check("Perder paquetes suelta E y cancela", !helper.input.interact && helper.state.rescueProgress == 0 && helper.state.rescueTargetId == -1);
            BeginRescue(); Step(RescueFrames(.5f)); chaser.state.safeSeconds = 0;
            Check("Rescatador puede ser atrapado", sim.TryTag(chaser.state.id, helper.state.id)); Step();
            Check("Captura cancela interaccion", helper.state.trapped && helper.state.rescueProgress == 0 && helper.state.rescueTargetId == -1 && victim.state.trapped);
            NewRound(MatchMode.Infection); Warp(helper, chaser.transform.position + Vector3.left * 1.8f);
            helper.input = new InputFrame { interact = true }; Step(155);
            Check("Infectado inicial no se cura", chaser.state.tagged && !chaser.state.trapped && helper.state.rescueTargetId == -1 && helper.state.rescueProgress == 0);
            BeginRescue(); victim.state.trapSeconds = Rules.RescueDuration;
            Physics.SyncTransforms(); sim.Tick(Rules.RescueDuration);
            Check("Conversion tiene prioridad al vencer ambos plazos", victim.state.tagged && !victim.state.trapped && victim.state.rescuesUsed == 0);
            BeginRescue(); victim.state.trapSeconds = Rules.RescueDuration;
            Step(RescueFrames() - 1);
            Check("Plazos iguales siguen pendientes antes del ultimo fotograma", victim.state.trapped && !victim.state.tagged && victim.state.rescuesUsed == 0);
            Step();
            Check("Conversion conserva prioridad con pasos de 20 ms", victim.state.tagged && !victim.state.trapped && victim.state.rescuesUsed == 0);
        }
        void VerifyWalls()
        {
            BeginRescue(); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Muro_temporal_verificacion";
            wall.transform.position = (victim.transform.position + helper.transform.position) * .5f + Vector3.up * .25f;
            wall.transform.localScale = new Vector3(.2f, 3, 2); Physics.SyncTransforms();
            try {
                Step(155); Check("Pared bloquea rescate cercano", victim.state.trapped && helper.state.rescueProgress == 0 && helper.state.rescueTargetId == -1);
                wall.SetActive(false); Physics.SyncTransforms(); Step(RescueFrames());
                Check("Quitar pared permite rescatar", !victim.state.trapped && victim.state.rescuesUsed == 1);
            } finally { wall.SetActive(false); Destroy(wall); Physics.SyncTransforms(); }
        }
        void VerifyRolePermissions()
        {
            BeginRescue();
            helper.input=new InputFrame();Warp(helper,new Vector3(-12,1.05f,13));
            Warp(chaser,victim.transform.position+Vector3.right*1.8f);
            chaser.input=new InputFrame{interact=true};
            Check("Infectado no tiene permiso de rescate",!MatchSimulation.CanRescue(chaser,victim));
            Step(155);
            Check("E del infectado no libera al atrapado",victim.state.trapped&&victim.state.rescuesUsed==0&&chaser.state.rescueProgress==0&&chaser.state.rescueTargetId==-1);
            victim.input=new InputFrame{interact=true};Step(100);
            Check("Atrapado no puede curarse a si mismo",victim.state.trapped&&!MatchSimulation.CanRescue(victim,victim)&&victim.state.rescuesUsed==0);
        }
        void VerifyConversion()
        {
            NewRound(MatchMode.Infection); sim.TryTag(chaser.state.id, victim.state.id); Step(480);
            Check("Ventana de rescate dura diez segundos", victim.state.trapped && !victim.state.tagged && victim.state.trapSeconds > .3f);
            Step(30); Check("Sin rescate se convierte", !victim.state.trapped && victim.state.tagged && victim.state.trapSeconds == 0 && chaser.state.tagged);
            Check("Superviviente restante puede seguir", sim.phase == MatchPhase.Playing && !helper.state.tagged && !helper.state.trapped);
            NewRound(MatchMode.Infection); sim.TryTag(chaser.state.id, victim.state.id); chaser.state.safeSeconds = 0;
            sim.TryTag(chaser.state.id, helper.state.id); Step(); Check("Sin supervivientes libres gana infeccion", sim.phase == MatchPhase.Results);
            NewRound(MatchMode.Infection); sim.TryTag(chaser.state.id, victim.state.id); sim.timeLeft = .01f; Step();
            Check("Infeccion termina al agotar tiempo", sim.phase == MatchPhase.Results);
        }
        void VerifyOriginalModes()
        {
            NewRound(MatchMode.Classic); Warp(chaser, new Vector3(-12, 1.05f, -13)); Warp(victim, new Vector3(-11, 1.05f, -13)); Step();
            Check("Clasico contagia por contacto", !chaser.state.tagged && victim.state.tagged && !victim.state.trapped && sim.TagCount == 1);
            Check("Clasico mantiene inmunidad", Mathf.Abs(victim.state.safeSeconds - Rules.Immunity) < .001f && !sim.TryTag(victim.state.id, chaser.state.id));
            Warp(victim, new Vector3(14, 1.05f, -13)); Step(152);
            Check("Clasico permite pasar tras inmunidad", sim.TryTag(victim.state.id, chaser.state.id));
            sim.timeLeft = .01f; Step(); Check("Final clasico", sim.phase == MatchPhase.Results);
            NewRound(MatchMode.Classic); Warp(chaser, new Vector3(-12, 1.05f, -13)); Warp(victim, new Vector3(-10.95f, 1.05f, -13));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-11.475f, 1.5f, -13); wall.transform.localScale = new Vector3(.02f, 3, 2); Physics.SyncTransforms();
            try { Step(); Check("Pared impide contagio", chaser.state.tagged && !victim.state.tagged && sim.TagCount == 0); }
            finally { wall.SetActive(false); Destroy(wall); Physics.SyncTransforms(); }
            NewRound(MatchMode.Relay);
            Check("Relevo transfiere sin atrapar", sim.TryTag(chaser.state.id, victim.state.id) && !chaser.state.tagged && victim.state.tagged && !victim.state.trapped);
            sim.fuse = .01f; Step(); Check("Explosion elimina portador", !victim.state.alive && !victim.state.tagged && sim.phase == MatchPhase.Playing);
            sim.fuse = .01f; Step(); Check("Victoria relevo", sim.players.Values.Count(p => p.state.alive) == 1 && sim.phase == MatchPhase.Results);
        }
        void VerifySnapshotAndReset()
        {
            BeginRescue(); Step(RescueFrames(.5f)); chaser.state.speedSeconds = 5; sim.pads[0].cooldown = 7;
            var packet = JsonUtility.FromJson<Packet>(JsonUtility.ToJson(sim.Snapshot()));
            var copy = packet.players.Single(p => p.id == victim.state.id); var helperCopy = packet.players.Single(p => p.id == helper.state.id);
            Check("Snapshot versionado incluye baldosas", packet.version == Rules.ProtocolVersion && packet.pads != null && packet.pads.Length == 3 && Mathf.Abs(packet.pads[0].cooldown - 7) < .001f);
            Check("Snapshot conserva trampa y medio rescate", copy.trapped && Mathf.Abs(copy.trapSeconds - (Rules.TrapDuration - Rules.RescueDuration * .5f)) < .001f && helperCopy.rescueTargetId == victim.state.id && Mathf.Abs(helperCopy.rescueProgress - Rules.RescueDuration * .5f) < .001f);
            Check("Snapshot conserva supervelocidad", Mathf.Abs(packet.players.Single(p => p.id == chaser.state.id).speedSeconds - 5) < .001f);
            var input = JsonUtility.FromJson<Packet>(JsonUtility.ToJson(new Packet { type = "input", version = Rules.ProtocolVersion, input = new InputFrame { interact = true } }));
            Check("Paquete LAN conserva E mantenida", input.version == Rules.ProtocolVersion && input.input.interact);
            victim.state.rescuesUsed = 1; sim.StartRound(MatchMode.Classic);
            Check("Revancha reinicia estados nuevos", sim.players.Values.All(p => p.state.alive && !p.state.trapped && p.state.trapSeconds == 0 && p.state.rescuesUsed == 0 && p.state.speedSeconds == 0 && p.state.rescueTargetId == -1 && p.state.rescueProgress == 0));
            Check("Revancha reinicia baldosas y reloj", sim.pads.All(p => p.cooldown == 0) && sim.players.Values.All(p => p.state.taggedSeconds == 0) && sim.timeLeft == Rules.RoundSeconds);
            sim.Remove(2); sim.Remove(1); Check("Desconexion cierra ronda sin rivales", sim.phase == MatchPhase.Results);
        }
        void VerifyRescueContention()
        {
            sim.StartRound(MatchMode.Infection);
            chaser = sim.players.Values.Single(p => p.state.tagged);
            var runners = sim.players.Values.Where(p => !p.state.tagged).OrderBy(p => p.state.id).ToArray();
            var newcomer = runners[0]; victim = runners[1]; helper = runners[2];
            foreach (var p in sim.players.Values) { p.input = new InputFrame(); p.state.safeSeconds = 0; }
            Warp(chaser, new Vector3(14, 1.05f, -13)); Warp(victim, new Vector3(-12, 1.05f, -13));
            Warp(helper, new Vector3(-10.2f, 1.05f, -13)); Warp(newcomer, new Vector3(-12, 1.05f, 13));
            sim.TryTag(chaser.state.id, victim.state.id);
            helper.input = new InputFrame { interact = true }; Step(RescueFrames(.5f));
            Warp(newcomer, victim.transform.position + Vector3.forward * 1.8f);
            newcomer.input = new InputFrame { interact = true }; Step();
            Check("Nuevo rescatador no roba progreso existente", newcomer.state.id < helper.state.id && helper.state.rescueProgress > Rules.RescueDuration * .5f && helper.state.rescueProgress < Rules.RescueDuration && newcomer.state.rescueProgress == 0);
            Step(RescueFrames() - RescueFrames(.5f) - 1);
            Check("Rescatador original completa pese a competencia", !victim.state.trapped && victim.state.rescuesUsed == 1 && helper.state.safeSeconds > 2.8f && newcomer.state.safeSeconds == 0);
        }
        IEnumerator Start()
        {
            yield return new WaitForSeconds(.2f); GetComponent<PilleGame>().Leave();
            sim = new MatchSimulation(); sim.Add(0, "A", false); sim.Add(1, "B", false); sim.Add(2, "C", false);
            // PlayerMotor.Create removes primitive mesh colliders at frame end.
            yield return null;
            Run("Motor", VerifyMotor); Run("Baldosas", VerifyPads); Run("Rescate", VerifyRescue);
            Run("Rescate con paredes", VerifyWalls); Run("Permisos de cada papel",VerifyRolePermissions); Run("Conversion", VerifyConversion);
            Run("Modos originales", VerifyOriginalModes);
            sim.Add(3, "D", false); yield return null;
            Run("Dos rescatadores", VerifyRescueContention); sim.Remove(3); yield return null;
            Run("Snapshot y revancha", VerifySnapshotAndReset);
            sim.Clear(); sim = null; int total = results.Count;
            results.Add("TOTAL: " + (total - failed) + " correctas / " + failed + " fallidas");
            File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../verificacion.txt")), results.ToArray());
            Debug.Log("PILLE_TEST_COMPLETE failed=" + failed + " total=" + total); Application.Quit(failed == 0 ? 0 : 1);
        }
    }
}

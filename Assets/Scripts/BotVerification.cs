using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    // Opt-in native player integration checks (-pilleBotTest). These move the real
    // CharacterControllers against ArenaBuilder colliders; input-only assertions
    // would miss the original bug where a bot pressed forward forever at a wall.
    public sealed class BotVerification : MonoBehaviour
    {
        const float Dt = .02f;
        readonly List<string> results = new List<string>();
        MatchSimulation sim;
        int failed;
        float simulatedSeconds;
        void Check(string name, bool condition, string detail = "")
        {
            string line = (condition ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " | " + detail : "");
            results.Add(line); Debug.Log(line); if (!condition) failed++;
        }
        void Run(string name, Action test)
        {
            try { test(); } catch (Exception error) { Check(name + " sin excepciones", false, error.ToString()); }
        }
        void Cleanup()
        {
            if (sim == null) return;
            foreach (var p in sim.players.Values) p.gameObject.SetActive(false);
            sim.Clear(); sim = null; Physics.SyncTransforms();
        }
        void NewRound(MatchMode mode, params bool[] bots)
        {
            Cleanup(); sim = new MatchSimulation();
            for (int i = 0; i < bots.Length; i++) sim.Add(i, "Verificacion " + i, bots[i]);
            sim.StartRound(mode, 0);
            foreach (var player in sim.players.Values) player.state.safeSeconds = 0;
        }
        void Warp(int id, float x, float z, float y = 1.06f)
        {
            sim.players[id].ResetAt(new Vector3(x, y, z)); Physics.SyncTransforms();
        }
        void Step()
        {
            foreach (var p in sim.players.Values) p.lastInput = Time.realtimeSinceStartup;
            Physics.SyncTransforms(); sim.Tick(Dt); simulatedSeconds += Dt;
        }
        static float Horizontal(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

        void VerifyPursuit()
        {
            NewRound(MatchMode.Classic, true, false); Warp(0, -8, -8); Warp(1, -2, -8);
            Vector3 previous = sim.players[0].transform.position; float distance = 0, detour = 0;
            for (int i = 0; i < 600 && sim.TagCount == 0; i++) {
                Step(); var position = sim.players[0].transform.position;
                distance += Horizontal(position, previous); detour = Mathf.Max(detour, Mathf.Abs(position.z + 8)); previous = position;
            }
            Check("Bot rodea cobertura sur real", detour > 3.3f && distance > 8, "desvio=" + detour.ToString("F2") + " recorrido=" + distance.ToString("F2"));
            Check("Persecucion termina en captura por contacto", sim.TagCount > 0 && sim.players[1].state.tagged, "capturas=" + sim.TagCount);
        }
        void VerifyCornerEscape()
        {
            NewRound(MatchMode.Classic, false, true); Warp(0, -14, -14); Warp(1, -16, -16);
            sim.players[1].state.safeSeconds = 100;
            Vector3 previous = sim.players[1].transform.position; float distance = 0, still = 0, longestStill = 0;
            bool inside = true;
            for (int i = 0; i < 300; i++) {
                Step(); var position = sim.players[1].transform.position; float movement = Horizontal(position, previous);
                distance += movement; still = movement < .008f ? still + Dt : 0; longestStill = Mathf.Max(still, longestStill);
                inside &= Mathf.Abs(position.x) < 17.2f && Mathf.Abs(position.z) < 17.2f; previous = position;
            }
            Check("Evasor sale de esquina sin quedarse quieto", distance > 16 && longestStill < 1.6f,
                "recorrido=" + distance.ToString("F2") + " quieto=" + longestStill.ToString("F2"));
            Check("Escape respeta muros exteriores", inside);
        }
        void VerifySustainedPlay()
        {
            NewRound(MatchMode.Classic, true, true, true, true);
            var distances = new float[4]; var still = new float[4]; var longest = new float[4];
            var previous = sim.players.Values.OrderBy(p => p.state.id).Select(p => p.transform.position).ToArray();
            for (int frame = 0; frame < 1500; frame++) {
                Step();
                for (int id = 0; id < 4; id++) {
                    Vector3 position = sim.players[id].transform.position; float movement = Horizontal(position, previous[id]);
                    distances[id] += movement; still[id] = movement < .008f ? still[id] + Dt : 0;
                    longest[id] = Mathf.Max(longest[id], still[id]); previous[id] = position;
                }
            }
            for (int id = 0; id < 4; id++) Check("Bot " + id + " mantiene movimiento durante 30 s", distances[id] > 45 && longest[id] < 3,
                "recorrido=" + distances[id].ToString("F2") + " quieto_max=" + longest[id].ToString("F2"));
            Check("Partida autonoma conserva reglas y estado valido", sim.phase == MatchPhase.Playing
                && sim.players.Values.Count(p => p.state.tagged) == 1
                && sim.players.Values.All(p => p.state.position.y > .8f && Mathf.Abs(p.state.position.x) < 17.2f && Mathf.Abs(p.state.position.z) < 17.2f));
        }
        void VerifyRescue()
        {
            NewRound(MatchMode.Infection, false, false, true);
            Warp(0, 14, 14); Warp(1, -2, -8); Warp(2, -8, -8);
            Check("Fixture captura atrapado", sim.TryTag(0, 1));
            bool interacted = false; float distance = 0; Vector3 previous = sim.players[2].transform.position;
            for (int frame = 0; frame < 450 && sim.players[1].state.trapped; frame++) {
                Step(); interacted |= sim.players[2].input.interact;
                distance += Horizontal(previous, sim.players[2].transform.position); previous = sim.players[2].transform.position;
            }
            Check("Superviviente bot llega al aliado rodeando pared", distance > 8 && interacted, "recorrido=" + distance.ToString("F2"));
            Check("Bot completa rescate real de un segundo", Rules.RescueDuration == 1f && !sim.players[1].state.trapped && !sim.players[1].state.tagged
                && sim.players[1].state.rescuesUsed == 1 && sim.players[2].state.safeSeconds > 2.8f);
            Warp(0,14,14);Warp(1,-2,-8);Warp(2,-8,-8);
            foreach(var player in sim.players.Values)player.state.safeSeconds=0;
            bool caughtAgain=sim.TryTag(0,1);
            for(int frame=0;frame<450&&sim.players[1].state.trapped;frame++)Step();
            Check("Bot vuelve a salvar al mismo aliado tras una nueva captura",caughtAgain&&!sim.players[1].state.trapped&&!sim.players[1].state.tagged&&sim.players[1].state.rescuesUsed==2&&sim.players[2].state.safeSeconds>2.8f);
        }
        void VerifyInfectedRole()
        {
            NewRound(MatchMode.Infection, true, false, false); Warp(0, -8, -8); Warp(1, -2, -8); Warp(2, 14, 14);
            sim.TryTag(0, 1); bool interact = false;
            for (int frame = 0; frame < 150; frame++) { Step(); interact |= sim.players[0].input.interact; }
            Check("Bot infectado persigue y nunca intenta curar", !interact && sim.players[0].state.tagged
                && sim.players[1].state.trapped && sim.players[1].state.rescuesUsed == 0);
        }
        void VerifyRaisedWalkway()
        {
            NewRound(MatchMode.Classic, true, false); Warp(0, -15, 0); Warp(1, 0, 0, 4.06f);
            float peak = 0;
            for (int frame = 0; frame < 500 && sim.TagCount == 0; frame++) { Step(); peak = Mathf.Max(peak, sim.players[0].transform.position.y); }
            Check("Bot utiliza rampa hacia pasarela", peak > 3.8f, "altura=" + peak.ToString("F2"));
            Check("Bot captura objetivo sobre pasarela", sim.TagCount > 0 && sim.players[1].state.tagged);
        }
        void VerifySpeedPad()
        {
            NewRound(MatchMode.Classic, true, false); Warp(0, -14, -13); Warp(1, -2, -13);
            bool boosted = false;
            for (int frame = 0; frame < 200 && sim.TagCount == 0; frame++) { Step(); boosted |= sim.players[0].state.speedSeconds > 0; }
            Check("Bot aprovecha baldosa durante persecucion", boosted && sim.pads[0].cooldown > 0);
            Check("Baldosa permite mantener persecucion valida", sim.TagCount > 0);
        }
        void VerifyPracticeRole()
        {
            NewRound(MatchMode.Infection, false, true, true);
            sim.StartRound(MatchMode.Infection, 2);
            Check("Practica puede empezar con humano superviviente", !sim.players[0].state.tagged && sim.players[2].state.tagged);
            sim.StartRound(MatchMode.Classic);
            Check("Inicio sin preferencia conserva rotacion original", sim.players[2].state.tagged);
        }
        IEnumerator Start()
        {
            yield return new WaitForSeconds(.2f); GetComponent<PilleGame>().Leave(); yield return null;
            Run("Persecucion", VerifyPursuit); Run("Esquina", VerifyCornerEscape);
            Run("Movimiento sostenido", VerifySustainedPlay); Run("Rescate autonomo", VerifyRescue);
            Run("Rol infectado", VerifyInfectedRole); Run("Pasarela", VerifyRaisedWalkway);
            Run("Baldosa", VerifySpeedPad); Run("Rol de practica", VerifyPracticeRole);
            Cleanup(); int total = results.Count;
            results.Add("TOTAL: " + (total - failed) + " correctas / " + failed + " fallidas | segundos_simulados=" + simulatedSeconds.ToString("F2"));
            File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../verificacion_bots.txt")), results.ToArray());
            Debug.Log("PILLE_BOT_TEST_COMPLETE failed=" + failed + " total=" + total + " seconds=" + simulatedSeconds.ToString("F2"));
            Application.Quit(failed == 0 ? 0 : 1);
        }
    }
}

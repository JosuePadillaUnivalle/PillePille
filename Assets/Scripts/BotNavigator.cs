using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    // Navigation is shared by all host-controlled bots. The graph is sampled from the
    // real colliders, including both the ground and the raised walkway/ramp surfaces.
    // Players are deliberately excluded: they must never permanently block a route.
    public sealed class BotNavigator
    {
        sealed class Brain
        {
            public readonly List<Vector3> route = new List<Vector3>();
            public Vector3 previous, destination, escape;
            public float decide, still, clock, escapeUntil;
            public int waypoint, target = -1, decisions;
            public bool initialized;
        }
        readonly Dictionary<int, Brain> brains = new Dictionary<int, Brain>();
        NavGraph graph;
        public void Reset() { brains.Clear(); graph = null; }
        public void Remove(int id) { brains.Remove(id); }

        public InputFrame Input(PlayerMotor player, IDictionary<int, PlayerMotor> players, MatchMode mode, PadState[] pads, float dt)
        {
            if (!player.state.alive || player.state.trapped) return new InputFrame();
            if (graph == null) graph = new NavGraph();
            if (!brains.TryGetValue(player.state.id, out var brain)) {
                brain = new Brain(); brains.Add(player.state.id, brain);
            }
            Vector3 position = player.transform.position;
            if (!brain.initialized) { brain.previous = position; brain.initialized = true; }
            brain.clock += dt; brain.decide -= dt;
            float travelled = HorizontalDistance(position, brain.previous);
            brain.previous = position;
            brain.still = travelled < dt * .8f ? brain.still + dt : Mathf.Max(0, brain.still - dt * 3);

            var enemies = players.Values.Where(p => p != player && p.state.alive && !p.state.trapped
                && (player.state.tagged ? !p.state.tagged : p.state.tagged))
                .OrderBy(p => (p.transform.position - position).sqrMagnitude).ToArray();
            PlayerMotor threat = !player.state.tagged && enemies.Length > 0 ? enemies[0] : null;
            float danger = threat == null ? 100 : Vector3.Distance(position, threat.transform.position);
            PlayerMotor rescue = null;
            if (mode == MatchMode.Infection && !player.state.tagged) {
                // A trapped ally is a separate state, never an infected player.
                // Allow a risky rescue when there is enough time to reach and free them.
                rescue = players.Values.Where(p => p != player && p.state.alive && p.state.trapped && !p.state.tagged
                    && p.state.trapSeconds > Rules.RescueDuration + .2f
                    && p.state.trapSeconds > Vector3.Distance(position, p.transform.position) / Rules.RunnerSpeed + Rules.RescueDuration)
                    .OrderBy(p => (p.transform.position - position).sqrMagnitude).FirstOrDefault();
                if (rescue != null && danger < 3.5f && player.state.rescueProgress <= 0) rescue = null;
                if (rescue != null && MatchSimulation.CanRescue(player, rescue)) {
                    brain.still = 0; brain.decide = 0;
                    return new InputFrame { interact = true };
                }
            }

            int target = rescue != null ? rescue.state.id : player.state.tagged && enemies.Length > 0 ? enemies[0].state.id : -1;
            bool atDestination = HorizontalDistance(position, brain.destination) < 1.1f;
            if (brain.decide <= 0 || brain.target != target || brain.route.Count == 0 || atDestination || brain.still > .6f) {
                brain.decisions++; brain.target = target;
                Vector3 goal;
                if (rescue != null) goal = rescue.transform.position;
                else if (player.state.tagged && enemies.Length > 0) {
                    var prey = enemies[0]; goal = prey.transform.position;
                    // A short lead makes pursuit intercept a running player, without
                    // reading future input or increasing the bot's movement speed.
                    var lead = new Vector3(prey.input.x, 0, prey.input.z);
                    if (Vector3.Distance(position, goal) > 3) goal += lead * 1.0f;
                }
                else goal = ChooseDestination(player, brain, threat, pads);
                brain.destination = goal; brain.waypoint = 0;
                graph.Path(position, goal, brain.route);
                brain.decide = rescue != null || player.state.tagged ? .35f : 1.15f;
                if (brain.still > .6f) {
                    // A persistent obstruction (including another character) triggers a
                    // deliberate lateral escape and a fresh route; never teleport.
                    Vector3 forward = brain.route.Count > 0 ? brain.route[0] - position : goal - position;
                    forward.y = 0;
                    brain.escape = FindEscape(position, forward, player.state.id + brain.decisions);
                    brain.escapeUntil = brain.clock + .55f;
                    brain.still = 0;
                }
            }

            while (brain.waypoint < brain.route.Count && HorizontalDistance(position, brain.route[brain.waypoint]) < .55f
                && Mathf.Abs(position.y - brain.route[brain.waypoint].y) < .75f) brain.waypoint++;
            // Smooth only through genuinely clear capsule-width corridors; a ray down
            // the centre alone lets bots cut corners and get stuck on their shoulders.
            while (brain.waypoint + 1 < brain.route.Count && NavGraph.Clear(position, brain.route[brain.waypoint + 1])) brain.waypoint++;
            Vector3 desired = brain.waypoint < brain.route.Count ? brain.route[brain.waypoint] - position : brain.destination - position;
            if (brain.clock < brain.escapeUntil) desired = brain.escape;
            desired.y = 0;
            if (desired.sqrMagnitude < .03f) desired = FindEscape(position, Vector3.forward, player.state.id + brain.decisions);
            desired.Normalize();
            bool clear = NavGraph.Clear(position, position + desired * 1.6f);
            // Dash only along a clear lane. Jump is a recovery tool rather than an
            // every-frame response that repeatedly jumps against the same wall.
            bool jump = !clear && brain.clock < brain.escapeUntil && player.state.grounded;
            bool dash = rescue == null && clear && (player.state.tagged || danger < 7) && player.state.dashCooldown <= 0;
            return new InputFrame { x = desired.x, z = desired.z, jump = jump, dash = dash };
        }

        Vector3 ChooseDestination(PlayerMotor player, Brain brain, PlayerMotor threat, PadState[] pads)
        {
            Vector3 position = player.transform.position;
            int start = graph.Nearest(position);
            if (start < 0) return position + Vector3.forward;
            int component = graph.nodes[start].component;
            Vector3 best = graph.nodes[start].position;
            float bestScore = float.NegativeInfinity;
            // Deterministic sampling spreads bots across the arena and avoids all of
            // them choosing one corner. Recompute a route, rather than fleeing straight
            // into a wall whenever the chaser changes direction.
            for (int sample = 0; sample < 44; sample++) {
                int index = (int)(((long)(player.state.id + 19) * 193 + (long)brain.decisions * 317 + sample * 83) % graph.nodes.Count);
                var node = graph.nodes[index];
                if (node.component != component || node.neighbors.Count < 3) continue;
                float travel = HorizontalDistance(position, node.position);
                if (travel < 5 || travel > 15) continue;
                float score = travel * .15f + Mathf.Min(node.neighbors.Count, 8) * .15f;
                if (threat != null) {
                    Vector3 away = position - threat.transform.position; away.y = 0;
                    Vector3 direction = node.position - position; direction.y = 0;
                    float separation = HorizontalDistance(node.position, threat.transform.position);
                    score += separation * (HorizontalDistance(position, threat.transform.position) < 10 ? 1.7f : .6f);
                    score += Vector3.Dot(direction.normalized, away.normalized) * 4;
                    // Do not choose a destination whose first stretch runs directly
                    // through a nearby pursuer, even if the far endpoint is safe.
                    if (Vector3.Dot(direction.normalized, away.normalized) < -.2f && away.magnitude < 6) score -= 20;
                }
                else score += Mathf.Sin(sample * 2.7f + brain.decisions) * 2;
                if (player.state.speedSeconds <= 1 && pads != null) {
                    foreach (var pad in pads) if (pad.cooldown <= 0 && HorizontalDistance(node.position, SpeedPad.Positions[pad.id]) < 2.1f) score += 3;
                }
                if (score > bestScore) { bestScore = score; best = node.position; }
            }
            return best;
        }

        static Vector3 FindEscape(Vector3 position, Vector3 forward, int seed)
        {
            forward.y = 0; if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();
            int side = seed % 2 == 0 ? 1 : -1;
            foreach (float angle in new[] { 65f * side, -65f * side, 110f * side, -110f * side, 180f, 0f }) {
                Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
                if (NavGraph.Clear(position, position + direction * 1.5f)) return direction;
            }
            return Quaternion.Euler(0, 110f * side, 0) * forward;
        }
        static float HorizontalDistance(Vector3 a, Vector3 b) { return new Vector2(a.x - b.x, a.z - b.z).magnitude; }

        sealed class NavGraph
        {
            public sealed class Node
            {
                public Vector3 position;
                public readonly List<int> neighbors = new List<int>(8);
                public int component = -1;
            }
            public readonly List<Node> nodes = new List<Node>();
            const int Size = 43;
            const float Cell = .8f, Origin = -16.8f;
            readonly List<int>[] cells = new List<int>[Size * Size];
            public NavGraph()
            {
                Physics.SyncTransforms();
                for (int z = 0; z < Size; z++) for (int x = 0; x < Size; x++) {
                    var cell = new List<int>(2); cells[x + z * Size] = cell;
                    var top = new Vector3(Origin + x * Cell, 7, Origin + z * Cell);
                    foreach (var hit in Physics.RaycastAll(top, Vector3.down, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .OrderBy(h => h.point.y)) {
                        if (!Obstacle(hit.collider) || hit.normal.y < .68f) continue;
                        Vector3 position = hit.point + Vector3.up * 1.08f;
                        if (cell.Any(i => Mathf.Abs(nodes[i].position.y - position.y) < .15f) || !Fits(position)) continue;
                        cell.Add(nodes.Count); nodes.Add(new Node { position = position });
                    }
                }
                for (int z = 0; z < Size; z++) for (int x = 0; x < Size; x++) foreach (int from in cells[x + z * Size]) {
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++) {
                        int nx = x + dx, nz = z + dz;
                        if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= Size || nz >= Size) continue;
                        foreach (int to in cells[nx + nz * Size]) {
                            if (Mathf.Abs(nodes[from].position.y - nodes[to].position.y) > .65f) continue;
                            if (Clear(nodes[from].position, nodes[to].position)) nodes[from].neighbors.Add(to);
                        }
                    }
                }
                int component = 0;
                foreach (var node in nodes) if (node.component < 0) {
                    var pending = new Queue<int>(); int index = nodes.IndexOf(node); node.component = component; pending.Enqueue(index);
                    while (pending.Count > 0) foreach (int neighbor in nodes[pending.Dequeue()].neighbors) if (nodes[neighbor].component < 0) {
                        nodes[neighbor].component = component; pending.Enqueue(neighbor);
                    }
                    component++;
                }
            }
            static bool Obstacle(Collider collider)
            {
                return collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.layer != 2
                    && collider.GetComponentInParent<PlayerMotor>() == null;
            }
            static bool Fits(Vector3 position)
            {
                return !Physics.OverlapCapsule(position + Vector3.up * .42f, position - Vector3.up * .34f, .54f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(Obstacle);
            }
            public static bool Clear(Vector3 from, Vector3 to)
            {
                Vector3 delta = to - from;
                if (Mathf.Abs(delta.y) > .6f && new Vector2(delta.x, delta.z).magnitude < Mathf.Abs(delta.y)) return false;
                if (delta.sqrMagnitude < .0001f) return true;
                return !Physics.CapsuleCastAll(from + Vector3.up * .42f, from - Vector3.up * .34f, .54f, delta.normalized,
                    delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(hit => Obstacle(hit.collider));
            }
            public int Nearest(Vector3 position, int component = -1)
            {
                int best = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < nodes.Count; i++) {
                    if (nodes[i].neighbors.Count == 0 || (component >= 0 && nodes[i].component != component)) continue;
                    float d = (nodes[i].position - position).sqrMagnitude;
                    if (d < distance) { best = i; distance = d; }
                }
                return best;
            }
            public void Path(Vector3 from, Vector3 destination, List<Vector3> result)
            {
                result.Clear();
                // Direct approach is important within rescue range and for contact.
                if (Mathf.Abs(from.y - destination.y) < .65f && Clear(from, destination)) { result.Add(destination); return; }
                int start = Nearest(from);
                if (start < 0) { result.Add(destination); return; }
                int goal = Nearest(destination, nodes[start].component);
                if (goal < 0) return;
                var cost = new float[nodes.Count]; var previous = new int[nodes.Count]; var closed = new bool[nodes.Count];
                for (int i = 0; i < nodes.Count; i++) { cost[i] = float.PositiveInfinity; previous[i] = -1; }
                var open = new MinHeap(); cost[start] = 0; open.Add(start, 0);
                while (open.Count > 0) {
                    int current = open.Pop(); if (closed[current]) continue; closed[current] = true;
                    if (current == goal) break;
                    foreach (int next in nodes[current].neighbors) {
                        float candidate = cost[current] + Vector3.Distance(nodes[current].position, nodes[next].position);
                        if (candidate >= cost[next]) continue;
                        cost[next] = candidate; previous[next] = current;
                        open.Add(next, candidate + Vector3.Distance(nodes[next].position, nodes[goal].position));
                    }
                }
                if (!closed[goal]) return;
                for (int node = goal; node >= 0 && node != start; node = previous[node]) result.Add(nodes[node].position);
                result.Reverse();
                // Never append an unreachable destination on top of a wall/island.
                if (result.Count == 0) result.Add(nodes[goal].position);
            }
            sealed class MinHeap
            {
                struct Item { public int node; public float score; }
                readonly List<Item> items = new List<Item>();
                public int Count { get { return items.Count; } }
                public void Add(int node, float score)
                {
                    var item = new Item { node = node, score = score }; items.Add(item); int index = items.Count - 1;
                    while (index > 0) { int parent = (index - 1) / 2; if (items[parent].score <= score) break; items[index] = items[parent]; index = parent; }
                    items[index] = item;
                }
                public int Pop()
                {
                    int node = items[0].node; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                    if (items.Count == 0) return node;
                    int index = 0;
                    while (index * 2 + 1 < items.Count) {
                        int child = index * 2 + 1;
                        if (child + 1 < items.Count && items[child + 1].score < items[child].score) child++;
                        if (last.score <= items[child].score) break;
                        items[index] = items[child]; index = child;
                    }
                    items[index] = last; return node;
                }
            }
        }
    }
}

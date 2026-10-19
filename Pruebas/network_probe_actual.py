"""Read-only protocol probe. Run against -pilleHost -pilleNetAudit on localhost.

The host owns game state; this script only sends ordinary client packets.
Startup + validation normally takes <15 seconds. Does not launch game processes.
"""
import argparse
import json
import math
import pathlib
import socket
import threading
import time

parser = argparse.ArgumentParser()
parser.add_argument('--host', default='127.0.0.1')
parser.add_argument('--port', type=int, default=7777)
parser.add_argument('--out', default=str(pathlib.Path(__file__).with_suffix('.txt')))
args = parser.parse_args()
results = []


def check(name, ok, detail=''):
    line = ('PASS ' if ok else 'FAIL ') + name + (' | ' + str(detail) if detail else '')
    print(line, flush=True)
    results.append(line)


class Peer:
    def __init__(self, name, version):
        self.sock = socket.create_connection((args.host, args.port), timeout=4)
        self.sock.settimeout(None)
        self.latest = None
        self.assigned = None
        self.rejected = None
        self.states = []
        self.error = None
        self.condition = threading.Condition()
        self.closed = False
        self.thread = threading.Thread(target=self.read, daemon=True)
        self.thread.start()
        self.send({'type': 'hello', 'name': name, 'version': version})

    def send(self, packet):
        self.sock.sendall((json.dumps(packet, separators=(',', ':')) + '\n').encode('utf-8'))

    def read(self):
        try:
            with self.sock.makefile('r', encoding='utf-8-sig') as stream:
                for line in stream:
                    packet = json.loads(line)
                    with self.condition:
                        kind = packet.get('type')
                        if kind == 'welcome':
                            self.assigned = packet['id']
                        elif kind == 'rejected':
                            self.rejected = packet.get('message')
                        elif kind == 'state':
                            self.latest = packet
                            self.states.append((time.monotonic(), packet))
                        self.condition.notify_all()
        except (OSError, ValueError) as exc:
            if not self.closed:
                self.error = str(exc)

    def wait(self, predicate, timeout):
        deadline = time.monotonic() + timeout
        with self.condition:
            while not predicate():
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return False
                self.condition.wait(min(remaining, .1))
            return True

    def me(self):
        return next(p for p in self.latest['players'] if p['id'] == self.assigned)

    def close(self):
        self.closed = True
        try:
            self.sock.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass
        self.sock.close()


peer = None
try:
    old = Peer('VersionAntigua', 2)
    try:
        rejected = old.wait(lambda: old.rejected is not None, 1.5)
        check('Version 2 explicitly rejected', rejected and old.assigned is None, old.rejected)
    finally:
        old.close()

    peer = Peer('PruebaLAN', 3)
    ready = peer.wait(lambda: peer.assigned is not None and peer.latest is not None and peer.latest.get('phase') == 1, 8)
    check('Version 3 welcome and playing snapshot', ready, 'id=' + str(peer.assigned))
    if not ready:
        raise RuntimeError('Host must run -pilleHost -pilleNetAudit; join before round starts. ' + str(peer.rejected or peer.error))
    check('Version 3 and three shared pads', peer.latest.get('version') == 3 and len(peer.latest.get('pads', [])) == 3)
    fields = {'trapped', 'trapSeconds', 'rescueProgress', 'speedSeconds', 'rescueTargetId', 'rescuesUsed'}
    check('Snapshot carries all new rescue and speed fields', all(fields <= set(p) for p in peer.latest['players']))
    check('Old version did not create player', not any(p['name'] == 'VersionAntigua' for p in peer.latest['players']))

    # Every corner spawn has a clear horizontal lane along z=+/-12.
    start = dict(peer.me()['position'])
    direction = -1 if start['x'] >= 0 else 1
    for _ in range(20):
        peer.send({'type': 'input', 'input': {'x': direction, 'z': 0, 'interact': False}})
        time.sleep(.02)
    peer.send({'type': 'input', 'input': {}})
    time.sleep(.15)
    moved = direction * (peer.me()['position']['x'] - start['x'])
    check('Remote movement in clear lane', 1.3 < moved < 4.5, round(moved, 3))
    check('Grounded remote player', .8 < peer.me()['position']['y'] < 1.3, peer.me()['position']['y'])

    sample_start = len(peer.states)
    peer.send({'type': 'input', 'input': {'x': direction, 'jump': True, 'dash': True, 'interact': True}})
    for _ in range(12):
        peer.send({'type': 'input', 'input': {'x': direction, 'interact': True}})
        time.sleep(.02)
    samples = [next(p for p in state['players'] if p['id'] == peer.assigned) for _, state in peer.states[sample_start:]]
    check('Remote jump visible in snapshots', any(p['position']['y'] > 1.6 for p in samples))
    check('Remote dash cooldown visible', any(p['dashCooldown'] > 2 for p in samples))
    # Allow held x and E to expire through server's 0.5 s stale-input rule.
    time.sleep(.85)
    stopped = dict(peer.me()['position'])
    time.sleep(.3)
    check('Stale input stops horizontal movement', abs(peer.me()['position']['x'] - stopped['x']) < .15)
    check('No invalid rescue from distant E', peer.me()['rescueProgress'] == 0 and peer.me()['rescueTargetId'] == -1)

    # Send a forged snapshot. Only regular input is accepted for authenticated peers.
    rescues_before = peer.me()['rescuesUsed']
    peer.send({'type': 'state', 'players': [{'id': peer.assigned, 'position': {'x': 999, 'y': 999, 'z': 999}, 'speedSeconds': 999, 'rescuesUsed': 999}]})
    time.sleep(.15)
    check('Forged state cannot overwrite authoritative fields', abs(peer.me()['position']['x']) < 18 and peer.me()['speedSeconds'] <= 10 and peer.me()['rescuesUsed'] == rescues_before)

    # For north/south corner spawns, follow their clear outer lane to a known pad.
    # Existing cooldown is shared: a bot may legitimately consume it first.
    z = peer.me()['position']['z']
    target_x = 8 if z > 10 else -8
    target_pad = 1 if z > 10 else 0
    deadline = time.monotonic() + 4
    reached = False
    while time.monotonic() < deadline:
        player = peer.me()
        if player['speedSeconds'] > 0:
            reached = True
            break
        dx = target_x - player['position']['x']
        peer.send({'type': 'input', 'input': {'x': max(-1, min(1, dx * 2)), 'z': 0}})
        time.sleep(.02)
    peer.send({'type': 'input', 'input': {}})
    time.sleep(.1)
    check('Remote player activates real speed pad', reached, 'speed=' + str(peer.me()['speedSeconds']))
    check('Pad recharge replicated', any(p['id'] == target_pad and 0 < p['cooldown'] <= 20 for p in peer.latest['pads']))
    check('State stream remains live', len(peer.states) > 20 and peer.error is None, len(peer.states))
except Exception as exc:
    check('Probe completed', False, repr(exc))
finally:
    if peer is not None:
        peer.close()
    pathlib.Path(args.out).write_text('\n'.join(results) + '\n', encoding='utf-8')
raise SystemExit(1 if any(line.startswith('FAIL ') for line in results) else 0)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PillePille
{
    // Socket threads only exchange strings. Unity and game rules stay on the main thread.
    public sealed class LanTransport : IDisposable
    {
        public struct Event { public int peer; public string json; public string kind; }
        sealed class Connection
        {
            public int id;
            public TcpClient socket;
            public readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
            public volatile bool live = true;
            public readonly AutoResetEvent signal = new AutoResetEvent(false);
        }
        public readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
        readonly ConcurrentDictionary<int, Connection> peers = new ConcurrentDictionary<int, Connection>();
        TcpListener listener;
        volatile bool running;
        int sequence;
        public bool IsServer { get; private set; }

        public void Host(int port)
        {
            IsServer = true;
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;
            new Thread(AcceptLoop) { IsBackground = true }.Start();
        }
        public void Join(string address, int port)
        {
            running = true;
            new Thread(() => {
                TcpClient socket = new TcpClient();
                try {
                    var result = socket.BeginConnect(address, port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(5000)) throw new IOException("Tiempo de conexión agotado.");
                    socket.EndConnect(result);
                    if (!running) { socket.Close(); return; }
                    Add(socket, 0);
                } catch (Exception ex) { socket.Close(); events.Enqueue(new Event { kind = "error", json = ex.Message }); }
            }) { IsBackground = true }.Start();
        }
        void AcceptLoop()
        {
            while (running) {
                try {
                    var socket = listener.AcceptTcpClient();
                    if (peers.Count >= Rules.MaxPlayers - 1) { socket.Close(); continue; }
                    Add(socket, Interlocked.Increment(ref sequence));
                } catch (Exception ex) { if (running) events.Enqueue(new Event { kind = "error", json = ex.Message }); }
            }
        }
        void Add(TcpClient socket, int id)
        {
            socket.NoDelay = true;
            socket.SendTimeout = 3000;
            var peer = new Connection { id = id, socket = socket };
            peers[id] = peer;
            events.Enqueue(new Event { peer = id, kind = "connected" });
            new Thread(() => ReadLoop(peer)) { IsBackground = true }.Start();
            new Thread(() => WriteLoop(peer)) { IsBackground = true }.Start();
        }
        void ReadLoop(Connection peer)
        {
            try {
                using (var reader = new StreamReader(peer.socket.GetStream(), Encoding.UTF8)) {
                    // Bounded framing prevents a malformed LAN peer from allocating an unbounded line.
                    var line = new StringBuilder();
                    while (running && peer.live) {
                        int c = reader.Read();
                        if (c < 0) break;
                        if (c == '\n') {
                            if (events.Count > 2048) throw new IOException("Demasiados mensajes.");
                            events.Enqueue(new Event { peer = peer.id, kind = "data", json = line.ToString() });
                            line.Length = 0;
                        } else if (c != '\r') {
                            line.Append((char)c);
                            if (line.Length > 32768) throw new IOException("Paquete demasiado grande.");
                        }
                    }
                }
            } catch { }
            Close(peer);
        }
        void WriteLoop(Connection peer)
        {
            try {
                using (var writer = new StreamWriter(peer.socket.GetStream(), new UTF8Encoding(false))) {
                    writer.AutoFlush = true;
                    while (running && peer.live) {
                        string json;
                        while (peer.outgoing.TryDequeue(out json)) writer.WriteLine(json);
                        peer.signal.WaitOne(100);
                    }
                }
            } catch { }
            Close(peer);
        }
        void Close(Connection peer)
        {
            if (!peers.TryRemove(peer.id, out var removed)) return;
            peer.live = false;
            peer.socket.Close();
            peer.signal.Set();
            events.Enqueue(new Event { kind = "disconnected", peer = peer.id });
        }
        public void Send(int peerId, string json)
        {
            if (!peers.TryGetValue(peerId, out var peer)) return;
            if (peer.outgoing.Count > 120) { Close(peer); return; }
            peer.outgoing.Enqueue(json);
            peer.signal.Set();
        }
        public void Broadcast(string json) { foreach (var peer in peers.Keys) Send(peer, json); }
        public void Dispose()
        {
            running = false;
            if (listener != null) listener.Stop();
            foreach (var peer in peers.Values) Close(peer);
        }
        public static string LocalAddresses()
        {
            var list = new List<string>();
            try { foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName())) if (ip.AddressFamily == AddressFamily.InterNetwork) list.Add(ip.ToString()); }
            catch { }
            return list.Count == 0 ? "127.0.0.1" : string.Join(" / ", list.ToArray());
        }
    }
}

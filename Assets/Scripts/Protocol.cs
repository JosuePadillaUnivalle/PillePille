using System;
using UnityEngine;

namespace PillePille
{
    public enum MatchMode { Classic, Infection, Relay }
    public enum MatchPhase { Lobby, Playing, Results }

    [Serializable] public class InputFrame
    {
        public float x, z;
        public bool jump, dash, interact;
    }

    [Serializable] public class PlayerState
    {
        public int id;
        public string name;
        public Vector3 position;
        public float yaw, taggedSeconds, safeSeconds, dashCooldown;
        public bool tagged, alive, bot, grounded;
        public bool trapped;
        public float trapSeconds, rescueProgress, speedSeconds;
        public int rescueTargetId = -1, rescuesUsed;
    }

    [Serializable] public class PadState
    {
        public int id;
        public float cooldown;
    }

    [Serializable] public class Packet
    {
        public string type, name, message;
        public int id, mode, phase, round, version;
        public float timeLeft, fuse;
        public InputFrame input;
        public PlayerState[] players;
        public PadState[] pads;
    }

    public static class Rules
    {
        public const float RunnerSpeed = 6f;
        public const float ChaserSpeed = 6.9f;
        public const float Immunity = 3f;
        public const float DashSpeed = 15f;
        public const float DashDuration = .18f;
        public const float DashRecharge = 2.5f;
        public const float JumpSpeed = 8f;
        public const float Gravity = 22f;
        public const float RoundSeconds = 120f;
        public const float FuseSeconds = 20f;
        public const float SpeedDuration = 10f;
        public const float SpeedMultiplier = 1.5f;
        public const float PadRecharge = 20f;
        public const float TrapDuration = 10f;
        public const float RescueDuration = 1f;
        public const float RescueRange = 2.2f;
        public const int ProtocolVersion = 3;
        public const int Port = 7777;
        public const int MaxPlayers = 8;
    }
}

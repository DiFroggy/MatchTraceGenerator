using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchTraceGenerator
{
    public class PlayerRoundTrace
    {
        // Basic identification attributes
        public string Map { get; set; }
        public string Team { get; set; }
        public byte InternalTeamId { get; set; }
        public int MatchId { get; set; }
        public int RoundId { get; set; }
        public long SteamId { get; set; }
        public bool AbnormalMatch { get; set; }
        public float TimeAlive { get; set; }
        // Round flags
        public bool RoundWinner { get; set; }
        public bool FirstDeath { get; set; }
        public bool FirstKill { get; set; }
        public bool BombCarrier { get; set; }

        // Cumulative round data
        public byte Kills { get; set; }
        public byte FlankKills { get; set; }
        public byte Assists { get; set; }
        public byte Headshots { get; set; }
        public int LGrenadesThrown { get; set; }
        public int NLGrenadesThrown { get; set; }
        public double AvgKillDistance { get; set; }

        // Revamped held variables
        public List<double> HeldElement { get; set; }
    }
}

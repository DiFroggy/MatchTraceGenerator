using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchTraceGenerator
{
    public class PlayerMatchTrace
    {
        // Basic identification attributes
        public string Map { get; set; }
        public string Team { get; set; }
        public byte InternalTeamId { get; set; }
        public int MatchId { get; set; }
        public long SteamId { get; set; }
        public bool AbnormalMatch { get; set; }
        public int RoundsPlayed { get; set; }

        // Result
        public bool MatchWinner { get; set; }

        // Cumulative match data
        public byte Kills { get; set; }
        public byte FlankKills { get; set; }
        public byte Assists { get; set; }
        public byte Headshots { get; set; }
        public byte FirstDeaths { get; set; }
        public byte FirstKills { get; set; }
        public byte LGrenadesThrown { get; set; }
        public byte NLGrenadesThrown { get; set; }
        public byte Clutches { get; set; }
        public byte TimesLastAlive { get; set; }

        // Avg values per round
        public double AvgKills { get; set; }
        public double AvgFlankKills { get; set; }
        public double AvgAssists { get; set; }
        public double AvgLGrenadesThrown { get; set; }
        public double AvgNLGrenadesThrown { get; set; }
        public double AvgHeadshots { get; set; }
        public double AvgTimeAlive { get; set; }
        public double AvgKillDistance { get; set; }
        public double AvgCentroidDistance { get; set; }
        public double AvgSiteDistance { get; set; }

        // Revamped held variables
        public List<double> HeldElement { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DemoInfo;

namespace MatchTraceGenerator
{
    class PlayerSnapshot
    {
        // Identification info
        public int MatchId { get; set; }
        public int RoundId { get; set; }
        public int TickId { get; set; }
        public long SteamId { get; set; }
        public string Map { get; set; }
        public string Team { get; set; }
        public byte InternalTeamId { get; set; }

        // Movement info
        // Final position
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        // Angle of movement from the first position
        public double MovAngle { get; set; }
        // Average velocity
        public double MovVelocity { get; set; }
        //double ViewAngle { get; set; }
    }
}

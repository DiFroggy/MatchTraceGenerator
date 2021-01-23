using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchTraceGenerator
{
    class PlayerStat
    {
        public long SteamId { get; set; }
        public byte InternalTeam { get; set; }
        public int CumulativeRounds { get; set; } = 0;
        public string LastCheckedTeam { get; set; }
        // Round data. Must be refreshed after every round
        public List<Vector> Positions { get; set; } = new List<Vector>();

        public List<double> CentroidDistances { get; set; } = new List<double>();
        public int TickSum { get; set; } = 0;
        public int RoundLethalGrenades = 0;
        public int RoundNonLethalGrenades = 0;
        public byte RoundKills { get; set; } = 0;
        public byte RoundFlankKills { get; set; } = 0;
        public byte RoundAssists { get; set; } = 0;
        public byte RoundHeadshots { get; set; } = 0;
        public List<double> SiteDistances { get; set; } = new List<double>();
        public List<double> KillDistance { get; set; } = new List<double>();
        public int FirstKillTick { get; set; } = 0;
        // Cumulative match data
        public byte Kills { get; set; } = 0;
        public byte FlankKills { get; set; } = 0;
        public byte Assists { get; set; } = 0;
        public byte Headshots { get; set; } = 0;
        public byte FirstDeaths { get; set; } = 0;
        public byte FirstKills { get; set; } = 0;
        public byte GrenadesThrown { get; set; } = 0;
        public byte LGrenadesThrown { get; set; } = 0;
        public byte NLGrenadesThrown { get; set; } = 0;
        public byte Clutches { get; set; } = 0;
        public byte TimesLastAlive { get; set; } = 0;
        // Match historical data (averaged before commiting)
        public List<int> MatchKills { get; set; } = new List<int>();
        public List<int> MatchFlankKills { get; set; } = new List<int>();
        public List<int> MatchAssists { get; set; } = new List<int>();
        public List<int> MatchLGrenadesThrown { get; set; } = new List<int>();
        public List<int> MatchNLGrenadesThrown { get; set; } = new List<int>();
        public List<int> MatchHeadshots { get; set; } = new List<int>();
        public List<int> MatchTickSums { get; set; } = new List<int>();
        public List<double> MatchKillDistance { get; set; } = new List<double>();
        public List<double> MatchCentroidDistance { get; set; } = new List<double>() { };
        public List<double> MatchSiteDistance { get; set; } = new List<double>();
        public Dictionary<int, double> MatchPrimaryFreq = new Dictionary<int, double>(){
            { 0 , 0 }, { 1 , 0 }, { 2 , 0 }, { 3 , 0 }, { 4 , 0 }
        };

        // Reference to the player entity from the parser
        public Player PlayerEntity { get; set; }
        public Dictionary<EquipmentElement, int> ElementMatchFreq = new Dictionary<EquipmentElement, int>();
        public int TotalMatchFreq = 0;
        public PlayerStat(Player PReference)
        {
            PlayerEntity = PReference;
            SteamId = PReference.SteamID;
            foreach (EquipmentElement item in Enum.GetValues(typeof(EquipmentElement)))
            {
                ElementMatchFreq.Add(item, 0);
            }
        }
        public void RecordElement(Equipment Element)
        {
            TotalMatchFreq += 1;
            ElementMatchFreq[Element.Weapon] = ElementMatchFreq[Element.Weapon] + 1;
        }

        public void ResetRoundData()
        {
            // Add to lists the corresponding round values
            MatchTickSums.Add(TickSum);
            MatchLGrenadesThrown.Add(RoundLethalGrenades);
            MatchNLGrenadesThrown.Add(RoundNonLethalGrenades);
            MatchKills.Add(RoundKills);
            MatchAssists.Add(RoundAssists);
            MatchHeadshots.Add(RoundHeadshots);
            MatchFlankKills.Add(RoundFlankKills);

            // TODO: Revisar cada variable una a una
            // Agregar variables no marcadas en el word
            // Comprobar si las listas están siendo bien pobladas

            // Then reset their corresponding values
            Positions = new List<Vector>();
            CentroidDistances = new List<double>();
            TickSum = 0;
            RoundLethalGrenades = 0;
            RoundNonLethalGrenades = 0;
            RoundKills = 0;
            RoundAssists = 0;
            RoundHeadshots = 0;
            KillDistance = new List<double>();
            RoundFlankKills = 0;
        }
        public void ResetMatchData()
        {
            MatchTickSums = new List<int>();
            MatchLGrenadesThrown = new List<int>();
            MatchNLGrenadesThrown = new List<int>();
            MatchKills = new List<int>();
            MatchAssists = new List<int>();
            MatchHeadshots = new List<int>();
            MatchFlankKills = new List<int>();
            MatchSiteDistance = new List<double>();
            MatchKillDistance = new List<double>();
        }
    }
}

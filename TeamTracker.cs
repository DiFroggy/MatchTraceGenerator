using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatchTraceGenerator
{
    class TeamTracker
    {
        // Match identification data
        public int RoundId { get; set; } = 0;

        //Results of the round
        byte RoundWinner;

        string Map { get; set; }
        float TickRate { get; set; }

        // Internal state control
        public bool FirstKill = false;
        public long FirstKiller = -1;
        public long FirstDeath = -1;
        public bool ProperlySetTeams { get; set; } = false;
        int LastRoundSwitchCheck = -1;
        int RoundTickSum = 0;

        // Stuff to track
        // By default, Team1 starts as Terrorists
        (HashSet<long>, HashSet<long>) TeamMembers = (new HashSet<long>(), new HashSet<long>());
        public Dictionary<long, PlayerStat> AllPlayers { get; set; } = new Dictionary<long, PlayerStat>();
        HashSet<long> DeadPlayers = new HashSet<long>();
        bool SwitchSides = false;

        List<PlayerMatchTrace> ReturnTraces = new List<PlayerMatchTrace>();
        // Manually keep track of scores
        (int, int) Scores = (0, 0);

        // Clutch checker
        bool PossibleClutch = false;
        long LastAlive;

        // RoundTraces
        public List<PlayerRoundTrace> RoundTraces = new List<PlayerRoundTrace>();
        // PositionTraces
        public List<PlayerSnapshot> Snapshots = new List<PlayerSnapshot>();

        // Hardcoded plant-sites
        public Dictionary<string, (Vector A, Vector B)> PlantSites = new Dictionary<string, (Vector, Vector)>()
        {
            { "de_dust2" , (new Vector { X = 1192.37f, Y = 2492.22f , Z = 96.09f } , new Vector { X = -1450.95f, Y = 2573.37f , Z = 6.03f }) },
            { "de_inferno" , (new Vector { X = 1942.44f, Y = 325.24f , Z = 161.03f } , new Vector { X = 472.31f, Y = 2815.43f , Z = 165.37f }) },
            { "de_mirage" , (new Vector { X = -394.6f, Y = -2110.5f , Z = -176.36f } , new Vector { X = -1979.14f, Y = 318.2f , Z = -159.97f }) },
            { "de_nuke" , (new Vector { X = 703.44f, Y = -731.23f , Z = -415.97f } , new Vector { X = 576.97f, Y = -1041.43f , Z = -767.97f }) }
        };
        public TeamTracker(DemoParser parser)
        {
            this.Map = parser.Map;
            this.TickRate = parser.TickRate;
            CorrectTeams(parser.PlayingParticipants);
        }
        /// <summary>
        /// Resets team members and stats, then checks if teams are evenly composed.
        /// </summary>
        /// <param name="PlayingParticipants">List of not-spectating players given by the parser.</param>
        /// <returns>Result of the operation.</returns>
        public bool CorrectTeams(IEnumerable<Player> PlayingParticipants)
        {
            // No need to correct if it's already properly set
            if (ProperlySetTeams) return true;

            // Refresh teams
            AllPlayers = new Dictionary<long, PlayerStat>();

            foreach (var player in PlayingParticipants)
            {
                PlayerStat ThisPlayer = new PlayerStat(player);
                switch (player.Team)
                {
                    case Team.Terrorist:
                        TeamMembers.Item1.Add(player.SteamID);
                        ThisPlayer.InternalTeam = 0;
                        break;
                    case Team.CounterTerrorist:
                        TeamMembers.Item2.Add(player.SteamID);
                        ThisPlayer.InternalTeam = 1;
                        break;
                }
                AllPlayers[player.SteamID] = ThisPlayer;
            }
            if (TeamMembers.Item1.Count == TeamMembers.Item2.Count && TeamMembers.Item1.Count == 5) ProperlySetTeams = true;
            return (ProperlySetTeams);
        }
        /// <summary>
        /// Checks if teams have switched places
        /// </summary>
        /// <param name="PlayingParticipants"></param>
        public void CheckSwitch(IEnumerable<Player> PlayingParticipants)
        {
            // Minimum amount of players that need to be found before switching the flag
            int MinimumPlayers = 4, SwappedPlayers = 0;
            if (!ProperlySetTeams || LastRoundSwitchCheck == RoundId) return;

            foreach (var player in PlayingParticipants)
            {
                HashSet<long> Terrorists, CTerrorists;
                Terrorists = SwitchSides ? ref TeamMembers.Item1 : ref TeamMembers.Item2;
                CTerrorists = !SwitchSides ? ref TeamMembers.Item1 : ref TeamMembers.Item2;

                switch (player.Team)
                {
                    case Team.Terrorist:
                        SwappedPlayers += CTerrorists.Contains(player.SteamID) ? 0 : 1;
                        break;
                    case Team.CounterTerrorist:
                        SwappedPlayers += Terrorists.Contains(player.SteamID) ? 0 : 1;
                        break;
                }
                
            }
            if (SwappedPlayers >= MinimumPlayers)
            {
                SwitchSides = !SwitchSides;
                ResumeMatch();
                foreach (var player in PlayingParticipants)
                {
                    AllPlayers[player.SteamID].ResetMatchData();
                }
            }
            foreach (var player in PlayingParticipants)
            {
                AllPlayers[player.SteamID].LastCheckedTeam = player.Team.ToString();
            }
            LastRoundSwitchCheck = RoundId;
        }
        /// <summary>
        /// Compares scores with the parser's, hence the match can be flagged as properly parsed.
        /// </summary>
        /// <param name="TScore"></param>
        /// <param name="CTScore"></param>
        /// <returns></returns>
        public bool CompareScores(int TScore, int CTScore)
        {
            return SwitchSides ? (CTScore, TScore) == Scores : (TScore, CTScore) == Scores;
        }
        /// <summary>
        /// Returns the id of the winning team
        /// </summary>
        /// <param name="TScore">Terrorist Score</param>
        /// <param name="CTScore">Counter Terrorist Score</param>
        /// <returns></returns>
        public int CheckWinner(int TScore, int CTScore)
        {
            if (TScore == CTScore) return 3;

            if (!SwitchSides)
            {
                return TScore > CTScore ? 1 : 2;
            }
            else
            {
                return TScore > CTScore ? 2 : 1;
            }
        }
        /// <summary>
        /// Adds a point to the corresponding team
        /// </summary>
        /// <param name="WinnerTeam">Winning team, as pointed out by the parser.</param>
        public void AddScore(Team WinnerTeam)
        {
            switch (WinnerTeam)
            {
                case Team.Terrorist:
                    if (!SwitchSides)
                    {
                        Scores.Item1++;
                        RoundWinner = 1;
                    }
                    else
                    {
                        Scores.Item2++;
                        RoundWinner = 2;
                    }
                    break;
                case Team.CounterTerrorist:
                    if (SwitchSides)
                    {
                        Scores.Item1++;
                        RoundWinner = 1;
                    }
                    else
                    {
                        Scores.Item2++;
                        RoundWinner = 2;
                    }
                    break;
            }
        }
        /// <summary>
        /// Updates values on tracked players. This includes: Reference to the player entity, position, primary weapon usage and count of recorded ticks.
        /// </summary>
        /// <param name="PlayingParticipants"></param>
        public void UpdatePlayerStats(IEnumerable<Player> PlayingParticipants, double Delta)
        {
            if (!ProperlySetTeams) return;
            RoundTickSum++;
            foreach (var player in PlayingParticipants)
            {

                // Prevent updates on dead players
                if (!player.IsAlive) continue;

                // Update reference
                AllPlayers[player.SteamID].PlayerEntity = player;

                // When round ends, teams are switched immediately
                // Therefore we store the team before so it can be assigned properly during trace generation
                if (AllPlayers[player.SteamID].LastCheckedTeam == null) AllPlayers[player.SteamID].LastCheckedTeam = player.Team.ToString();

                // Ticksum aka time played
                AllPlayers[player.SteamID].TickSum++;

                // Record min distance to plant sites
                double SiteADist = (player.Position - PlantSites[Map].A).AbsoluteSquared;
                double SiteBDist = (player.Position - PlantSites[Map].B).AbsoluteSquared;
                AllPlayers[player.SteamID].SiteDistances.Add(SiteADist < SiteBDist ? SiteADist : SiteBDist);

                // Check active weapon
                if (player.ActiveWeapon != null) AllPlayers[player.SteamID].RecordElement(player.ActiveWeapon);
            }
        }
        /// <summary>
        /// Auxiliar function, purposed to iterate over teams and be called every tick during parsing
        /// </summary>
        public void CalculatePlayerStats(double ElapsedTime)
        {
            // Centroid calculation happens after 10 seconds of round time to give players enough time to position themselves
            if (!ProperlySetTeams || ElapsedTime < 10) return;

            for (int i = 0; i < 2; i++)
            {
                HashSet<long> CurrentTeam = i == 0 ? ref TeamMembers.Item1 : ref TeamMembers.Item2;
                CalculateTickCentroids(CurrentTeam);
            }
        }
        public void RecordPositions()
        {
            foreach (var Player in AllPlayers.Values)
            {
                Player.PositionTrace.SetPosition(Player.PlayerEntity);
            }
        }
        public void GetSnapshots()
        {
            List<PlayerSnapshot> ReturnSnapshots = new List<PlayerSnapshot>();
            foreach (var Player in AllPlayers.Values)
            {
                PlayerSnapshot Snapshot = Player.PositionTrace.GetSnapshot(Player.PlayerEntity, Player.InternalTeam);
                Snapshot.Map = Map;
                Snapshot.RoundId = RoundId+1;
                ReturnSnapshots.Add(Snapshot);
            }
            Snapshots.AddRange(ReturnSnapshots);
        }
        /// <summary>
        /// Calculates the centroid of the team so it can be tracked. Called during parsing.
        /// </summary>
        public void CalculateTickCentroids(HashSet<long> CurrentTeam)
        {
            // Calculate centroid for the team
            Vector TeamCentroid = new Vector(0, 0, 0);

            // If only 1 player is alive, there is no centroid to account for
            int CentroidMembers = 0;
            foreach (var PlayerKey in CurrentTeam)
            {
                if (DeadPlayers.Contains(PlayerKey)) continue;

                CentroidMembers += 1;
                PlayerStat Player = AllPlayers[PlayerKey];
                TeamCentroid += Player.PlayerEntity.Position;
            }
            if (CentroidMembers <= 1) return;

            TeamCentroid.X /= CentroidMembers;
            TeamCentroid.Y /= CentroidMembers;

            // Then calculate the distance of the player to the centroid
            foreach (var PlayerKey in CurrentTeam)
            {
                if (DeadPlayers.Contains(PlayerKey)) continue;

                PlayerStat Player = AllPlayers[PlayerKey];
                Vector TempVector = TeamCentroid - Player.PlayerEntity.Position;
                Player.CentroidDistances.Add(TempVector.AbsoluteSquared);
            }
        }
        public (double, double) CalculateTravelledDistance(List<Vector> Positions)
        {
            // Sum of all absolute squares for walked distances
            double TravelledDistance = 0f;

            List<double> Velocities = new List<double>();
            int VelocityElements = 0;

            // Logic control variables
            Vector PreviousPosition = Positions[0];
            bool FirstPosition = true;

            foreach (var Position in Positions)
            {

                if (FirstPosition)
                {
                    FirstPosition = false;
                    continue;
                }
                VelocityElements++;

                if (Position.Equals(PreviousPosition)) continue;

                double CurrentDistance = (Position - PreviousPosition).AbsoluteSquared;
                TravelledDistance += CurrentDistance;
                Velocities.Add(CurrentDistance);
                PreviousPosition = Position;
            }
            double AvgVelocity = Velocities.Sum() / VelocityElements;

            return (TravelledDistance, AvgVelocity);
        }
        public double CalculateAvgCentroidDistance(List<double> CentroidDistances)
        {
            double CentroidDistance = 0;
            foreach (var Distance in CentroidDistances)
            {
                CentroidDistance += Distance;
            }
            CentroidDistance /= CentroidDistances.Count;
            return CentroidDistance;
        }
        public void GrenadeThrownEvent(long PlayerId, Equipment Grenade)
        {
            if (!ProperlySetTeams) return;
            AllPlayers[PlayerId].GrenadesThrown += 1;
            switch (Grenade.Weapon)
            {
                case EquipmentElement.Decoy:
                case EquipmentElement.Flash:
                case EquipmentElement.Smoke:
                    AllPlayers[PlayerId].RoundNonLethalGrenades += 1;
                    AllPlayers[PlayerId].NLGrenadesThrown += 1;
                    break;
                case EquipmentElement.Incendiary:
                case EquipmentElement.Molotov:
                case EquipmentElement.HE:
                    AllPlayers[PlayerId].RoundLethalGrenades += 1;
                    AllPlayers[PlayerId].LGrenadesThrown += 1;
                    break;
                default:
                    break;
            }
        }
        public void KillEvent(PlayerKilledEventArgs e)
        {
            Player Killer = e.Killer;
            Player Victim = e.Victim;
            Player Assister = e.Assister;

            if (Victim == null) return;
            if (DeadPlayers.Add(Victim.SteamID))
            {
                if (!PossibleClutch)
                {
                    int Remaining1 = 5, Remaining2 = 5;
                    long Last1 = 0, Last2 = 0;
                    // Check if this is a clutch situation (1xN)
                    foreach (var Player in TeamMembers.Item1)
                    {
                        if (DeadPlayers.Contains(Player)) Remaining1 += -1;
                        if (Remaining1 == 1) Last1 = Player;
                    }

                    foreach (var Player in TeamMembers.Item2)
                    {
                        if (DeadPlayers.Contains(Player)) Remaining2 += -1;
                        if (Remaining2 == 1) Last2 = Player;
                    }
                    if (Remaining1 == 1 && Remaining2 > 2)
                    {
                        PossibleClutch = true;
                        LastAlive = Last1;
                    }
                    if (Remaining2 == 1 && Remaining1 > 2)
                    {
                        PossibleClutch = true;
                        LastAlive = Last2;
                    }
                }



                if (Killer != null)
                {
                    // Don't count teamkills
                    if (TeamMembers.Item1.Contains(Killer.SteamID) && TeamMembers.Item1.Contains(Victim.SteamID) ||
                        TeamMembers.Item2.Contains(Killer.SteamID) && TeamMembers.Item2.Contains(Victim.SteamID)) return;
                    if (!FirstKill)
                    {
                        FirstKiller = Killer.SteamID;
                        FirstDeath = Victim.SteamID;
                        FirstKill = true;

                        AllPlayers[Killer.SteamID].FirstKills += 1;
                        AllPlayers[Victim.SteamID].FirstDeaths += 1;
                    }
                    if (AllPlayers[Killer.SteamID].RoundKills == 0)
                    {
                        AllPlayers[Killer.SteamID].FirstKillTick = AllPlayers[Killer.SteamID].TickSum;
                    }

                    // Add to killer's stats
                    AllPlayers[Killer.SteamID].RoundKills += 1;
                    AllPlayers[Killer.SteamID].Kills += 1;

                    double KillDistance = (Killer.LastAlivePosition - Victim.LastAlivePosition).AbsoluteSquared;
                    AllPlayers[Killer.SteamID].KillDistance.Add(KillDistance);
                    AllPlayers[Killer.SteamID].MatchKillDistance.Add(KillDistance);
                    if (e.Headshot)
                    {
                        AllPlayers[Killer.SteamID].RoundHeadshots += 1;
                        AllPlayers[Killer.SteamID].Headshots += 1;
                    }


                    // Check for flanking
                    if (e.Weapon.Class != EquipmentClass.Grenade || e.Weapon.Class != EquipmentClass.Unknown || e.Weapon.Class != EquipmentClass.Equipment)
                    {
                        double DeathAngle = (Killer.Position - Victim.Position).Angle2D * 180 / Math.PI;
                        DeathAngle = DeathAngle < 0 ? 360 + DeathAngle : DeathAngle;
                        float ViewDirection = Victim.ViewDirectionX;
                        bool OutOfView = false;
                        if (ViewDirection >= 45 && ViewDirection <= 315)
                        {
                            if (DeathAngle > ViewDirection + 45 || DeathAngle < ViewDirection - 45)
                            {
                                OutOfView = true;
                            }
                        }
                        else if (ViewDirection < 45)
                        {
                            if (DeathAngle > ViewDirection + 45 && DeathAngle < 360 - (45 - ViewDirection))
                            {
                                OutOfView = true;
                            }
                        }
                        else if (ViewDirection > 315)
                        {
                            if (DeathAngle > 45 + ViewDirection - 360 && DeathAngle < ViewDirection - 45)
                            {
                                OutOfView = true;
                            }
                        }
                        if (OutOfView)
                        {
                            AllPlayers[Killer.SteamID].FlankKills += 1;
                            AllPlayers[Killer.SteamID].RoundFlankKills += 1;
                        }
                    }


                    // If it was assisted, record assist
                    if (Assister != null)
                    {
                        AllPlayers[Assister.SteamID].RoundAssists += 1;
                        AllPlayers[Assister.SteamID].Assists += 1;
                    }

                }
            }
        }

        /// <summary>
        /// Calculates round data once the round is over and returns a list of PlayerTraces.
        /// </summary>
        public void ResumeRound()
        {
            byte teamId = 1;
            RoundId++;

            for (int i = 0; i < 2; i++)
            {
                HashSet<long> CurrentTeam = teamId == 1 ? ref TeamMembers.Item1 : ref TeamMembers.Item2;

                int TotalEquipmentValue = 0;
                foreach (var PlayerKey in CurrentTeam)
                {
                    TotalEquipmentValue+= AllPlayers[PlayerKey].PlayerEntity.FreezetimeEndEquipmentValue;
                }

                // Iterate over all players of this team
                foreach (var PlayerKey in CurrentTeam)
                {
                    PlayerStat Player = AllPlayers[PlayerKey];
                    Player.CumulativeRounds++;

                    // Round winner is the team whose score increased in AddScore() function
                    bool RoundResult = teamId == RoundWinner;

                    // Was it a clutch?
                    if (PossibleClutch)
                    {
                        if (LastAlive == PlayerKey)
                        {
                            Player.TimesLastAlive += 1;
                            PossibleClutch = false;
                            if (RoundResult) Player.Clutches += 1;
                        }
                    }

                    // Sum of all absolute squares for walked distances
                    (double TravelledDistance, double AvgVelocity) TravelInfo = Player.Positions.Count > 0 ? CalculateTravelledDistance(Player.Positions) : (0, 0);
                    double SiteDistance = Player.SiteDistances.Count > 0 ? Player.SiteDistances.Average() : 0;

                    // Average round centroid distance
                    double CentroidDistance = Player.CentroidDistances.Count > 0 ? CalculateAvgCentroidDistance(Player.CentroidDistances) : 0;

                    List<double> HeldElements = new List<double>();
                    foreach (EquipmentElement item in Enum.GetValues(typeof(EquipmentElement)))
                    {
                        HeldElements.Add((double)Player.ElementRoundFreq[item] / (double)Player.TotalRoundFreq);
                    }

                    RoundTraces.Add(new PlayerRoundTrace()
                    {
                        Map = Map,
                        Team = Player.LastCheckedTeam,
                        InternalTeamId = Player.InternalTeam,
                        RoundId = RoundId,
                        SteamId = Player.SteamId,
                      
                        TimeAlive = Player.TickSum/TickRate,
                        TeamStartingEquipmentValue = TotalEquipmentValue,

                        RoundWinner = RoundResult,
                        FirstDeath = FirstDeath == Player.SteamId,
                        FirstKill = FirstKiller == Player.SteamId,
                        // BombCarrier =

                        Kills = Player.RoundKills,
                        Assists = Player.RoundAssists,
                        Headshots = Player.RoundHeadshots,
                        FlankKills = Player.RoundFlankKills,
                        LGrenadesThrown = Player.RoundLethalGrenades,
                        NLGrenadesThrown = Player.RoundNonLethalGrenades,
                        AvgKillDistance = Player.RoundKills !=0 ? Player.KillDistance.Average() : -500,

                        HeldElement = HeldElements,
                    });
                    // Add to match records
                    Player.MatchCentroidDistance.Add(CentroidDistance);
                    Player.MatchSiteDistance.Add(SiteDistance);

                    Player.ResetRoundData();
                }

                teamId++;
            }

            // Reset TeamTracker's variables
            FirstKill = false;
            FirstKiller = -1;
            FirstDeath = -1;
            DeadPlayers = new HashSet<long>();
            RoundTickSum = 0;
        }
        public void ResumeMatch()
        {
            byte teamId = 1;
            for (int i = 0; i < 2; i++)
            {

                HashSet<long> CurrentTeam = teamId == 1 ? ref TeamMembers.Item1 : ref TeamMembers.Item2;
                // Iterate over all players of this team
                foreach (var PlayerKey in CurrentTeam)
                {
                    PlayerStat Player = AllPlayers[PlayerKey];


                    List<double> HeldElements = new List<double>();
                    foreach (EquipmentElement item in Enum.GetValues(typeof(EquipmentElement)))
                    {
                        HeldElements.Add((double)Player.ElementMatchFreq[item] / (double)Player.TotalMatchFreq);
                    }
                    ReturnTraces.Add(new PlayerMatchTrace()
                    {
                        Map = Map,
                        // AbnormalMatch = AbnormalMatch,
                        Team = Player.LastCheckedTeam,
                        InternalTeamId = Player.InternalTeam,
                        SteamId = Player.SteamId,
                        RoundsPlayed = Player.CumulativeRounds,

                        // MatchWinner = Player.InternalTeam == MatchWinner,

                        Kills = Player.Kills,
                        FlankKills = Player.FlankKills,
                        Assists = Player.Assists,
                        Headshots = Player.Headshots,
                        FirstDeaths = Player.FirstDeaths,
                        FirstKills = Player.FirstKills,
                        LGrenadesThrown = Player.LGrenadesThrown,
                        NLGrenadesThrown = Player.NLGrenadesThrown,
                        Clutches = Player.Clutches,
                        TimesLastAlive = Player.TimesLastAlive,

                        AvgKills = Player.MatchKills.Count != 0 ? Player.MatchKills.Average() : 0,
                        AvgFlankKills = Player.MatchFlankKills.Count != 0 ? Player.MatchFlankKills.Average() : 0,
                        AvgAssists = Player.MatchAssists.Count != 0 ? Player.MatchAssists.Average() : 0,
                        AvgLGrenadesThrown = Player.MatchLGrenadesThrown.Count != 0 ? Player.MatchLGrenadesThrown.Average() : 0,
                        AvgNLGrenadesThrown = Player.MatchNLGrenadesThrown.Count != 0 ? Player.MatchNLGrenadesThrown.Average() : 0,
                        AvgHeadshots = Player.MatchHeadshots.Count != 0 ? Player.MatchHeadshots.Average() : 0,
                        AvgTimeAlive = Player.MatchTickSums.Count != 0 ? Player.MatchTickSums.Average() / TickRate : 0,
                        AvgCentroidDistance = Player.MatchCentroidDistance.Count != 0 ? Player.MatchCentroidDistance.Average() : 0,
                        AvgKillDistance = Player.MatchKillDistance.Count != 0 ? Player.MatchKillDistance.Average() : 0,
                        AvgSiteDistance = Player.MatchSiteDistance.Count != 0 ? Player.MatchSiteDistance.Average() : 0,

                        // List of use. Should add up to 1
                        HeldElement = HeldElements

                    });
                    teamId++;
                }
            }
        }
        public (List<PlayerRoundTrace>,List<PlayerMatchTrace>) CompleteMatch()
        {
            return (RoundTraces,ReturnTraces);
        }
    }
}

using CsvHelper;
using DemoInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatchTraceGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Files info
            string dir = @"D:\Titulo\data\csgo\csgo\";
            DirectoryInfo d = new DirectoryInfo(dir);
            FileInfo[] Files = d.GetFiles("*.dem"); // Getting Text files

            (int SuccessCount, int FailCount) FileCount = (0, 0);
            List<PlayerMatchTrace> MatchOutputTraces = new List<PlayerMatchTrace>();
            List<PlayerRoundTrace> RoundOutputTraces = new List<PlayerRoundTrace>();
            List<PlayerSnapshot> AllSnapshots = new List<PlayerSnapshot>();
            foreach (var file in Files)
            {
                string fileName = dir + file.Name;

                (List<PlayerRoundTrace> FullRoundTraces,
                    List<PlayerMatchTrace> FullMatchTraces, 
                    List<PlayerSnapshot> Snapshots) = ParseFile(fileName);

                if (FullMatchTraces.Count == 0)
                {
                    FileCount.FailCount++;
                    //continue;
                }
                else
                {
                    FileCount.SuccessCount++;
                    
                    foreach(var Trace in FullRoundTraces)
                    {
                        Trace.MatchId = FileCount.SuccessCount;
                    }
                    foreach (var Trace in FullMatchTraces)
                    {
                        Trace.MatchId = FileCount.SuccessCount;
                    }
                    foreach (var Snapshot in Snapshots)
                    {
                        Snapshot.MatchId = FileCount.SuccessCount;
                    }
                    RoundOutputTraces.AddRange(FullRoundTraces);
                    MatchOutputTraces.AddRange(FullMatchTraces);
                    AllSnapshots.AddRange(Snapshots);
                    
                }
                Console.Write("\rSuccess: {0} - Failure: {1}", FileCount.SuccessCount, FileCount.FailCount);

                //if (FileCount.SuccessCount == 5) break;
            }

            using (var writer = new StreamWriter(@"C:\\Users\\jawas\\Documents\\Titulo\\round_output.csv"))
            using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.CurrentCulture))
            {
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.RegisterClassMap<PlayerRoundTraceMap>();
                csv.WriteRecords(RoundOutputTraces);
                writer.Flush();
            }
            using (var writer = new StreamWriter(@"C:\\Users\\jawas\\Documents\\Titulo\\match_output.csv"))
            using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.CurrentCulture))
            {
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.RegisterClassMap<PlayerMatchTraceMap>();
                csv.WriteRecords(MatchOutputTraces);
                writer.Flush();
            }

            using (var writer = new StreamWriter(@"C:\\Users\\jawas\\Documents\\Titulo\\snapshot_output.csv"))
            using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.CurrentCulture))
            {
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.AutoMap<PlayerSnapshot>();
                csv.WriteRecords(AllSnapshots);
                writer.Flush();
            }

            Console.WriteLine("\nTotal files parsed: {0}", FileCount.SuccessCount + FileCount.FailCount);
            Console.WriteLine("Finished writing!");
            Console.ReadKey();
        }
        static (List<PlayerRoundTrace>,List<PlayerMatchTrace>,List<PlayerSnapshot>) ParseFile(string fileName)
        {
            int PreviousTick = -1;

            List<PlayerRoundTrace> FullRoundTraces = new List<PlayerRoundTrace>();
            List<PlayerMatchTrace> FullMatchTraces = new List<PlayerMatchTrace>();

            // Consider only behaviour of active rounds and matches
            bool MatchStarted = false, RoundStarted = false;

            // Exclude matches divided in multiple demos
            bool CompleteMatch = false;

            // Current round info
            double ElapsedTime = 0;
            double ElapsedTicks = 0;

            // Tick position retrieval span
            double TimeDelta = 1.0f;


            using (var fileStream = File.OpenRead(fileName))
            {
                using (var parser = new DemoParser(fileStream))
                {
                    parser.ParseHeader();
                    string map = parser.Map;
                    HashSet<string> validMaps = new HashSet<string>() { "de_inferno", "de_dust2", "de_mirage", "de_nuke" };

                    if (!validMaps.Contains(map) || parser.TickRate <= 0)
                        return (FullRoundTraces,FullMatchTraces,new List<PlayerSnapshot>());

                    // The parser doesn't consider teams, but instead players with team tags which makes them hard to track.
                    // Furthermore, scores can only be checked per team as in 'Terrorist' or 'CTerrorist', making it a pain in the ass
                    // since teams are swapped after 15 rounds.
                    // So a team tracker, to track team members and scores of the team
                    TeamTracker TeamTracker = new TeamTracker(parser);
                    int TickDelta = (int)Math.Round(TimeDelta / parser.TickTime,0);
                    //Console.WriteLine($"Retrieving traces every {TickDelta} ticks, amounting to {parser.TickTime*TickDelta} seconds.");

                    parser.MatchStarted += (sender, e) =>
                    {
                        // If it has been called twice, drop the previous records
                        if (MatchStarted)
                        {
                            TeamTracker = new TeamTracker(parser);
                            FullMatchTraces = new List<PlayerMatchTrace>();
                            FullRoundTraces = new List<PlayerRoundTrace>();
                        }
                        MatchStarted = true;
                    };


                    parser.RoundStart += (sender, e) =>
                    {
                        // Check if this is the first round in the current match
                        if (!MatchStarted || RoundStarted || CompleteMatch)
                            return;

                        RoundStarted = true;
                        ElapsedTime = 0;
                        ElapsedTicks = 0;

                        TeamTracker.CheckSwitch(parser.PlayingParticipants);

                    };

                    parser.RoundEnd += (sender, e) =>
                    {

                        if (!MatchStarted || CompleteMatch || !TeamTracker.ProperlySetTeams)
                            return;

                        RoundStarted = false;



                        // Add score to the correct team
                        TeamTracker.AddScore(e.Winner);
                        TeamTracker.ResumeRound();
                    };

                    parser.TickDone += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted) return;
                        ElapsedTime += parser.TickTime;
                        // Check if teams have been set
                        if (!TeamTracker.ProperlySetTeams)
                        {
                            TeamTracker.CorrectTeams(parser.PlayingParticipants);
                            return;
                        }
                        ElapsedTicks += 1;

                        // Update team information in every tick
                        TeamTracker.UpdatePlayerStats(parser.PlayingParticipants, parser.TickTime);

                        TeamTracker.CalculatePlayerStats(ElapsedTime);
                        if (ElapsedTicks % TickDelta != 0)
                        {
                            TeamTracker.RecordPositions();
                        } else
                        {
                            TeamTracker.GetSnapshots();
                        }

                    };
                    parser.NadeReachedTarget += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted || !TeamTracker.ProperlySetTeams) return;
                        if (e.ThrownBy == null) return;

                    };
                    parser.DecoyNadeStarted += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted || !TeamTracker.ProperlySetTeams) return;
                        if (e.ThrownBy == null) return;

                    };
                    parser.SmokeNadeStarted += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted || !TeamTracker.ProperlySetTeams) return;
                        if (e.ThrownBy == null) return;

                    };
                    parser.FireNadeStarted += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted || !TeamTracker.ProperlySetTeams) return;
                        if (e.ThrownBy == null) return;

                    };
                    parser.WeaponFired += (sender, e) =>
                    {
                        if (!RoundStarted || !MatchStarted || !TeamTracker.ProperlySetTeams) return;

                        if (e.Weapon == null || e.Shooter == null) return;


                        switch (e.Weapon.Class)
                        {
                            case EquipmentClass.Grenade:
                                TeamTracker.GrenadeThrownEvent(e.Shooter.SteamID, e.Weapon);
                                break;
                        }

                    };


                    parser.PlayerKilled += (sender, e) =>
                    {
                        if (!TeamTracker.ProperlySetTeams) return;
                        TeamTracker.KillEvent(e);

                        if (e.Victim == null) return;

                        

                        if (e.Killer == null) return;

                        
                    };

                    parser.BombBeginPlant += (sender, e) =>
                    {
                        
                    };
                    //PlayerHurt is not used in all demos, so ignore for now. That's a headache for tomorrow.

                    try
                    {
                        // If it breaks out of the loop, then it succesfully parsed the demo
                        while (true)
                        {

                            // With this we advance through the demo
                            PreviousTick = parser.CurrentTick;

                            // Not sure why didn't count these as abnormal before
                            if (!parser.ParseNextTick())
                            {
                                if (!CompleteMatch)
                                {
                                    foreach (var Trace in FullMatchTraces)
                                    {
                                        Trace.AbnormalMatch = true;
                                        Trace.Map = parser.Map;
                                    }
                                    foreach (var Trace in FullRoundTraces)
                                    {
                                        Trace.AbnormalMatch = true;
                                    }
                                }
                            };

                            // Sometimes it get stuck without proceeding through the ticks,
                            // so we check make sure this is not the case 
                            if (parser.CurrentTick == PreviousTick) return (new List<PlayerRoundTrace>(),new List<PlayerMatchTrace>(),new List<PlayerSnapshot>());

                            // End of match
                            if (parser.TScore == 16 || parser.CTScore == 16 || (parser.TScore, parser.CTScore).Equals((15, 15)))
                            {
                                CompleteMatch = true;
                                TeamTracker.ResumeMatch();
                                // Set winner of the match and refresh matchTraces
                                // TODO: Meter estos en resumematch
                                int WinnerTeam = TeamTracker.CheckWinner(parser.TScore, parser.CTScore);
                                bool AbnormalMatch = TeamTracker.CompareScores(parser.TScore, parser.CTScore);
                                (List<PlayerRoundTrace>, List<PlayerMatchTrace>) Result = TeamTracker.CompleteMatch();
                                FullRoundTraces.AddRange(Result.Item1);
                                FullMatchTraces.AddRange(Result.Item2);
                                foreach (var Trace in FullMatchTraces)
                                {
                                    Trace.AbnormalMatch = AbnormalMatch;
                                    Trace.MatchWinner = Trace.InternalTeamId == WinnerTeam;
                                }
                                foreach (var Trace in FullRoundTraces)
                                {
                                    Trace.AbnormalMatch = AbnormalMatch;
                                }
                            }

                            if (CompleteMatch) break;

                        }
                        return (FullRoundTraces,FullMatchTraces,TeamTracker.Snapshots);
                    }
                    catch (Exception e)
                    {
                        // Sometimes demos are corrupt
                        /*
                        Console.WriteLine(e.ToString());
                        var st = new StackTrace(e, true);
                        var frame = st.GetFrame(0);
                        var line = frame.GetFileLineNumber();
                        Console.WriteLine($"{st} {frame} {line}");
                        Console.ReadKey();
                        */

                        return (new List<PlayerRoundTrace>(),new List<PlayerMatchTrace>(),new List<PlayerSnapshot>());

                    }
                }
            }

        }
    }
}

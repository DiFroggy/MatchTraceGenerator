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
            foreach (var file in Files)
            {
                string fileName = dir + file.Name;

                List<PlayerMatchTrace> FullMatchTraces = ParseFile(fileName);

                if (FullMatchTraces.Count == 0)
                {
                    FileCount.FailCount++;
                    //continue;
                }
                else
                {
                    FileCount.SuccessCount++;
                    
                    foreach (var Trace in FullMatchTraces)
                    {
                        Trace.MatchId = FileCount.SuccessCount;
                    }
                    MatchOutputTraces.AddRange(FullMatchTraces);
                    
                }
                Console.Write("\rSuccess: {0} - Failure: {1}", FileCount.SuccessCount, FileCount.FailCount);

                //if (FileCount.SuccessCount == 2) break;
            }

            using (var writer = new StreamWriter(@"C:\\Users\\jawas\\Documents\\Titulo\\match_output.csv"))
            using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.CurrentCulture))
            {
                csv.Configuration.HasHeaderRecord = true;
                csv.Configuration.RegisterClassMap<PlayerMatchTraceMap>();
                csv.WriteRecords(MatchOutputTraces);
                writer.Flush();
            }

            Console.WriteLine("\nTotal files parsed: {0}", FileCount.SuccessCount + FileCount.FailCount);
            Console.WriteLine("Finished writing!");
            Console.ReadKey();
        }
        static List<PlayerMatchTrace> ParseFile(string fileName)
        {
            int PreviousTick = -1;

            List<PlayerMatchTrace> FullMatchTraces = new List<PlayerMatchTrace>();

            // Consider only behaviour of active rounds and matches
            bool MatchStarted = false, RoundStarted = false;

            // Exclude matches divided in multiple demos
            bool CompleteMatch = false;

            // Current round info
            double ElapsedTime = 0;


            using (var fileStream = File.OpenRead(fileName))
            {
                using (var parser = new DemoParser(fileStream))
                {
                    parser.ParseHeader();
                    string map = parser.Map;
                    HashSet<string> validMaps = new HashSet<string>() { "de_inferno", "de_dust2", "de_mirage", "de_nuke" };

                    if (!validMaps.Contains(map) || parser.TickRate <= 0)
                        return FullMatchTraces;

                    // The parser doesn't consider teams, but instead players with team tags which makes them hard to track.
                    // Furthermore, scores can only be checked per team as in 'Terrorist' or 'CTerrorist', making it a pain in the ass
                    // since teams are swapped after 15 rounds.
                    // So a team tracker, to track team members and scores of the team
                    TeamTracker TeamTracker = new TeamTracker(parser);


                    parser.MatchStarted += (sender, e) =>
                    {
                        // If it has been called twice, drop the previous records
                        if (MatchStarted)
                        {
                            TeamTracker = new TeamTracker(parser);
                            FullMatchTraces = new List<PlayerMatchTrace>();
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

                        // Update team information in every tick
                        TeamTracker.UpdatePlayerStats(parser.PlayingParticipants, parser.TickTime);

                        TeamTracker.CalculatePlayerStats(ElapsedTime);

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
                                }
                            };

                            // Sometimes it get stuck without proceeding through the ticks,
                            // so we check make sure this is not the case 
                            if (parser.CurrentTick == PreviousTick) return new List<PlayerMatchTrace>();

                            // End of match
                            if (parser.TScore == 16 || parser.CTScore == 16 || (parser.TScore, parser.CTScore).Equals((15, 15)))
                            {
                                CompleteMatch = true;
                                TeamTracker.ResumeMatch();
                                // Set winner of the match and refresh matchTraces
                                // TODO: Meter estos en resumematch
                                int WinnerTeam = TeamTracker.CheckWinner(parser.TScore, parser.CTScore);
                                bool AbnormalMatch = TeamTracker.CompareScores(parser.TScore, parser.CTScore);
                                
                                FullMatchTraces.AddRange(TeamTracker.CompleteMatch());
                                foreach (var Trace in FullMatchTraces)
                                {
                                    Trace.AbnormalMatch = AbnormalMatch;
                                    Trace.MatchWinner = Trace.InternalTeamId == WinnerTeam;
                                }
                            }

                            if (CompleteMatch) break;

                        }
                        return FullMatchTraces;
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

                        return new List<PlayerMatchTrace>();

                    }
                }
            }

        }
    }
}

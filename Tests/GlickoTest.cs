using Microsoft.VisualStudio.TestTools.UnitTesting;
using Glicko2;
using System.Diagnostics;
using System.Collections.Generic;
using System;

namespace Tests
{
    [TestClass]
    public class GlickoTest
    {
        [TestMethod]
        public void TestNormal()
        {
            double[] playerSkills = new double[] {1,1,1,1,1,1,1,1,1,3 };
            RatingCalculator calc = new RatingCalculator();
            RatingPeriodResults periodResults = new RatingPeriodResults();

            List<Rating> ratings = new List<Rating>();
            foreach(double playerSkill in playerSkills)
            {
                ratings.Add(new Rating(calc));
            }
            Random rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                int player1 = rnd.Next(0, playerSkills.Length);
                int player2 = player1;
                while (player2 == player1)
                {
                    player2 = rnd.Next(0, playerSkills.Length);
                }
                Rating firstPlayer = ratings[player1];
                Rating secondPlayer = ratings[player2];
                double skill1 = playerSkills[player1];
                double skill2 = playerSkills[player2];
                double skillMultiplier = 1.0 / (skill1 + skill2);
                skill1 *= skillMultiplier;
                skill2 *= skillMultiplier;
                double diceThrow = rnd.NextDouble();
                if (diceThrow < skill1)
                {
                    periodResults.AddResult(firstPlayer,secondPlayer);
                } else
                {
                    periodResults.AddResult(secondPlayer, firstPlayer);
                }
                if((i % 15)==0)
                {
                    calc.UpdateRatings(periodResults);
                }
            }
            calc.UpdateRatings(periodResults);

            Trace.WriteLine($"Normal results: \n");
            double lowSkillAverage = 0;
            double lowSkillAverageDev = 0;
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
                if (i != (playerSkills.Length - 1))
                {
                    lowSkillAverage += ratings[i].GetRating();
                    lowSkillAverageDev += ratings[i].GetRatingDeviation();
                }
            }
            lowSkillAverage /= (double)playerSkills.Length - 1;
            lowSkillAverageDev /= (double)playerSkills.Length - 1;
            Trace.WriteLine($"\nLow skill average: {lowSkillAverage}+-{lowSkillAverageDev}\n");
            Assert.IsTrue(true);
        }
        [TestMethod]
        public void TestWeighted05()
        {
            double[] playerSkills = new double[] {1,1,1,1,1,1,1,1,1,3 };
            RatingCalculator calc = new RatingCalculator();
            RatingPeriodResults periodResults = new RatingPeriodResults();

            List<Rating> ratings = new List<Rating>();
            foreach(double playerSkill in playerSkills)
            {
                ratings.Add(new Rating(calc));
            }
            Random rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                int player1 = rnd.Next(0, playerSkills.Length);
                int player2 = player1;
                while (player2 == player1)
                {
                    player2 = rnd.Next(0, playerSkills.Length);
                }
                Rating firstPlayer = ratings[player1];
                Rating secondPlayer = ratings[player2];
                double skill1 = playerSkills[player1];
                double skill2 = playerSkills[player2];
                double skillMultiplier = 1.0 / (skill1 + skill2);
                skill1 *= skillMultiplier;
                skill2 *= skillMultiplier;
                double diceThrow = rnd.NextDouble();
                if (diceThrow < skill1)
                {
                    periodResults.AddResult(firstPlayer,secondPlayer,0.5);
                } else
                {
                    periodResults.AddResult(secondPlayer, firstPlayer,0.5);
                }
                if((i % 15)==0)
                {
                    calc.UpdateRatings(periodResults);
                }
            }
            calc.UpdateRatings(periodResults);

            Trace.WriteLine($"All weighted 0.5 results: \n");
            double lowSkillAverage = 0;
            double lowSkillAverageDev = 0;
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
                if (i != (playerSkills.Length - 1))
                {
                    lowSkillAverage += ratings[i].GetRating();
                    lowSkillAverageDev += ratings[i].GetRatingDeviation();
                }
            }
            lowSkillAverage /= (double)playerSkills.Length - 1;
            lowSkillAverageDev /= (double)playerSkills.Length - 1;
            Trace.WriteLine($"\nLow skill average: {lowSkillAverage}+-{lowSkillAverageDev}\n");
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestWeighted03ButLowSkillTripled()
        {
            double[] playerSkills = new double[] {1,1,1,1,1,1,1,1,1,3 };
            RatingCalculator calc = new RatingCalculator();
            RatingPeriodResults periodResults = new RatingPeriodResults();

            List<Rating> ratings = new List<Rating>();
            foreach(double playerSkill in playerSkills)
            {
                ratings.Add(new Rating(calc));
            }
            Random rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                int player1 = rnd.Next(0, playerSkills.Length);
                int player2 = player1;
                while (player2 == player1)
                {
                    player2 = rnd.Next(0, playerSkills.Length);
                }
                Rating firstPlayer = ratings[player1];
                Rating secondPlayer = ratings[player2];
                double skill1 = playerSkills[player1];
                double skill2 = playerSkills[player2];
                bool p1LowSkill = skill1 == 1;
                bool p2LowSkill = skill2 == 1;
                if (p1LowSkill)
                {
                    skill1 = 3;
                }
                if (p2LowSkill)
                {
                    skill2 = 3;
                }

                double skillMultiplier = 1.0 / (skill1 + skill2);
                skill1 *= skillMultiplier;
                skill2 *= skillMultiplier;
                double diceThrow = rnd.NextDouble();
                if (diceThrow < skill1)
                {
                    periodResults.AddResult(firstPlayer,secondPlayer, p1LowSkill ? 0.3 : 1.0);
                } else
                {
                    periodResults.AddResult(secondPlayer, firstPlayer, p2LowSkill ? 0.3 : 1.0);
                }
                if((i % 15)==0)
                {
                    calc.UpdateRatings(periodResults);
                }
            }
            calc.UpdateRatings(periodResults);

            Trace.WriteLine($"All weighted 0.5 results: \n");
            double lowSkillAverage = 0;
            double lowSkillAverageDev = 0;
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
                if (i != (playerSkills.Length - 1))
                {
                    lowSkillAverage += ratings[i].GetRating();
                    lowSkillAverageDev += ratings[i].GetRatingDeviation();
                }
            }
            lowSkillAverage /= (double)playerSkills.Length - 1;
            lowSkillAverageDev /= (double)playerSkills.Length - 1;
            Trace.WriteLine($"\nLow skill average: {lowSkillAverage}+-{lowSkillAverageDev}\n");
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestLowSkillTripledUnweighted()
        {
            double[] playerSkills = new double[] {1,1,1,1,1,1,1,1,1,3 };
            RatingCalculator calc = new RatingCalculator();
            RatingPeriodResults periodResults = new RatingPeriodResults();

            List<Rating> ratings = new List<Rating>();
            foreach(double playerSkill in playerSkills)
            {
                ratings.Add(new Rating(calc));
            }
            Random rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                int player1 = rnd.Next(0, playerSkills.Length);
                int player2 = player1;
                while (player2 == player1)
                {
                    player2 = rnd.Next(0, playerSkills.Length);
                }
                Rating firstPlayer = ratings[player1];
                Rating secondPlayer = ratings[player2];
                double skill1 = playerSkills[player1];
                double skill2 = playerSkills[player2];
                bool p1LowSkill = skill1 == 1;
                bool p2LowSkill = skill2 == 1;
                if (p1LowSkill)
                {
                    skill1 = 3;
                }
                if (p2LowSkill)
                {
                    skill2 = 3;
                }

                double skillMultiplier = 1.0 / (skill1 + skill2);
                skill1 *= skillMultiplier;
                skill2 *= skillMultiplier;
                double diceThrow = rnd.NextDouble();
                if (diceThrow < skill1)
                {
                    periodResults.AddResult(firstPlayer,secondPlayer, 1);
                } else
                {
                    periodResults.AddResult(secondPlayer, firstPlayer, 1);
                }
                if((i % 15)==0)
                {
                    calc.UpdateRatings(periodResults);
                }
            }
            calc.UpdateRatings(periodResults);

            Trace.WriteLine($"All weighted 0.5 results: \n");
            double lowSkillAverage = 0;
            double lowSkillAverageDev = 0;
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
                if (i != (playerSkills.Length - 1))
                {
                    lowSkillAverage += ratings[i].GetRating();
                    lowSkillAverageDev += ratings[i].GetRatingDeviation();
                }
            }
            lowSkillAverage /= (double)playerSkills.Length - 1;
            lowSkillAverageDev /= (double)playerSkills.Length - 1;
            Trace.WriteLine($"\nLow skill average: {lowSkillAverage}+-{lowSkillAverageDev}\n");
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestWeighted2()
        {
            double[] playerSkills = new double[] {1,1,1,1,1,1,1,1,1,3 };
            RatingCalculator calc = new RatingCalculator();
            RatingPeriodResults periodResults = new RatingPeriodResults();

            List<Rating> ratings = new List<Rating>();
            foreach(double playerSkill in playerSkills)
            {
                ratings.Add(new Rating(calc));
            }
            Random rnd = new Random();
            for(int i = 0; i < 10000; i++)
            {
                int player1 = rnd.Next(0, playerSkills.Length);
                int player2 = player1;
                while (player2 == player1)
                {
                    player2 = rnd.Next(0, playerSkills.Length);
                }
                Rating firstPlayer = ratings[player1];
                Rating secondPlayer = ratings[player2];
                double skill1 = playerSkills[player1];
                double skill2 = playerSkills[player2];
                double skillMultiplier = 1.0 / (skill1 + skill2);
                skill1 *= skillMultiplier;
                skill2 *= skillMultiplier;
                double diceThrow = rnd.NextDouble();
                if (diceThrow < skill1)
                {
                    periodResults.AddResult(firstPlayer,secondPlayer,2);
                } else
                {
                    periodResults.AddResult(secondPlayer, firstPlayer,2);
                }
                if((i % 15)==0)
                {
                    calc.UpdateRatings(periodResults);
                }
            }
            calc.UpdateRatings(periodResults);

            Trace.WriteLine($"All weighted 2.0 results: \n");
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
            }
            Assert.IsTrue(true);
        }
    }
}

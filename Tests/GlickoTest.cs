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
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
            }
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
            for (int i = 0; i < playerSkills.Length; i++)
            {
                Trace.WriteLine($"{i}: {ratings[i].GetRating()}+-{ratings[i].GetRatingDeviation()}");
            }
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

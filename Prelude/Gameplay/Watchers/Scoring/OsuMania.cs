﻿using System;
using System.Collections.Generic;
using Prelude.Utilities;

namespace Prelude.Gameplay.Watchers.Scoring
{
    [DataTemplate("OD", "Default", 9f, "Min", 0f, "Max", 10f, "Step", 0.1f)]
    public class OsuMania : ScoreSystem
    {
        //This is a simulation of osu!mania's hit windows and points system.
        //The "Overall Difficulty" setting can be adjusted to tighten or relax the hit windows
        //This does not recreate the numerical score out of 1 million, only the displayed accuracy
        public OsuMania(DataGroup Settings) : base("o!m OD" + Math.Round(Settings.GetValue("OD", 9f), 1).ToString(), 6)
        {
            MaxPointsPerNote = 300;
            PointsPerJudgement = new int[] { 300, 300, 200, 100, 50, 0 };
            ComboBreakingJudgement = 5;
            float od = Settings.GetValue("OD", 9f);
            JudgementWindows = new float[] {
                16.5f,
                64.5f - od * 3,
                97.5f - od * 3,
                127.5f - od * 3,
                151.5f - od * 3
            };
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAVSRG.Beatmap;

namespace YAVSRG.Gameplay
{
    public class ScoreTracker //handles scoring while you play through a chart, keeping track of hits and acc and stuff
    {
        public class HitData : OffsetItem
        {
            public float[] delta;
            public byte[] hit;

            public HitData(Snap s, int keycount)
            {
                Offset = s.Offset;
                hit = new byte[keycount];
                foreach (int k in s.Combine().GetColumns())
                {
                    hit[k] = 1;
                }
                delta = new float[keycount];
            }

            public int Count
            {
                get
                {
                    int x = 0;
                    for (int i = 0; i < hit.Length; i++)
                    {
                        if (hit[i] > 0)
                        {
                            x++;
                        }
                    }
                    return x;
                }
            }
        }

        public event Action<int, int, float> OnHit;

        public Chart c;
        public ScoreSystem Scoring;
        public HitData[] hitdata;
        public int maxcombo;

        public ScoreTracker(Chart c)
        {
            this.c = c;
            //some temp hack until i move this inside ScoreSystem
            maxcombo = 0;
            foreach (Snap s in c.States.Points)
            {
                maxcombo += s.Count;
            }
            Scoring = ScoreSystem.GetScoreSystem(Game.Options.Profile.ScoreSystem);

            Scoring.OnHit = (k, j, d) => { OnHit(k, j, d); };

            int count = c.States.Count;
            hitdata = new HitData[count];
            for (int i = 0; i < count; i++)
            {
                hitdata[i] = new HitData(c.States.Points[i], c.Keys);
            }
        }

        public int Combo()
        {
            return Scoring.Combo;
        }

        public float Accuracy()
        {
            return Scoring.Accuracy();
        }

        public void Update(float time)
        {
            Scoring.Update(time,hitdata);
            
        }

        public void RegisterHit(int i, int k, float delta)
        {
            if (hitdata[i].hit[k] == 2) { return; } //ignore if the note is already hit. prevents mashing exploit.
            hitdata[i].hit[k] = 2; //mark that note was not only supposed to be hit, but was also hit
            hitdata[i].delta[k] = delta;
            Scoring.HandleHit(k, i, hitdata);
            //OnHit(k, Scoring.JudgeHit(Math.Abs(delta)), delta);
        }

        public bool EndOfChart()
        {
            return Scoring.EndOfChart(hitdata.Length);
        }
    }
}
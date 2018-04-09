﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAVSRG.Beatmap;

namespace YAVSRG.Gameplay
{
    public class ChartSaveData
    {
        public string Path;
        public float Offset;

        public static ChartSaveData FromChart(Chart c)
        {
            return new ChartSaveData()
            {
                Path = c.path, //this needs to be the absolute path
                Offset = c.Notes.Count > 0 ? c.Notes.Points[0].Offset : 0
            };
        }
    }
}
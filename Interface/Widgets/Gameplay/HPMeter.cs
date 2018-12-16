﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YAVSRG.Gameplay;

namespace YAVSRG.Interface.Widgets.Gameplay
{
    public class HPMeter : GameplayWidget
    {
        bool Horizontal;

        public HPMeter(ScoreTracker scoreTracker, Options.WidgetPosition pos) : base(scoreTracker, pos)
        {
            Horizontal = pos.GetValue("Horizontal", true);
        }

        public override void Draw(Rect bounds)
        {
            base.Draw(bounds);
            bounds = GetBounds(bounds);
            if (Horizontal)
            {
                SpriteBatch.DrawRect(bounds.SliceLeft(bounds.Width * scoreTracker.HP.GetValue()), System.Drawing.Color.White);
            }
            else
            {
                SpriteBatch.DrawRect(bounds.SliceBottom(bounds.Height * scoreTracker.HP.GetValue()), System.Drawing.Color.White);
            }
        }
    }
}

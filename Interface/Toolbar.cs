﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using static YAVSRG.Interface.ScreenUtils;
using YAVSRG.Interface.Widgets;
using YAVSRG.Interface.Animations;
using YAVSRG.Interface.Screens;

namespace YAVSRG.Interface
{
    public class Toolbar : Widget
    {
        AnimationSlider slide;
        bool hidden;
        public bool cursor = true;
        public ChatBox Chat;

        public Toolbar()
        {
            AddChild(
                new Button("buttonback", "Back", () => { Game.Screens.PopScreen(); })
                .PositionTopLeft(0, 0, AnchorType.MIN, AnchorType.MIN).PositionBottomRight(240, 80, AnchorType.MIN, AnchorType.MIN));
            AddChild(
                new Button("buttonmusic", "Visualiser", () => { if (!(Game.Screens.Current is ScreenVisualiser && Game.Gameplay.CurrentCachedChart != null)) Game.Screens.AddScreen(new ScreenVisualiser()); })
                .PositionTopLeft(160, 0, AnchorType.MAX, AnchorType.MIN).PositionBottomRight(80, 80, AnchorType.MAX, AnchorType.MIN));
            AddChild(
                new Button("buttonoptions", "Options", () => { if (!(Game.Screens.Current is ScreenOptions)) Game.Screens.AddScreen(new ScreenOptions()); })
                .PositionTopLeft(240, 0, AnchorType.MAX, AnchorType.MIN).PositionBottomRight(160, 80, AnchorType.MAX, AnchorType.MIN));
            AddChild(
                new Button("buttonimport", "Import", () => { if (!(Game.Screens.Current is ScreenImport)) Game.Screens.AddScreen(new ScreenImport()); })
                .PositionTopLeft(320, 0, AnchorType.MAX, AnchorType.MIN).PositionBottomRight(240, 80, AnchorType.MAX, AnchorType.MIN));
            AddChild(
                new Button("buttononline", "Multiplayer", () => { if (!(Game.Screens.Current is ScreenLobby)) Game.Screens.AddScreen(new ScreenLobby()); })
                .PositionTopLeft(400, 0, AnchorType.MAX, AnchorType.MIN).PositionBottomRight(320, 80, AnchorType.MAX, AnchorType.MIN));
            Chat = new ChatBox();
            AddChild(Chat.PositionTopLeft(0, 80, AnchorType.MIN, AnchorType.MAX).PositionBottomRight(0, 80, AnchorType.MAX, AnchorType.MAX));
            AddChild(new TaskDisplay());
            slide = new AnimationSlider(-10);
            Animation.Add(slide);
        }

        public void Collapse()
        {
            slide.Target = -10;
        }

        public void Expand()
        {
            slide.Target = 80;
        }

        public void SetHidden(bool v)
        {
            hidden = v;
            if (hidden) { Collapse(); }
            else { Expand(); }
        }

        public new float Height
        {
            get { return Math.Max(slide.Val, 0); }
        }

        public override void Draw(Rect bounds)
        {
            if (slide > 1)
            {
                float s = (ScreenHeight * 2 - slide * 2) / 24f;
                for (int i = 0; i < 24; i++) //draws the waveform
                {
                    float level = Game.Audio.WaveForm[i * 4] + Game.Audio.WaveForm[i * 4 + 1] + Game.Audio.WaveForm[i * 4 + 2] + Game.Audio.WaveForm[i * 4 + 3];
                    level += 0.01f;
                    level *= slide * 0.002f;
                    SpriteBatch.DrawRect(new Rect(-ScreenWidth, -ScreenHeight + slide + i * s, -ScreenWidth + level, -ScreenHeight + slide - 2 + (i + 1) * s), Color.FromArgb(100, Game.Screens.HighlightColor));
                    SpriteBatch.DrawRect(new Rect(ScreenWidth - level, -ScreenHeight + slide + i * s, ScreenWidth, -ScreenHeight + slide - 2 + (i + 1) * s), Color.FromArgb(100, Game.Screens.HighlightColor));
                }

                //top
                Game.Screens.DrawChartBackground(new Rect(-ScreenWidth, -ScreenHeight, ScreenWidth, -ScreenHeight + slide), Game.Screens.DarkColor, 2f);
                SpriteBatch.Draw("toolbar", new Rect(-ScreenWidth, -ScreenHeight, ScreenWidth, -ScreenHeight + slide), Color.FromArgb(127, Game.Screens.BaseColor));
                DrawFrame(new Rect(-ScreenWidth - 30, -ScreenHeight - 30, ScreenWidth + 30, -ScreenHeight + slide + 5), 30f, Game.Screens.BaseColor);

                //bottom
                Game.Screens.DrawChartBackground(new Rect(-ScreenWidth, ScreenHeight - slide, ScreenWidth, ScreenHeight), Game.Screens.DarkColor, 2f);
                SpriteBatch.Draw("toolbar", new Rect(-ScreenWidth, ScreenHeight - slide, ScreenWidth, ScreenHeight), Color.FromArgb(127, Game.Screens.BaseColor), 2);
                DrawFrame(new Rect(-ScreenWidth - 30, ScreenHeight - slide - 5, ScreenWidth + 30, ScreenHeight + 30), 30f, Game.Screens.BaseColor);

                SpriteBatch.Font1.DrawText(Game.Options.Profile.Name, 30f, -ScreenWidth, ScreenHeight - slide + 5, Game.Options.Theme.MenuFont);
                SpriteBatch.Font2.DrawCentredText("Plays: " + Game.Options.Profile.Stats.TimesPlayed.ToString(), 18f, 0, ScreenHeight - slide + 5, Game.Options.Theme.MenuFont);
                SpriteBatch.Font2.DrawCentredText("Playtime: " + Utils.FormatTime(Game.Options.Profile.Stats.SecondsPlayed * 1000), 18f, 0, ScreenHeight - slide + 28, Game.Options.Theme.MenuFont);
                SpriteBatch.Font2.DrawCentredText("S Ranks: " + Game.Options.Profile.Stats.SRanks, 18f, 0, ScreenHeight - slide + 51, Game.Options.Theme.MenuFont);
                SpriteBatch.Font1.DrawJustifiedText(Game.Version, 25f, ScreenWidth, ScreenHeight - slide + 5, Game.Options.Theme.MenuFont);
                SpriteBatch.Font1.DrawJustifiedText(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString(), 25f, ScreenWidth, ScreenHeight - slide + 45, Game.Options.Theme.MenuFont);

                base.Draw(bounds.ExpandY(80-slide));
            }

            if (!(hidden || !cursor)) SpriteBatch.Draw("cursor", new Rect(Input.MouseX, Input.MouseY, Input.MouseX + 48, Input.MouseY + 48), Game.Screens.HighlightColor);
        }

        public override void Update(Rect bounds)
        {
            if (Game.Screens.InDialog()) { return; }
            base.Update(bounds.ExpandY(80 - slide));
            if (!hidden)
            {
                if (Input.KeyTap(OpenTK.Input.Key.Escape))
                {
                    Game.Screens.PopScreen();
                }
                if (Input.KeyTap(OpenTK.Input.Key.T) && Input.KeyPress(OpenTK.Input.Key.ControlLeft))
                {
                    if (slide.Target <= 0)
                    {
                        Expand();
                    }
                    else
                    {
                        Collapse();
                    }
                }
            }
        }
    }
}

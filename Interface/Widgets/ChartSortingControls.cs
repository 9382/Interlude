﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using YAVSRG.Charts;
using static YAVSRG.Charts.ChartLoader;
using OpenTK;

namespace YAVSRG.Interface.Widgets
{
    public class ChartSortingControls : Widget
    {
        ScrollContainer sort;
        ScrollContainer group;
        Widget sortB, groupB;

        public ChartSortingControls() : base()
        {
            group = new ScrollContainer(20, 10, false, 0) { State = 0 };
            sort = new ScrollContainer(20, 10, false, 0) { State = 0 };
            AddChild(sortB = new SimpleButton("Sort by...", () => {
                sort.State = 1 - sort.State;
            }, () => { return false; }, 20f).PositionTopLeft(260, 50, AnchorType.MAX, AnchorType.MAX).PositionBottomRight(20, 10, AnchorType.MAX, AnchorType.MAX));

            sort.AddChild(SortButton("Difficulty", SortByDifficulty));
            sort.AddChild(SortButton("Artist", SortByArtist));
            sort.AddChild(SortButton("Creator", SortByCreator));
            sort.AddChild(SortButton("Title", SortByTitle));
            AddChild(sort.PositionTopLeft(260, 0, AnchorType.MAX, AnchorType.MAX).PositionBottomRight(20,-400,AnchorType.MAX,AnchorType.MAX));

            AddChild(groupB = new SimpleButton("Group by...", () => {
                group.State = 1 - group.State;
            }, () => { return false; }, 20f).PositionTopLeft(520, 50, AnchorType.MAX, AnchorType.MAX).PositionBottomRight(280, 10, AnchorType.MAX, AnchorType.MAX));
            group.AddChild(GroupButton("Pack", GroupByPack));
            group.AddChild(GroupButton("Difficulty", GroupByDifficulty));
            group.AddChild(GroupButton("Artist", GroupByArtist));
            group.AddChild(GroupButton("Creator", GroupByCreator));
            group.AddChild(GroupButton("Title", GroupByTitle));
            group.AddChild(GroupButton("Keymode", GroupByKeymode));
            group.AddChild(GroupButton("Collection", GroupByCollection));
            AddChild(group.PositionTopLeft(520, 0, AnchorType.MAX, AnchorType.MAX).PositionBottomRight(280, -400, AnchorType.MAX, AnchorType.MAX));

            AddChild(new TextEntryBox((s) => { SearchString = s; }, () => { return SearchString; }, () => { Refresh(); }, null, () => { return "Press " + Game.Options.General.Binds.Search.ToString().ToUpper() + " to search..."; })
                .PositionTopLeft(520,10,AnchorType.MAX,AnchorType.MIN).PositionBottomRight(20,70,AnchorType.MAX,AnchorType.MIN));
            
        }

        private Widget SortButton(string l, Comparison<CachedChart> m)
        {
            return new SimpleButton(l, () => { SortMode = m; Refresh(); }, () => { return SortMode == m; }, 15f).PositionBottomRight(200,35,AnchorType.MIN,AnchorType.MIN);
        }

        private Widget GroupButton(string l, Func<CachedChart,string> m)
        {
            return new SimpleButton(l, () => { GroupMode = m; Refresh(); }, () => { return GroupMode == m; }, 15f).PositionBottomRight(200, 35, AnchorType.MIN, AnchorType.MIN);
        }

        public override void Draw(Rect bounds)
        {
            bounds = GetBounds(bounds);
            Game.Screens.DrawChartBackground(bounds, Utils.ColorInterp(Color.FromArgb(255,0,0,0), Game.Screens.BaseColor, 0.8f),1.5f);
            ScreenUtils.DrawFrame(bounds.ExpandX(30), 30f, Game.Screens.HighlightColor);
            DrawWidgets(bounds);
        }
    }
}

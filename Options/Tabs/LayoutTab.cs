﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Input;
using YAVSRG.Interface;
using YAVSRG.Interface.Widgets;

namespace YAVSRG.Options.Tabs
{
    class LayoutTab : Widget
    {
        private Widget selectKeyMode;
        private KeyBinder[] binds = new KeyBinder[10];
        private ColorPicker[] colors = new ColorPicker[10];
        private int keyMode = 4;
        private Sprite texture;

        protected class ColorPicker : Widget
        {
            string label;
            Action<int> select;
            Func<int> get;
            Sprite s;
            int max;

            public ColorPicker(string label, Action<int> select, Func<int> get, Sprite s, int max)
            {
                Change(label, select, get, s, max);
            }

            public void Change(string label, Action<int> select, Func<int> get, Sprite s, int max)
            {
                this.label = label;
                this.select = select;
                this.get = get;
                this.s = s;
                this.max = max;
            }

            public override void Draw(float left, float top, float right, float bottom)
            {
                base.Draw(left, top, right, bottom);
                ConvertCoordinates(ref left, ref top, ref right, ref bottom);
                Game.Options.Theme.DrawNote(s, left, top, right, bottom, 0, 0, get(), 0);
            }

            public override void Update(float left, float top, float right, float bottom)
            {
                base.Update(left, top, right, bottom);
                ConvertCoordinates(ref left, ref top, ref right, ref bottom);
                if (ScreenUtils.MouseOver(left, top, right, bottom))
                {
                    if (Input.MouseClick(MouseButton.Left))
                    {
                        select(Utils.Modulus(get() + 1, max));
                    }
                    else if (Input.MouseClick(MouseButton.Right))
                    {
                        select(Utils.Modulus(get() - 1, max));
                    }
                }
            }
        }

        public LayoutTab() : base()
        {
            selectKeyMode = new TextPicker("Keys", new string[] { "3K", "4K", "5K", "6K", "7K", "8K", "9K", "10K" }, 1, (i) => { ChangeKeyMode(i + 3); })
                .PositionTopLeft(-50, 25, AnchorType.CENTER, AnchorType.MIN).PositionBottomRight(50, 75, AnchorType.CENTER, AnchorType.MIN);
            for (int i = 0; i < 10; i++)
            {
                binds[i] = new KeyBinder("Column " + (i + 1).ToString(), Key.F35, (b) => { });
                AddChild(binds[i]);
                colors[i] = new ColorPicker("", null, null, new Sprite(), 1);
                AddChild(colors[i]);
            }
            AddChild(selectKeyMode);
            Refresh();
            AddChild(new BoolPicker("Different colors per keymode", !Game.Options.Profile.ColorStyle.UseForAllKeyModes, (i) => { Game.Options.Profile.ColorStyle.UseForAllKeyModes = !i; Refresh(); })
                .PositionTopLeft(-500, 425, AnchorType.CENTER, AnchorType.MIN).PositionBottomRight(-200, 475, AnchorType.CENTER, AnchorType.MIN));
            AddChild(new TextPicker("Skin [REQUIRES GAME RESTART]", Options.Skins, 0, (i) => { Game.Options.Profile.Skin = Options.Skins[i]; Content.ClearStore(); })
                .PositionTopLeft(200, 425, AnchorType.CENTER, AnchorType.MIN).PositionBottomRight(500, 475, AnchorType.CENTER, AnchorType.MIN));
        }

        public void Refresh()
        {
            if (Game.Options.Profile.Keymode == 0)
            {
                selectKeyMode.State = 1;
            }
            else
            {
                keyMode = Game.Options.Profile.Keymode;
                selectKeyMode.State = 0;
            }
            ChangeKeyMode(keyMode);
        }

        private Action<Key> BindSetter(int i, int k)
        {
            return (key) => { Game.Options.Profile.Bindings[k][i] = key; };
        }
        private Action<int> ColorSetter(int i, int k)
        {
            return (s) => { Game.Options.Profile.ColorStyle.SetColorIndex(i, k, s);};
        }
        private Func<int> ColorGetter(int i, int k)
        {
            return () => { return Game.Options.Profile.ColorStyle.GetColorIndex(i, k); };
        }

        private void ChangeKeyMode(int k)
        {
            texture = Game.Options.Theme.GetNoteTexture(k);
            for (int i = 0; i < 10; i++)
            {
                binds[i].State = 0;
                colors[i].State = 0;
            }
            keyMode = k;
            int c = Game.Options.Theme.ColumnWidth;
            int start = -k * c / 2;
            for (int i = 0; i < k; i++)
            {
                binds[i].Change(Game.Options.Profile.Bindings[k][i], BindSetter(i, k));
                binds[i].State = 1;
                binds[i].PositionTopLeft(start + i * c, 100, AnchorType.CENTER, AnchorType.MIN).PositionBottomRight(start + c + i * c, 150, AnchorType.CENTER, AnchorType.MIN);
            }
            int colorCount = Game.Options.Profile.ColorStyle.GetColorCount(k);
            int availableColors = Game.Options.Theme.UseColor ? Game.Options.Theme.NoteColors.Length : texture.UV_Y;
            start = -colorCount * c / 2;
            int keymodeIndex = Game.Options.Profile.ColorStyle.UseForAllKeyModes ? 0 : k;
            for (int i = 0; i < colorCount; i++)
            {
                colors[i].Change("label NYI", ColorSetter(i,keymodeIndex), ColorGetter(i,keymodeIndex), texture, availableColors);
                colors[i].State = 1;
                colors[i].PositionTopLeft(start + i * c, 200, AnchorType.CENTER, AnchorType.MIN).PositionBottomRight(start + c + i * c, 200+Game.Options.Theme.ColumnWidth, AnchorType.CENTER, AnchorType.MIN);
            }
        }
    }
}
#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Clone.cs is part of SFXClone.

 SFXClone is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXClone is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXClone. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXClone.Classes;
using SFXClone.Library;
using SFXClone.Library.Logger;

#endregion

namespace SFXClone.Features.Drawings
{
    internal class Clone : Child<App>
    {
        private readonly List<string> _cloneHeroes = new List<string> { "shaco", "leblanc", "monkeyking", "yorick" };
        private readonly List<Obj_AI_Hero> _heroes = new List<Obj_AI_Hero>();

        public Clone(App parent) : base(parent)
        {
            OnLoad();
        }

        public override string Name
        {
            get { return "Clone"; }
        }

        protected override void OnEnable()
        {
            Drawing.OnDraw += OnDrawingDraw;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Drawing.OnDraw -= OnDrawingDraw;
            base.OnDisable();
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                var color = Menu.Item(Name + "DrawingCircleColor").GetValue<Color>();
                var radius = Menu.Item(Name + "DrawingCircleRadius").GetValue<Slider>().Value;
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                foreach (var hero in
                    _heroes.Where(hero => !hero.IsDead && hero.IsVisible && hero.Position.IsOnScreen()))
                {
                    Render.Circle.DrawCircle(hero.Position, hero.BoundingRadius + radius, color, thickness);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override sealed void OnLoad()
        {
            try
            {
                Menu = new Menu(Name, Name);
                var drawingMenu = new Menu("Drawing", Name + "Drawing");
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "CircleColor", "Circle Color").SetValue(Color.YellowGreen));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "CircleRadius", "Circle Radius").SetValue(new Slider(30)));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + "CircleThickness", "Circle Thickness").SetValue(
                        new Slider(2, 1, 10)));

                Menu.AddSubMenu(drawingMenu);

                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(true));

                Parent.Menu.AddSubMenu(Menu);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                _heroes.AddRange(
                    GameObjects.EnemyHeroes.Where(hero => _cloneHeroes.Contains(hero.ChampionName.ToLower())));

                if (!_heroes.Any())
                {
                    OnUnload(null, new UnloadEventArgs(true));
                    return;
                }

                base.OnInitialize();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}
#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Range.cs is part of SFXRange.

 SFXRange is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXRange is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXRange. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXRange.Classes;
using SFXRange.Library;
using SFXRange.Library.Extensions.SharpDX;
using SFXRange.Library.Logger;

#endregion

namespace SFXRange.Features.Drawings
{
    internal class Range : Child<App>
    {
        private const float ExperienceRange = 1350f;
        private const float TurretRange = 875f;
        private const float ShopRange = 1250f;

        public Range(App parent) : base(parent)
        {
            OnLoad();
        }

        public override string Name
        {
            get { return Global.Lang.Get("F_Range"); }
        }

        private void DrawAttack()
        {
            try
            {
                var drawAlly = Menu.Item(Name + "AttackAlly").GetValue<bool>();
                var drawEnemy = Menu.Item(Name + "AttackEnemy").GetValue<bool>();
                var drawSelf = Menu.Item(Name + "AttackSelf").GetValue<bool>();
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                if (!drawAlly && !drawEnemy && !drawSelf)
                {
                    return;
                }

                var allyColor = Menu.Item(Name + "AttackColorAlly").GetValue<Color>();
                var enemyColor = Menu.Item(Name + "AttackColorEnemy").GetValue<Color>();
                var selfColor = Menu.Item(Name + "AttackColorSelf").GetValue<Color>();

                foreach (var hero in
                    GameObjects.Heroes.Where(hero => !hero.IsDead && hero.IsVisible)
                        .Where(
                            hero =>
                                (hero.IsAlly && drawAlly || hero.IsMe && drawSelf || hero.IsEnemy && drawEnemy) &&
                                !(hero.IsMe && !drawSelf)))
                {
                    var radius = hero.BoundingRadius + hero.AttackRange +
                                 (hero.IsAlly
                                     ? GameObjects.EnemyHeroes.Average(e => e.BoundingRadius)
                                     : ObjectManager.Player.BoundingRadius);
                    if (hero.Position.IsOnScreen(radius))
                    {
                        Render.Circle.DrawCircle(
                            hero.Position, radius, hero.IsMe ? selfColor : (hero.IsEnemy ? enemyColor : allyColor),
                            thickness);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void DrawExperience()
        {
            try
            {
                var drawAlly = Menu.Item(Name + "ExperienceAlly").GetValue<bool>();
                var drawEnemy = Menu.Item(Name + "ExperienceEnemy").GetValue<bool>();
                var drawSelf = Menu.Item(Name + "ExperienceSelf").GetValue<bool>();
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                if (!drawAlly && !drawEnemy && !drawSelf)
                {
                    return;
                }

                var allyColor = Menu.Item(Name + "ExperienceColorAlly").GetValue<Color>();
                var enemyColor = Menu.Item(Name + "ExperienceColorEnemy").GetValue<Color>();
                var selfColor = Menu.Item(Name + "ExperienceColorSelf").GetValue<Color>();

                foreach (var hero in
                    GameObjects.Heroes.Where(hero => !hero.IsDead && hero.IsVisible)
                        .Where(
                            hero =>
                                (hero.IsAlly && drawAlly || hero.IsMe && drawSelf || hero.IsEnemy && drawEnemy) &&
                                !(hero.IsMe && !drawSelf) && hero.Position.IsOnScreen(ExperienceRange)))
                {
                    Render.Circle.DrawCircle(
                        hero.Position, ExperienceRange, hero.IsMe ? selfColor : (hero.IsEnemy ? enemyColor : allyColor),
                        thickness);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void DrawSpell()
        {
            try
            {
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                var drawAllyQ = Menu.Item(Name + "SpellAllyQ").GetValue<bool>();
                var drawAllyW = Menu.Item(Name + "SpellAllyW").GetValue<bool>();
                var drawAllyE = Menu.Item(Name + "SpellAllyE").GetValue<bool>();
                var drawAllyR = Menu.Item(Name + "SpellAllyR").GetValue<bool>();
                var drawAlly = drawAllyQ || drawAllyW || drawAllyE || drawAllyR;

                var drawEnemyQ = Menu.Item(Name + "SpellEnemyQ").GetValue<bool>();
                var drawEnemyW = Menu.Item(Name + "SpellEnemyW").GetValue<bool>();
                var drawEnemyE = Menu.Item(Name + "SpellEnemyE").GetValue<bool>();
                var drawEnemyR = Menu.Item(Name + "SpellEnemyR").GetValue<bool>();
                var drawEnemy = drawEnemyQ || drawEnemyW || drawEnemyE || drawEnemyR;

                var drawSelfQ = Menu.Item(Name + "SpellSelfQ").GetValue<bool>();
                var drawSelfW = Menu.Item(Name + "SpellSelfW").GetValue<bool>();
                var drawSelfE = Menu.Item(Name + "SpellSelfE").GetValue<bool>();
                var drawSelfR = Menu.Item(Name + "SpellSelfR").GetValue<bool>();
                var drawSelf = drawSelfQ || drawSelfW || drawSelfE || drawSelfR;

                if (!drawAlly && !drawEnemy && !drawSelf)
                {
                    return;
                }

                var spellMaxRange = Menu.Item(Name + "SpellMaxRange").GetValue<Slider>().Value;

                foreach (var hero in GameObjects.Heroes.Where(hero => !hero.IsDead && hero.IsVisible))
                {
                    if ((hero.IsAlly && drawAllyQ || hero.IsEnemy && drawEnemyQ || hero.IsMe && drawSelfQ) &&
                        !(hero.IsMe && !drawSelfQ))
                    {
                        var range = hero.Spellbook.GetSpell(SpellSlot.Q).SData.CastRange;
                        if (range <= spellMaxRange && hero.Position.IsOnScreen(range))
                        {
                            Render.Circle.DrawCircle(
                                hero.Position, range,
                                Menu.Item(
                                    Name + "Spell" + (hero.IsMe ? "Self" : (hero.IsEnemy ? "Enemy" : "Ally")) + "ColorQ")
                                    .GetValue<Color>(), thickness);
                        }
                    }
                    if ((hero.IsAlly && drawAllyW || hero.IsEnemy && drawEnemyW || hero.IsMe && drawSelfW) &&
                        !(hero.IsMe && !drawSelfW))
                    {
                        var range = hero.Spellbook.GetSpell(SpellSlot.W).SData.CastRange;
                        if (range <= spellMaxRange && hero.Position.IsOnScreen(range))
                        {
                            Render.Circle.DrawCircle(
                                hero.Position, range,
                                Menu.Item(
                                    Name + "Spell" + (hero.IsMe ? "Self" : (hero.IsEnemy ? "Enemy" : "Ally")) + "ColorW")
                                    .GetValue<Color>(), thickness);
                        }
                    }
                    if ((hero.IsAlly && drawAllyE || hero.IsEnemy && drawEnemyE || hero.IsMe && drawSelfE) &&
                        !(hero.IsMe && !drawSelfE))
                    {
                        var range = hero.Spellbook.GetSpell(SpellSlot.E).SData.CastRange;
                        if (range <= spellMaxRange && hero.Position.IsOnScreen(range))
                        {
                            Render.Circle.DrawCircle(
                                hero.Position, range,
                                Menu.Item(
                                    Name + "Spell" + (hero.IsMe ? "Self" : (hero.IsEnemy ? "Enemy" : "Ally")) + "ColorE")
                                    .GetValue<Color>(), thickness);
                        }
                    }
                    if ((hero.IsAlly && drawAllyR || hero.IsEnemy && drawEnemyR || hero.IsMe && drawSelfR) &&
                        !(hero.IsMe && !drawSelfR))
                    {
                        var range = hero.Spellbook.GetSpell(SpellSlot.R).SData.CastRange;
                        if (range <= spellMaxRange && hero.Position.IsOnScreen(range))
                        {
                            Render.Circle.DrawCircle(
                                hero.Position, range,
                                Menu.Item(
                                    Name + "Spell" + (hero.IsMe ? "Self" : (hero.IsEnemy ? "Enemy" : "Ally")) + "ColorR")
                                    .GetValue<Color>(), thickness);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void DrawTurret()
        {
            try
            {
                var drawAlly = Menu.Item(Name + "TurretAlly").GetValue<bool>();
                var drawEnemy = Menu.Item(Name + "TurretEnemy").GetValue<bool>();
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                if (!drawAlly && !drawEnemy)
                {
                    return;
                }

                var allyColor = Menu.Item(Name + "TurretColorAlly").GetValue<Color>();
                var enemyColor = Menu.Item(Name + "TurretColorEnemy").GetValue<Color>();

                foreach (var turret in
                    GameObjects.Turrets.Where(
                        t =>
                            t.IsValid && !t.IsDead && t.Health > 1f && t.IsVisible &&
                            (t.IsAlly && drawAlly || t.IsEnemy && drawEnemy) && t.Position.IsOnScreen(TurretRange)))
                {
                    Render.Circle.DrawCircle(
                        turret.Position, TurretRange, turret.IsAlly ? allyColor : enemyColor, thickness);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void DrawShop()
        {
            try
            {
                var drawAlly = Menu.Item(Name + "ShopAlly").GetValue<bool>();
                var drawEnemy = Menu.Item(Name + "ShopEnemy").GetValue<bool>();
                var thickness = Menu.Item(Name + "DrawingCircleThickness").GetValue<Slider>().Value;

                if (!drawAlly && !drawEnemy)
                {
                    return;
                }

                var allyColor = Menu.Item(Name + "ShopColorAlly").GetValue<Color>();
                var enemyColor = Menu.Item(Name + "ShopColorEnemy").GetValue<Color>();

                foreach (var shop in
                    GameObjects.Shops.Where(
                        t =>
                            t.IsValid && !t.IsDead && t.Health > 1f && t.IsVisible &&
                            (t.IsAlly && drawAlly || t.IsEnemy && drawEnemy) && t.Position.IsOnScreen(ShopRange)))
                {
                    Render.Circle.DrawCircle(shop.Position, ShopRange, shop.IsAlly ? allyColor : enemyColor, thickness);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                DrawExperience();
                DrawTurret();
                DrawAttack();
                DrawSpell();
                DrawShop();
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
                var drawingMenu = new Menu(Global.Lang.Get("G_Drawing"), Name + "Drawing");
                drawingMenu.AddItem(
                    new MenuItem(
                        drawingMenu.Name + "CircleThickness",
                        Global.Lang.Get("G_Circle") + " " + Global.Lang.Get("G_Thickness")).SetValue(
                            new Slider(2, 1, 10)));

                var experienceMenu = new Menu(Global.Lang.Get("G_Experience"), Name + "Experience");
                experienceMenu.AddItem(
                    new MenuItem(
                        experienceMenu.Name + "ColorSelf", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Self"))
                        .SetValue(Color.Gray));
                experienceMenu.AddItem(
                    new MenuItem(
                        experienceMenu.Name + "ColorAlly", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Ally"))
                        .SetValue(Color.Gray));
                experienceMenu.AddItem(
                    new MenuItem(
                        experienceMenu.Name + "ColorEnemy",
                        Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Enemy")).SetValue(Color.Gray));
                experienceMenu.AddItem(
                    new MenuItem(experienceMenu.Name + "Self", Global.Lang.Get("G_Self")).SetValue(false));
                experienceMenu.AddItem(
                    new MenuItem(experienceMenu.Name + "Ally", Global.Lang.Get("G_Ally")).SetValue(false));
                experienceMenu.AddItem(
                    new MenuItem(experienceMenu.Name + "Enemy", Global.Lang.Get("G_Enemy")).SetValue(false));

                var attackMenu = new Menu(Global.Lang.Get("G_Attack"), Name + "Attack");
                attackMenu.AddItem(
                    new MenuItem(
                        attackMenu.Name + "ColorSelf", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Self"))
                        .SetValue(Color.Yellow));
                attackMenu.AddItem(
                    new MenuItem(
                        attackMenu.Name + "ColorAlly", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Ally"))
                        .SetValue(Color.Yellow));
                attackMenu.AddItem(
                    new MenuItem(
                        attackMenu.Name + "ColorEnemy", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Enemy"))
                        .SetValue(Color.Yellow));
                attackMenu.AddItem(new MenuItem(attackMenu.Name + "Self", Global.Lang.Get("G_Self")).SetValue(false));
                attackMenu.AddItem(new MenuItem(attackMenu.Name + "Ally", Global.Lang.Get("G_Ally")).SetValue(false));
                attackMenu.AddItem(new MenuItem(attackMenu.Name + "Enemy", Global.Lang.Get("G_Enemy")).SetValue(false));

                var turretMenu = new Menu(Global.Lang.Get("G_Turret"), Name + "Turret");
                turretMenu.AddItem(
                    new MenuItem(
                        turretMenu.Name + "ColorAlly", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Ally"))
                        .SetValue(Color.DarkGreen));
                turretMenu.AddItem(
                    new MenuItem(
                        turretMenu.Name + "ColorEnemy", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Enemy"))
                        .SetValue(Color.DarkRed));
                turretMenu.AddItem(new MenuItem(turretMenu.Name + "Ally", Global.Lang.Get("G_Ally")).SetValue(false));
                turretMenu.AddItem(new MenuItem(turretMenu.Name + "Enemy", Global.Lang.Get("G_Enemy")).SetValue(false));

                var shopMenu = new Menu(Global.Lang.Get("G_Shop"), Name + "Shop");
                shopMenu.AddItem(
                    new MenuItem(
                        shopMenu.Name + "ColorAlly", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Ally"))
                        .SetValue(Color.DarkGreen));
                shopMenu.AddItem(
                    new MenuItem(
                        shopMenu.Name + "ColorEnemy", Global.Lang.Get("G_Color") + " " + Global.Lang.Get("G_Enemy"))
                        .SetValue(Color.DarkRed));
                shopMenu.AddItem(new MenuItem(shopMenu.Name + "Ally", Global.Lang.Get("G_Ally")).SetValue(false));
                shopMenu.AddItem(new MenuItem(shopMenu.Name + "Enemy", Global.Lang.Get("G_Enemy")).SetValue(false));

                var spellMenu = new Menu(Global.Lang.Get("G_Spell"), Name + "Spell");
                spellMenu.AddItem(
                    new MenuItem(
                        spellMenu.Name + "MaxRange",
                        Global.Lang.Get("G_Maximum") + " " + Global.Lang.Get("G_Spell") + " " +
                        Global.Lang.Get("G_Range")).SetValue(new Slider(1000, 500, 3000)));

                var spellSelfMenu = new Menu(Global.Lang.Get("G_Self"), spellMenu.Name + "Self");
                spellSelfMenu.AddItem(
                    new MenuItem(spellSelfMenu.Name + "ColorQ", Global.Lang.Get("G_Color") + " Q").SetValue(
                        Color.Purple));
                spellSelfMenu.AddItem(
                    new MenuItem(spellSelfMenu.Name + "ColorW", Global.Lang.Get("G_Color") + " W").SetValue(
                        Color.Purple));
                spellSelfMenu.AddItem(
                    new MenuItem(spellSelfMenu.Name + "ColorE", Global.Lang.Get("G_Color") + " E").SetValue(
                        Color.Purple));
                spellSelfMenu.AddItem(
                    new MenuItem(spellSelfMenu.Name + "ColorR", Global.Lang.Get("G_Color") + " R").SetValue(
                        Color.Purple));
                spellSelfMenu.AddItem(new MenuItem(spellSelfMenu.Name + "Q", "Q").SetValue(false));
                spellSelfMenu.AddItem(new MenuItem(spellSelfMenu.Name + "W", "W").SetValue(false));
                spellSelfMenu.AddItem(new MenuItem(spellSelfMenu.Name + "E", "E").SetValue(false));
                spellSelfMenu.AddItem(new MenuItem(spellSelfMenu.Name + "R", "R").SetValue(false));

                spellMenu.AddSubMenu(spellSelfMenu);

                var spellAllyMenu = new Menu(Global.Lang.Get("G_Ally"), spellMenu.Name + "Ally");
                spellAllyMenu.AddItem(
                    new MenuItem(spellAllyMenu.Name + "ColorQ", Global.Lang.Get("G_Color") + " Q").SetValue(Color.Green));
                spellAllyMenu.AddItem(
                    new MenuItem(spellAllyMenu.Name + "ColorW", Global.Lang.Get("G_Color") + " W").SetValue(Color.Green));
                spellAllyMenu.AddItem(
                    new MenuItem(spellAllyMenu.Name + "ColorE", Global.Lang.Get("G_Color") + " E").SetValue(Color.Green));
                spellAllyMenu.AddItem(
                    new MenuItem(spellAllyMenu.Name + "ColorR", Global.Lang.Get("G_Color") + " R").SetValue(Color.Green));
                spellAllyMenu.AddItem(new MenuItem(spellAllyMenu.Name + "Q", "Q").SetValue(false));
                spellAllyMenu.AddItem(new MenuItem(spellAllyMenu.Name + "W", "W").SetValue(false));
                spellAllyMenu.AddItem(new MenuItem(spellAllyMenu.Name + "E", "E").SetValue(false));
                spellAllyMenu.AddItem(new MenuItem(spellAllyMenu.Name + "R", "R").SetValue(false));

                spellMenu.AddSubMenu(spellAllyMenu);

                var spellEnemyMenu = new Menu(Global.Lang.Get("G_Enemy"), spellMenu.Name + "Enemy");
                spellEnemyMenu.AddItem(
                    new MenuItem(spellEnemyMenu.Name + "ColorQ", Global.Lang.Get("G_Color") + " Q").SetValue(Color.Red));
                spellEnemyMenu.AddItem(
                    new MenuItem(spellEnemyMenu.Name + "ColorW", Global.Lang.Get("G_Color") + " W").SetValue(Color.Red));
                spellEnemyMenu.AddItem(
                    new MenuItem(spellEnemyMenu.Name + "ColorE", Global.Lang.Get("G_Color") + " E").SetValue(Color.Red));
                spellEnemyMenu.AddItem(
                    new MenuItem(spellEnemyMenu.Name + "ColorR", Global.Lang.Get("G_Color") + " R").SetValue(Color.Red));
                spellEnemyMenu.AddItem(new MenuItem(spellEnemyMenu.Name + "Q", "Q").SetValue(false));
                spellEnemyMenu.AddItem(new MenuItem(spellEnemyMenu.Name + "W", "W").SetValue(false));
                spellEnemyMenu.AddItem(new MenuItem(spellEnemyMenu.Name + "E", "E").SetValue(false));
                spellEnemyMenu.AddItem(new MenuItem(spellEnemyMenu.Name + "R", "R").SetValue(false));

                spellMenu.AddSubMenu(spellEnemyMenu);

                Menu.AddSubMenu(drawingMenu);
                Menu.AddSubMenu(experienceMenu);
                Menu.AddSubMenu(attackMenu);
                Menu.AddSubMenu(turretMenu);
                Menu.AddSubMenu(shopMenu);
                Menu.AddSubMenu(spellMenu);

                Menu.AddItem(new MenuItem(Name + "Enabled", Global.Lang.Get("G_Enabled")).SetValue(false));

                Parent.Menu.AddSubMenu(Menu);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
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
    }
}
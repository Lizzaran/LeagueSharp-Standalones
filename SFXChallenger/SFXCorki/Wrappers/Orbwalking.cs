#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Orbwalking.cs is part of SFXCorki.

 SFXCorki is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXCorki is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXCorki. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXCorki.Library;
using SharpDX;
using Color = System.Drawing.Color;
using DamageType = SFXCorki.Enumerations.DamageType;
using MinionManager = SFXCorki.Library.MinionManager;
using TargetSelector = SFXCorki.SFXTargetSelector.TargetSelector;
using Utils = LeagueSharp.Common.Utils;

#endregion

namespace SFXCorki.Wrappers
{
    /// <summary>
    ///     This class offers everything related to auto-attacks and orbwalking.
    /// </summary>
    public static class Orbwalking
    {
        public delegate void AfterAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);

        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);

        public enum OrbwalkingDelay
        {
            Move,
            Attack
        }

        public enum OrbwalkingMode
        {
            Flee,
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            None
        }

        //Spells that reset the attack timer.
        private static readonly string[] AttackResets =
        {
            "dariusnoxiantacticsonh", "fioraflurry", "garenq",
            "hecarimrapidslash", "jaxempowertwo", "jaycehypercharge", "leonashieldofdaybreak", "luciane", "lucianq",
            "monkeykingdoubleattack", "mordekaisermaceofspades", "nasusq", "nautiluspiercinggaze", "netherblade",
            "parley", "poppydevastatingblow", "powerfist", "renektonpreexecute", "rengarq", "shyvanadoubleattack",
            "sivirw", "takedown", "talonnoxiandiplomacy", "trundletrollsmash", "vaynetumble", "vie", "volibearq",
            "xenzhaocombotarget", "yorickspectral", "reksaiq", "itemtitanichydracleave"
        };

        //Spells that are not attacks even if they have the "attack" word in their name.
        private static readonly string[] NoAttacks =
        {
            "jarvanivcataclysmattack", "monkeykingdoubleattack",
            "shyvanadoubleattack", "shyvanadoubleattackdragon", "zyragraspingplantattack", "zyragraspingplantattack2",
            "zyragraspingplantattackfire", "zyragraspingplantattack2fire", "viktorpowertransfer", "sivirwattackbounce"
        };

        //Spells that are attacks even if they dont have the "attack" word in their name.
        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced", "renektonexecute",
            "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2",
            "xenzhaothrust3", "viktorqbuff"
        };

        // Champs whose auto attacks can't be cancelled
        private static readonly string[] NoCancelChamps = { "Kalista" };
        public static int LastAaTick;
        public static bool Attack = true;
        public static bool DisableNextAttack;
        public static bool Move = true;
        public static int LastMoveCommandT;
        public static Vector3 LastMoveCommandPosition = Vector3.Zero;
        private static AttackableUnit _lastTarget;
        private static readonly Obj_AI_Hero Player;
        private static float _minDistance = 400;
        private static bool _missileLaunched;
        private static readonly Random Random = new Random(DateTime.Now.Millisecond);
        private static bool _preventStuttering;
        private static readonly Dictionary<OrbwalkingDelay, Delay> Delays = new Dictionary<OrbwalkingDelay, Delay>();

        static Orbwalking()
        {
            Player = ObjectManager.Player;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            GameObject.OnCreate += MissileClient_OnCreate;
            Spellbook.OnStopCast += SpellbookOnStopCast;
        }

        /// <summary>
        ///     This event is fired before the player auto attacks.
        /// </summary>
        public static event BeforeAttackEvenH BeforeAttack;

        /// <summary>
        ///     This event is fired when a unit is about to auto-attack another unit.
        /// </summary>
        public static event OnAttackEvenH OnAttack;

        /// <summary>
        ///     This event is fired after a unit finishes auto-attacking another unit (Only works with player for now).
        /// </summary>
        public static event AfterAttackEvenH AfterAttack;

        /// <summary>
        ///     Gets called on target changes
        /// </summary>
        public static event OnTargetChangeH OnTargetChange;

        //  <summary>
        //      Gets called if you can't kill a minion with auto attacks
        //  </summary>
        public static event OnNonKillableMinionH OnNonKillableMinion;

        private static void FireBeforeAttack(AttackableUnit target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            }
            else
            {
                DisableNextAttack = false;
            }
        }

        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (OnAttack != null)
            {
                OnAttack(unit, target);
            }
        }

        private static void FireAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (AfterAttack != null && target.IsValidTarget())
            {
                AfterAttack(unit, target);
            }
        }

        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget() || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            if (OnNonKillableMinion != null)
            {
                OnNonKillableMinion(minion);
            }
        }

        /// <summary>
        ///     Returns true if the spellname resets the attack timer.
        /// </summary>
        public static bool IsAutoAttackReset(string name)
        {
            return AttackResets.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns true if the unit is melee
        /// </summary>
        public static bool IsMelee(this Obj_AI_Base unit)
        {
            return unit.CombatType == GameObjectCombatType.Melee || Player.IsMelee;
        }

        /// <summary>
        ///     Returns true if the spellname is an auto-attack.
        /// </summary>
        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower())) ||
                   Attacks.Contains(name.ToLower());
        }

        /// <summary>
        ///     Returns the auto-attack range.
        /// </summary>
        public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = Player.AttackRange + Player.BoundingRadius;
            if (target.IsValidTarget())
            {
                result += target.BoundingRadius;
            }
            if (_preventStuttering)
            {
                var hero = target as Obj_AI_Hero;
                if (hero != null && !hero.IsFacing(Player))
                {
                    result -= 10;
                }
            }
            return result;
        }

        public static void PreventStuttering(bool val)
        {
            _preventStuttering = val;
        }

        /// <summary>
        ///     Returns true if the target is in auto-attack range.
        /// </summary>
        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);
            return
                Vector2.DistanceSquared(
                    (target is Obj_AI_Base) ? ((Obj_AI_Base) target).ServerPosition.To2D() : target.Position.To2D(),
                    Player.ServerPosition.To2D()) <= myRange * myRange;
        }

        /// <summary>
        ///     Returns player auto-attack missile speed.
        /// </summary>
        public static float GetMyProjectileSpeed()
        {
            return IsMelee(Player) || Player.ChampionName == "Azir" ? float.MaxValue : Player.BasicAttack.MissileSpeed;
        }

        /// <summary>
        ///     Returns if the player's auto-attack is ready.
        /// </summary>
        public static bool CanAttack(float extraDelay = 0f)
        {
            return Utils.GameTimeTickCount + Game.Ping / 2 + 25 >= LastAaTick + Player.AttackDelay * 1000 + extraDelay &&
                   Attack;
        }

        /// <summary>
        ///     Returns true if moving won't cancel the auto-attack.
        /// </summary>
        public static bool CanMove(float extraWindup)
        {
            if (!Move)
            {
                return false;
            }

            if (_missileLaunched && Orbwalker.MissileCheck)
            {
                return true;
            }

            return NoCancelChamps.Contains(Player.ChampionName) ||
                   (Utils.GameTimeTickCount + Game.Ping / 2 >= LastAaTick + Player.AttackCastDelay * 1000 + extraWindup);
        }

        public static void SetDelay(float value, OrbwalkingDelay delay)
        {
            Delay delayEntry;
            if (Delays.TryGetValue(delay, out delayEntry))
            {
                delayEntry.Default = value;
            }
            else
            {
                Delays[delay] = new Delay { Default = value };
            }
        }

        public static void SetMinDelay(float value, OrbwalkingDelay delay)
        {
            Delay delayEntry;
            if (Delays.TryGetValue(delay, out delayEntry))
            {
                delayEntry.MinDelay = value;
            }
            else
            {
                Delays[delay] = new Delay { MinDelay = value };
            }
        }

        public static void SetMaxDelay(float value, OrbwalkingDelay delay)
        {
            Delay delayEntry;
            if (Delays.TryGetValue(delay, out delayEntry))
            {
                delayEntry.MaxDelay = value;
            }
            else
            {
                Delays[delay] = new Delay { MaxDelay = value };
            }
        }

        public static void SetDelayFrequency(float value, OrbwalkingDelay delay)
        {
            Delay delayEntry;
            if (Delays.TryGetValue(delay, out delayEntry))
            {
                delayEntry.Frequency = value;
            }
            else
            {
                Delays[delay] = new Delay { Frequency = value };
            }
        }

        public static void SetDelayRandomize(bool value, OrbwalkingDelay delay)
        {
            Delay delayEntry;
            if (Delays.TryGetValue(delay, out delayEntry))
            {
                delayEntry.Randomize = value;
            }
            else
            {
                Delays[delay] = new Delay { Randomize = value };
            }
        }

        private static void SetCurrentDelay(Delay delay)
        {
            if (delay.Randomize && Random.Next(0, 101) >= (100 - delay.Frequency))
            {
                var min = (delay.Default / 100f) * delay.MinDelay;
                var max = (delay.Default / 100f) * delay.MaxDelay;
                delay.CurrentDelay = Random.Next((int) Math.Min(min, max), (int) Math.Max(min, max) + 1);
            }
            else
            {
                delay.CurrentDelay = delay.Default;
            }
        }

        public static void SetMinimumOrbwalkDistance(float d)
        {
            _minDistance = d;
        }

        public static float GetLastMoveTime()
        {
            return LastMoveCommandT;
        }

        public static Vector3 GetLastMovePosition()
        {
            return LastMoveCommandPosition;
        }

        public static void MoveTo(Vector3 position,
            float holdAreaRadius = 0,
            bool overrideTimer = false,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            var delay = Delays[OrbwalkingDelay.Move];

            if (Utils.GameTimeTickCount - LastMoveCommandT < delay.CurrentDelay && !overrideTimer)
            {
                return;
            }

            SetCurrentDelay(delay);

            LastMoveCommandT = Utils.GameTimeTickCount;

            var playerPosition = Player.ServerPosition;

            if (playerPosition.Distance(position, true) < holdAreaRadius * holdAreaRadius)
            {
                if (Player.Path.Length > 0)
                {
                    Player.IssueOrder(GameObjectOrder.Stop, playerPosition);
                    LastMoveCommandPosition = playerPosition;
                }
                return;
            }

            var point = position;
            if (useFixedDistance)
            {
                point = playerPosition.Extend(
                    position, (randomizeMinDistance ? (Random.NextFloat(0.6f, 1) + 0.2f) * _minDistance : _minDistance));
            }
            else
            {
                if (randomizeMinDistance)
                {
                    point = playerPosition.Extend(position, (Random.NextFloat(0.6f, 1) + 0.2f) * _minDistance);
                }
                else if (playerPosition.Distance(position) > _minDistance)
                {
                    point = playerPosition.Extend(position, _minDistance);
                }
            }
            Player.IssueOrder(GameObjectOrder.MoveTo, point);
            LastMoveCommandPosition = point;
        }

        /// <summary>
        ///     Orbwalk a target while moving to Position.
        /// </summary>
        public static void Orbwalk(AttackableUnit target,
            Vector3 position,
            float extraWindup = 90,
            float holdAreaRadius = 0,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true)
        {
            try
            {
                var delay = Delays[OrbwalkingDelay.Attack];
                if (target.IsValidTarget() && CanAttack(delay.CurrentDelay))
                {
                    SetCurrentDelay(delay);
                    DisableNextAttack = false;
                    FireBeforeAttack(target);

                    if (!DisableNextAttack)
                    {
                        if (!NoCancelChamps.Contains(Player.ChampionName))
                        {
                            LastAaTick = Utils.GameTimeTickCount + Game.Ping + 100 -
                                         (int) (ObjectManager.Player.AttackCastDelay * 1000f);
                            _missileLaunched = false;

                            if (!IsMelee(Player))
                            {
                                var d = GetRealAutoAttackRange(target) - 65;
                                if (Player.Distance(target, true) > d * d)
                                {
                                    LastAaTick += 300;
                                }
                            }
                        }

                        Player.IssueOrder(GameObjectOrder.AttackUnit, target);

                        _lastTarget = target;
                        return;
                    }
                }
                if (CanMove(extraWindup))
                {
                    MoveTo(position, holdAreaRadius, false, useFixedDistance, randomizeMinDistance);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        ///     Resets the Auto-Attack timer.
        /// </summary>
        public static void ResetAutoAttackTimer()
        {
            LastAaTick = 0;
        }

        private static void SpellbookOnStopCast(Spellbook spellbook, SpellbookStopCastEventArgs args)
        {
            if (spellbook.Owner.IsValid && spellbook.Owner.IsMe && args.DestroyMissile && args.StopAnimation)
            {
                ResetAutoAttackTimer();
            }
        }

        private static void MissileClient_OnCreate(GameObject sender, EventArgs args)
        {
            var missile = sender as MissileClient;
            if (missile != null && missile.SpellCaster.IsMe && IsAutoAttack(missile.SData.Name))
            {
                _missileLaunched = true;
            }
        }

        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs spell)
        {
            try
            {
                var spellName = spell.SData.Name;

                if (IsAutoAttackReset(spellName) && unit.IsMe)
                {
                    Utility.DelayAction.Add(250, ResetAutoAttackTimer);
                }

                if (!IsAutoAttack(spellName))
                {
                    return;
                }

                if (unit.IsMe &&
                    (spell.Target is Obj_AI_Base || spell.Target is Obj_BarracksDampener || spell.Target is Obj_HQ))
                {
                    LastAaTick = Utils.GameTimeTickCount - Game.Ping / 2;
                    _missileLaunched = false;

                    var target = spell.Target as Obj_AI_Base;
                    if (target != null)
                    {
                        if (target.IsValid)
                        {
                            FireOnTargetSwitch(target);
                            _lastTarget = target;
                        }

                        //Trigger it for ranged until the missiles catch normal attacks again!
                        Utility.DelayAction.Add(
                            (int) (unit.AttackCastDelay * 1000 + 40), () => FireAfterAttack(unit, _lastTarget));
                    }
                }

                FireOnAttack(unit, _lastTarget);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public class BeforeAttackEventArgs
        {
            private bool _process = true;
            public AttackableUnit Target;
            public Obj_AI_Base Unit = ObjectManager.Player;

            public bool Process
            {
                get { return _process; }
                set
                {
                    DisableNextAttack = !value;
                    _process = value;
                }
            }
        }

        /// <summary>
        ///     This class allows you to add an instance of "Orbwalker" to your assembly in order to control the orbwalking in an
        ///     easy way.
        /// </summary>
        public class Orbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private Obj_AI_Base _forcedTarget;
            private OrbwalkingMode _mode = OrbwalkingMode.None;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;

            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;
                /* Drawings submenu */
                var drawings = new Menu("Drawings", "drawings");
                drawings.AddItem(
                    new MenuItem("AACircle", "AACircle").SetShared()
                        .SetValue(new Circle(true, Color.FromArgb(255, 255, 0, 255))));
                drawings.AddItem(
                    new MenuItem("AACircle2", "Enemy AA circle").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(255, 255, 0, 255))));
                drawings.AddItem(
                    new MenuItem("HoldZone", "HoldZone").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(255, 255, 0, 255))));
                _config.AddSubMenu(drawings);

                /* Misc options */
                var misc = new Menu("Misc", "Misc");
                misc.AddItem(
                    new MenuItem("HoldPosRadius", "Hold Position Radius").SetShared().SetValue(new Slider(0, 0, 250)));
                misc.AddItem(new MenuItem("PriorizeFarm", "Priorize farm over harass").SetShared().SetValue(true));

                _config.AddSubMenu(misc);

                var delays = new Menu("Delays", "Delays");
                delays.AddItem(new MenuItem("ExtraWindup", "Windup Delay").SetShared().SetValue(new Slider(80, 0, 200)));
                delays.AddItem(new MenuItem("FarmDelay", "Farm Delay").SetShared().SetValue(new Slider(0, 0, 200)));

                var delayMovement = new Menu("Movement", "Movement");
                delayMovement.AddItem(
                    new MenuItem("MovementDelay", "Delay").SetShared().SetValue(new Slider(25, 0, 250))).ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Move);
                    };
                delayMovement.AddItem(
                    new MenuItem("MovementMinDelay", "Min. % Delay").SetShared().SetValue(new Slider(170, 100, 300)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetMinDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Move);
                    };
                delayMovement.AddItem(
                    new MenuItem("MovementMaxDelay", "Max. % Delay").SetShared().SetValue(new Slider(220, 100, 300)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetMaxDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Move);
                    };
                delayMovement.AddItem(
                    new MenuItem("MovementFrequency", "Frequency %").SetShared().SetValue(new Slider(30))).ValueChanged
                    +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelayFrequency(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Move);
                    };
                delayMovement.AddItem(new MenuItem("MovementRandomize", "Randomize").SetShared().SetValue(false))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelayRandomize(args.GetNewValue<bool>(), OrbwalkingDelay.Move);
                    };

                var delayAttack = new Menu("Attack", "Attack");
                delayAttack.AddItem(new MenuItem("AttackDelay", "Delay").SetShared().SetValue(new Slider(25, 0, 250)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Attack);
                    };
                delayAttack.AddItem(
                    new MenuItem("AttackMinDelay", "Min. % Delay").SetShared().SetValue(new Slider(170, 100, 300)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetMinDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Attack);
                    };
                delayAttack.AddItem(
                    new MenuItem("AttackMaxDelay", "Max. % Delay").SetShared().SetValue(new Slider(220, 100, 300)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetMaxDelay(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Attack);
                    };
                delayAttack.AddItem(new MenuItem("AttackFrequency", "Frequency %").SetShared().SetValue(new Slider(30)))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelayFrequency(args.GetNewValue<Slider>().Value, OrbwalkingDelay.Attack);
                    };
                delayAttack.AddItem(new MenuItem("AttackRandomize", "Randomize").SetShared().SetValue(false))
                    .ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        SetDelayRandomize(args.GetNewValue<bool>(), OrbwalkingDelay.Attack);
                    };

                delays.AddSubMenu(delayMovement);
                delays.AddSubMenu(delayAttack);

                _config.AddSubMenu(delays);

                var attackables = new Menu("Attackable Objects", "Attackables");
                attackables.AddItem(new MenuItem("AttackWard", "Ward").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackZyra", "Zyra Plant").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackHeimer", "Heimer Turret").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackShaco", "Shaco Box").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackTeemo", "Teemo Shroom").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackGangplank", "Gangplank Barrel").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackAnnie", "Annie Tibbers").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackYorick", "Yorick Ghost").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackMordekaiser", "Mordekaiser Ghost").SetShared().SetValue(true));
                attackables.AddItem(new MenuItem("AttackClone", "Clones").SetShared().SetValue(true));

                _config.AddSubMenu(attackables);

                /* Missile check */
                _config.AddItem(new MenuItem("MissileCheck", "Missile Check").SetShared().SetValue(true));

                /*Load the menu*/
                _config.AddItem(new MenuItem("Flee", "Flee").SetShared().SetValue(new KeyBind('G', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("LastHit", "Last hit").SetShared().SetValue(new KeyBind('X', KeyBindType.Press)));

                _config.AddItem(new MenuItem("Farm", "Mixed").SetShared().SetValue(new KeyBind('C', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("LaneClear", "LaneClear").SetShared().SetValue(new KeyBind('V', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("Orbwalk", "Combo").SetShared().SetValue(new KeyBind(32, KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("Orbwalk2", "Combo Alternate").SetShared().SetValue(new KeyBind(32, KeyBindType.Press)));

                SetDelay(_config.Item("MovementDelay").GetValue<Slider>().Value, OrbwalkingDelay.Move);
                SetMinDelay(_config.Item("MovementMinDelay").GetValue<Slider>().Value, OrbwalkingDelay.Move);
                SetMaxDelay(_config.Item("MovementMaxDelay").GetValue<Slider>().Value, OrbwalkingDelay.Move);
                SetDelayFrequency(_config.Item("MovementFrequency").GetValue<Slider>().Value, OrbwalkingDelay.Move);
                SetDelayRandomize(_config.Item("MovementRandomize").GetValue<bool>(), OrbwalkingDelay.Move);

                SetDelay(_config.Item("AttackDelay").GetValue<Slider>().Value, OrbwalkingDelay.Attack);
                SetMinDelay(_config.Item("AttackMinDelay").GetValue<Slider>().Value, OrbwalkingDelay.Attack);
                SetMaxDelay(_config.Item("AttackMaxDelay").GetValue<Slider>().Value, OrbwalkingDelay.Attack);
                SetDelayFrequency(_config.Item("AttackFrequency").GetValue<Slider>().Value, OrbwalkingDelay.Attack);
                SetDelayRandomize(_config.Item("AttackRandomize").GetValue<bool>(), OrbwalkingDelay.Attack);

                Game.OnUpdate += GameOnOnGameUpdate;
                Drawing.OnDraw += DrawingOnOnDraw;
            }

            private int FarmDelay
            {
                get { return _config.Item("FarmDelay").GetValue<Slider>().Value; }
            }

            public static bool MissileCheck
            {
                get { return _config.Item("MissileCheck").GetValue<bool>(); }
            }

            public int HoldAreaRadius
            {
                get { return _config.Item("HoldPosRadius").GetValue<Slider>().Value; }
            }

            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (_mode != OrbwalkingMode.None)
                    {
                        return _mode;
                    }

                    if (_config.Item("Orbwalk").GetValue<KeyBind>().Active ||
                        _config.Item("Orbwalk2").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Combo;
                    }

                    if (_config.Item("LaneClear").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (_config.Item("Farm").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (_config.Item("LastHit").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LastHit;
                    }

                    if (_config.Item("Flee").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Flee;
                    }

                    return OrbwalkingMode.None;
                }
                set { _mode = value; }
            }

            /// <summary>
            ///     Enables or disables the auto-attacks.
            /// </summary>
            public void SetAttack(bool b)
            {
                Attack = b;
            }

            /// <summary>
            ///     Enables or disables the movement.
            /// </summary>
            public void SetMovement(bool b)
            {
                Move = b;
            }

            /// <summary>
            ///     Forces the orbwalker to attack the set target if valid and in range.
            /// </summary>
            public void ForceTarget(Obj_AI_Base target)
            {
                _forcedTarget = target;
            }

            public Obj_AI_Base ForcedTarget()
            {
                return _forcedTarget;
            }

            /// <summary>
            ///     Forces the orbwalker to move to that point while orbwalking (Game.CursorPos by default).
            /// </summary>
            public void SetOrbwalkingPoint(Vector3 point)
            {
                _orbwalkingPoint = point;
            }

            private bool ShouldWait()
            {
                return
                    MinionManager.GetMinions(Player.Position, float.MaxValue)
                        .Any(
                            minion =>
                                InAutoAttackRange(minion) &&
                                HealthPrediction.LaneClearHealthPrediction(
                                    minion, (int) ((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay) <=
                                Player.GetAutoAttackDamage(minion));
            }

            public virtual AttackableUnit GetTarget()
            {
                AttackableUnit result = null;

                if ((ActiveMode == OrbwalkingMode.Mixed || ActiveMode == OrbwalkingMode.LaneClear) &&
                    !_config.Item("PriorizeFarm").GetValue<bool>())
                {
                    var target = TargetSelector.GetTarget(-1, DamageType.Physical);
                    if (target != null)
                    {
                        return target;
                    }
                }

                //Forced target
                if (_forcedTarget.IsValidTarget() && InAutoAttackRange(_forcedTarget))
                {
                    return _forcedTarget;
                }

                var minions = new List<Obj_AI_Minion>();
                if (ActiveMode != OrbwalkingMode.None && ActiveMode != OrbwalkingMode.Flee)
                {
                    minions = GetMinions(
                        _config.Item("AttackWard").GetValue<bool>(), _config.Item("AttackZyra").GetValue<bool>(),
                        _config.Item("AttackHeimer").GetValue<bool>(), _config.Item("AttackClone").GetValue<bool>(),
                        _config.Item("AttackAnnie").GetValue<bool>(), _config.Item("AttackTeemo").GetValue<bool>(),
                        _config.Item("AttackShaco").GetValue<bool>(), _config.Item("AttackGangplank").GetValue<bool>(),
                        _config.Item("AttackYorick").GetValue<bool>(),
                        _config.Item("AttackMordekaiser").GetValue<bool>());
                }

                /*Killable Minion*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed ||
                    ActiveMode == OrbwalkingMode.LastHit)
                {
                    var minionList =
                        minions.OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                            .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                            .ThenByDescending(minion => minion.MaxHealth)
                            .ThenBy(minion => minion.Health);

                    foreach (var minion in minionList)
                    {
                        var t = (int) (Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2 +
                                1000 * (int) Math.Max(0, Player.Distance(minion) - Player.BoundingRadius) /
                                (int) GetMyProjectileSpeed();

                        if (minion.MaxHealth <= 10)
                        {
                            if (minion.Health <= 1)
                            {
                                return minion;
                            }
                        }
                        else
                        {
                            var predHealth = HealthPrediction.GetHealthPrediction(minion, t, FarmDelay);
                            if (predHealth <= 0)
                            {
                                FireOnNonKillableMinion(minion);
                            }

                            if (predHealth > 0 && predHealth <= Player.GetAutoAttackDamage(minion, true))
                            {
                                return minion;
                            }
                        }
                    }
                }

                /* turrets / inhibitors / nexus */
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    /* turrets */
                    foreach (var turret in
                        GameObjects.EnemyTurrets.Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* inhibitor */
                    foreach (var turret in
                        GameObjects.EnemyInhibitors.Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* nexus */
                    if (GameObjects.EnemyNexus != null && GameObjects.EnemyNexus.IsValidTarget() &&
                        InAutoAttackRange(GameObjects.EnemyNexus))
                    {
                        return GameObjects.EnemyNexus;
                    }
                }

                /*Champions*/
                if (ActiveMode != OrbwalkingMode.LastHit)
                {
                    var target = TargetSelector.GetTarget(-1, DamageType.Physical);
                    if (target.IsValidTarget())
                    {
                        return target;
                    }
                }

                /*Lane Clear minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    if (!ShouldWait())
                    {
                        if (_prevMinion.IsValidTarget() && InAutoAttackRange(_prevMinion))
                        {
                            if (_prevMinion.MaxHealth <= 10)
                            {
                                return _prevMinion;
                            }
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                _prevMinion, (int) ((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay);
                            if (predHealth >= 2 * Player.GetAutoAttackDamage(_prevMinion) ||
                                Math.Abs(predHealth - _prevMinion.Health) < float.Epsilon)
                            {
                                return _prevMinion;
                            }
                        }

                        foreach (var minion in minions.Where(m => m.Team != GameObjectTeam.Neutral))
                        {
                            if (minion.MaxHealth <= 10)
                            {
                                result = minion;
                            }
                            else
                            {
                                var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                    minion, (int) ((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), FarmDelay);
                                if (predHealth >= 2 * Player.GetAutoAttackDamage(minion) ||
                                    Math.Abs(predHealth - minion.Health) < float.Epsilon)
                                {
                                    if (result == null || minion.Health > result.Health && result.MaxHealth > 10)
                                    {
                                        result = minion;
                                    }
                                }
                            }
                        }

                        if (result != null)
                        {
                            _prevMinion = (Obj_AI_Minion) result;
                        }
                    }
                }

                /*Jungle minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed)
                {
                    result = minions.Where(m => m.Team == GameObjectTeam.Neutral).MaxOrDefault(mob => mob.MaxHealth);
                    if (result != null)
                    {
                        return result;
                    }
                }

                if (result == null && ActiveMode == OrbwalkingMode.Combo)
                {
                    if (
                        !GameObjects.EnemyHeroes.Any(
                            e => e.IsValidTarget() && e.Distance(Player) < GetRealAutoAttackRange(e) * 1.2f))
                    {
                        return minions.FirstOrDefault(m => !MinionManager.IsMinion(m, true));
                    }
                }

                return result;
            }

            private List<Obj_AI_Minion> GetMinions(bool ward,
                bool zyra,
                bool heimer,
                bool clone,
                bool annie,
                bool teemo,
                bool shaco,
                bool gangplank,
                bool yorick,
                bool mordekaiser)
            {
                var targets = new List<Obj_AI_Minion>();
                var minions = new List<Obj_AI_Minion>();
                var clones = new List<Obj_AI_Minion>();

                var units = ward ? GameObjects.EnemyMinions.Concat(GameObjects.EnemyWards) : GameObjects.EnemyMinions;
                foreach (var minion in units.Where(u => u.IsValidTarget() && InAutoAttackRange(u)))
                {
                    var baseName = minion.CharData.BaseSkinName.ToLower();
                    if (MinionManager.IsMinion(minion, ward)) //minions & wards
                    {
                        minions.Add(minion);
                        continue;
                    }
                    if (zyra) //zyra plant
                    {
                        if (baseName.Contains("zyra") && baseName.Contains("plant"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (heimer) //heimer turret
                    {
                        if (baseName.Contains("heimert"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (annie) //annie tibber
                    {
                        if (baseName.Contains("annietibbers"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (teemo) //teemo shroom
                    {
                        if (baseName.Contains("teemomushroom"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (shaco) //shaco box
                    {
                        if (baseName.Contains("shacobox"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (gangplank) //gangplank barrel
                    {
                        if (baseName.Contains("gangplankbarrel"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (yorick) //yorick ghouls
                    {
                        if (baseName.Contains("yorick") && baseName.Contains("ghoul"))
                        {
                            targets.Add(minion);
                            continue;
                        }
                    }
                    if (clone) //clones
                    {
                        if (baseName.Contains("shaco") || baseName.Contains("leblanc") ||
                            baseName.Contains("monkeyking"))
                        {
                            clones.Add(minion);
                            continue;
                        }
                    }
                    if (mordekaiser) //Mordekaiser Ghost
                    {
                        if (GameObjects.AllyHeroes.Any(e => e.CharData.BaseSkinName.ToLower().Equals(baseName)))
                        {
                            targets.Add(minion);
                        }
                    }
                }
                return
                    targets.Concat(minions)
                        .Concat(GameObjects.Jungle.Where(u => u.IsValidTarget() && InAutoAttackRange(u)))
                        .Concat(clones)
                        .ToList();
            }

            private void GameOnOnGameUpdate(EventArgs args)
            {
                try
                {
                    if (ActiveMode == OrbwalkingMode.None || ActiveMode == OrbwalkingMode.Flee)
                    {
                        return;
                    }

                    //Prevent canceling important spells
                    if (Player.IsCastingInterruptableSpell(true))
                    {
                        return;
                    }

                    var target = GetTarget();
                    Orbwalk(
                        target, (_orbwalkingPoint.To2D().IsValid()) ? _orbwalkingPoint : Game.CursorPos,
                        _config.Item("ExtraWindup").GetValue<Slider>().Value,
                        _config.Item("HoldPosRadius").GetValue<Slider>().Value);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            private void DrawingOnOnDraw(EventArgs args)
            {
                if (_config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position, GetRealAutoAttackRange(null) + 65,
                        _config.Item("AACircle").GetValue<Circle>().Color);
                }

                if (_config.Item("AACircle2").GetValue<Circle>().Active)
                {
                    foreach (var target in
                        HeroManager.Enemies.FindAll(target => target.IsValidTarget(1175)))
                    {
                        Render.Circle.DrawCircle(
                            target.Position, GetRealAutoAttackRange(target) + 65,
                            _config.Item("AACircle2").GetValue<Circle>().Color);
                    }
                }

                if (_config.Item("HoldZone").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position, _config.Item("HoldPosRadius").GetValue<Slider>().Value,
                        _config.Item("HoldZone").GetValue<Circle>().Color, 5, true);
                }
            }
        }
    }

    internal class Delay
    {
        public float Default { get; set; }
        public float MinDelay { get; set; }
        public float MaxDelay { get; set; }
        public float Frequency { get; set; }
        public bool Randomize { get; set; }
        public float CurrentDelay { get; set; }
    }
}
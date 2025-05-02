using Modding;
using System;
using JetBrains.Annotations;
using UnityEngine;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Globalization;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections;

namespace HkSpeedUp
{
    [UsedImplicitly]
    public class HkSpeedUp : Mod, IMenuMod
    {
        public override string GetVersion() => "1.0.2";

        /* Global settings */
        private KeyCode bindUp = KeyCode.Alpha9;
        private KeyCode bindDown = KeyCode.Alpha8;

        private bool globalSwitch = true;
        private bool lockSwitch = false;
        private int formatType = 0;
        private void ChangeGlobalSwitchState(bool state)
        {
            globalSwitch = state;
            if (!state)
                SpeedMultiplier = 1;
        }

        /* Handle timescale changes */
        private float _speedMultiplier = 1;
        public float SpeedMultiplier
        {
            get
            {
                return globalSwitch ? _speedMultiplier : 1;
            }
            set
            {
                if (value > 0)
                {
                    if (Time.timeScale != 0)
                    {
                        Time.timeScale = value;
                    }
                    _speedMultiplier = value;
                }
            }
        }

        /* Create menu */
        bool IMenuMod.ToggleButtonInsideMenu => true;

        public bool ToggleButtonInsideMenu => true;

        List<IMenuMod.MenuEntry> IMenuMod.GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return new List<IMenuMod.MenuEntry>
        {
            new IMenuMod.MenuEntry
            {
                Name = "Global Switch",
                Description = "turn mod on/off",
                Values = new string[] {
                    "On",
                    "Off"
                },
                Saver = opt => ChangeGlobalSwitchState(opt == 0 ? true : false),
                Loader = () => this.globalSwitch switch
                {
                    false => 1,
                    true => 0
                }
            },
            new IMenuMod.MenuEntry
            {
                Name = "Lock Switch",
                Description = "lock changing your speed",
                Values = new string[] {
                    "On",
                    "Off"
                },
                Saver = opt => this.lockSwitch = opt == 0 ? true : false,
                Loader = () => this.lockSwitch switch
                {
                    false => 1,
                    true => 0
                }
            },
            new IMenuMod.MenuEntry
            {
                Name = "Format Type",
                Description = "change how speed will be formated",
                Values = new string[] {
                    "#.##",
                    "%"
                },
                Saver = opt => this.formatType = opt,
                Loader = () => this.formatType
            },
            new IMenuMod.MenuEntry
            {
                Name = "Slow Game Down (-0.05)",
                Description = "not, changeable (for now)",
                Values = new string[] {
                    "8"
                },
                Saver = opt => this.bindDown = KeyCode.Alpha8,
                Loader = () => 0
            },
            new IMenuMod.MenuEntry
            {
                Name = "Speed Game Up (+0.05)",
                Description = "not, changeable (for now)",
                Values = new string[] {
                    "9"
                },
                Saver = opt => this.bindUp = KeyCode.Alpha9,
                Loader = () => 0
            }
            };
        }

        /* Handle freeze frames */
        private ILHook[] _coroutineHooks;

        private static readonly MethodInfo[] FreezeCoroutines = (
            from method in typeof(GameManager).GetMethods()
            where method.Name.StartsWith("FreezeMoment")
            where method.ReturnType == typeof(IEnumerator)
            select method.GetCustomAttribute<IteratorStateMachineAttribute>() into attr
            select attr.StateMachineType into type
            select type.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)
        ).ToArray();

        private void ScaleFreeze(ILContext il)
        {
            var cursor = new ILCursor(il);

            cursor.GotoNext
            (
                MoveType.After,
                x => x.MatchLdfld(out _),
                x => x.MatchCall<Time>("get_unscaledDeltaTime")
            );

            cursor.EmitDelegate<Func<float>>(() => _speedMultiplier);

            cursor.Emit(OpCodes.Mul);
        }

        /* Set up the hooks */
        public override void Initialize()
        {
            Time.timeScale = SpeedMultiplier;

            ModHooks.HeroUpdateHook += Update;

            _coroutineHooks = new ILHook[FreezeCoroutines.Length];
            foreach ((MethodInfo coro, int idx) in FreezeCoroutines.Select((mi, idx) => (mi, idx)))
            {
                _coroutineHooks[idx] = new ILHook(coro, ScaleFreeze);

                LogDebug($"Hooked {coro.DeclaringType?.Name}!");
            }

            ModDisplay.Instance = new ModDisplay();
        }

        private void Update()
        {
            if (!globalSwitch)
            {
                SpeedMultiplier = 1;
                return;
            }

            ModDisplay.Instance.Display("Game Speed: " + (formatType == 0 ?
                SpeedMultiplier.ToString(SpeedMultiplier >= 10f ? "00.00" : "0.00", CultureInfo.InvariantCulture) :
                (Math.Round(SpeedMultiplier*100)).ToString("0.##\\%")));
            
            ModDisplay.Instance.Update();

            SpeedMultiplier = SpeedMultiplier;

            if (lockSwitch) return;

            if (Input.GetKeyDown(bindUp))
                SpeedMultiplier += 0.05f;

            else if (Input.GetKeyDown(bindDown))
                SpeedMultiplier -= 0.05f;
        }
    }
}
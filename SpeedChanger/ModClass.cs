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

namespace SpeedChanger
{
    public class GlobalSettings
    {
        public bool lockSwitch = false;

        public int displayStyle = 0;

        public string speedUpKeybind = "Alpha9";
        public string slowDownKeybind = "Alpha8";

        public float step = 0.05f;
        private float _speed = 1.00f;
        public float speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = (float)Math.Round(value, 2);
            }
        }
    }

    [UsedImplicitly]
    public class SpeedChanger : Mod, IMenuMod, IGlobalSettings<GlobalSettings>
    {
        public override string GetVersion() => "1.0.3";

        /* Global settings */
        public static GlobalSettings GS { get; set; } = new GlobalSettings();

        public void OnLoadGlobal(GlobalSettings s)
        {
            GS = s;
        }

        public GlobalSettings OnSaveGlobal()
        {
            return GS;
        }
        private KeyCode _speedUpKeybind = (KeyCode) System.Enum.Parse(typeof(KeyCode), GS.speedUpKeybind, true);
        private KeyCode _slowDownKeybind = (KeyCode)System.Enum.Parse(typeof(KeyCode), GS.slowDownKeybind, true);

        private bool _globalSwitch = true;
        private void ChangeGlobalSwitchState(bool state)
        {
            _globalSwitch = state;
            if (!state)
                Unload();
            else
                Initialize();
        }

        /* Handle timescale changes */
        public float SpeedMultiplier
        {
            get
            {
                return _globalSwitch ? GS.speed : 1;
            }
            set
            {
                if (value > 0)
                {
                    if (Time.timeScale != 0)
                    {
                        Time.timeScale = value;
                    }
                    GS.speed = value;
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
                    Description = "Turn mod On/Off",
                    Values = new string[] {
                        "On",
                        "Off"
                    },
                    Saver = opt => ChangeGlobalSwitchState(opt == 0 ? true : false),
                    Loader = () => this._globalSwitch switch
                    {
                        false => 1,
                        true => 0
                    }
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Lock Switch",
                    Description = "Lock changing your speed",
                    Values = new string[] {
                        "On",
                        "Off"
                    },
                    Saver = opt => GS.lockSwitch = opt == 0 ? true : false,
                    Loader = () => GS.lockSwitch switch
                    {
                        false => 1,
                        true => 0
                    }
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Display Style",
                    Description = "Change how speed is displayed",
                    Values = new string[] {
                        "#.##",
                        "%",
                        "Off"
                    },
                    Saver = opt => {
                        GS.displayStyle = opt;
                        if (opt == 2)
                        {
                            ModDisplay.Instance.Destroy();
                        }
                    },
                    Loader = () => GS.displayStyle
                },
                new IMenuMod.MenuEntry
                {
                    Name = $"Slow Game Down (-{GS.step})",
                    Description = "Change in SpeedChangerGlobalSettings.json",
                    Values = new string[] {
                        GS.slowDownKeybind,
                    },
                    Saver = opt => {},
                    Loader = () => 0
                },
                new IMenuMod.MenuEntry
                {
                    Name = $"Speed Game Up (+{GS.step})",
                    Description = "Change in SpeedChangerGlobalSettings.json",
                    Values = new string[] {
                        GS.speedUpKeybind,
                    },
                    Saver = opt => {},
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

            cursor.EmitDelegate<Func<float>>(() => GS.speed);

            cursor.Emit(OpCodes.Mul);
        }

        /* Set up the hooks */
        public override void Initialize()
        {
            SpeedMultiplier = GS.speed;

            ModHooks.HeroUpdateHook += Update;

            _coroutineHooks = new ILHook[FreezeCoroutines.Length];
            foreach ((MethodInfo coro, int idx) in FreezeCoroutines.Select((mi, idx) => (mi, idx)))
            {
                _coroutineHooks[idx] = new ILHook(coro, ScaleFreeze);
            }

            ModDisplay.Instance = new ModDisplay();
        }

        private void Update()
        {
            if (GS.displayStyle != 2)
            {
                string speedString = GS.displayStyle == 0
                    ? SpeedMultiplier.ToString(SpeedMultiplier >= 10f ? "00.00" : "0.00", CultureInfo.InvariantCulture)
                    : (Math.Round(SpeedMultiplier * 100)).ToString("0.##\\%");
                ModDisplay.Instance.Display($"Game Speed: {speedString}");
                ModDisplay.Instance.Update();
            }

            SpeedMultiplier = SpeedMultiplier;

            if (GS.lockSwitch) return;

            if (Input.GetKeyDown(_speedUpKeybind))
                SpeedMultiplier += GS.step;

            else if (Input.GetKeyDown(_slowDownKeybind))
                SpeedMultiplier -= GS.step;
        }

        public void Unload()
        {
            ModDisplay.Instance.Destroy();

            foreach (ILHook hook in _coroutineHooks)
                hook.Dispose();

            ModHooks.HeroUpdateHook -= Update;

            if (Time.timeScale != 0)
            {
                Time.timeScale = 1;
            }
        }
    }
}
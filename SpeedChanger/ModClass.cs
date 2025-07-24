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
using Satchel.BetterMenus;
using UnityEngine.UIElements;
using IL.InControl;
using HutongGames.PlayMaker.Actions;

using PlayerActionSet = InControl.PlayerActionSet;
using PlayerAction = InControl.PlayerAction;
using Modding.Converters;
using Newtonsoft.Json;

namespace SpeedChanger
{
    public class GlobalSettings
    {
        public bool globalSwitch = true;

        public int displayStyle = 0;

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

        [JsonConverter(typeof(PlayerActionSetConverter))]
        public SpeedChangerBinds binds = new SpeedChangerBinds();
    }
    [UsedImplicitly]
    public class SpeedChanger : Mod, IGlobalSettings<GlobalSettings>, ICustomMenuMod
    {
        public void OnLoadGlobal(GlobalSettings s)
        {
            GS = s;
        }

        public GlobalSettings OnSaveGlobal() => GS;

        private GlobalSettings GS = new();

        public override string GetVersion() => "1.1.3";

        private float gameTimeScale = 1;
        private float _lastNonDefaultSpeed = 1.0f;

        private static readonly MethodInfo[] FreezeCoroutines = (
            from method in typeof(GameManager).GetMethods()
            where method.Name.StartsWith("FreezeMoment")
            where method.ReturnType == typeof(IEnumerator)
            select method.GetCustomAttribute<IteratorStateMachineAttribute>() into attr
            select attr.StateMachineType into type
            select type.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)
        ).ToArray();

        private ILHook[] _coroutineHooks;
        private void LoadCoroutineHooks()
        {
            foreach ((MethodInfo coro, int idx) in FreezeCoroutines.Select((mi, idx) => (mi, idx)))
            {
                _coroutineHooks[idx] = new ILHook(coro, ScaleFreeze);
            }
        }
        private void UnloadCoroutineHooks()
        {
            foreach (ILHook hook in _coroutineHooks)
                hook.Dispose();
        }

        private Menu menu;

        public override void Initialize()
        {
            Time.timeScale = GS.speed;

            On.GameManager.SetTimeScale_float += GameManager_SetTimeScale_1;
            On.QuitToMenu.Start += QuitToMenu_Start;

            _coroutineHooks = new ILHook[FreezeCoroutines.Length];
            LoadCoroutineHooks();

            ModHooks.HeroUpdateHook += Update;

            ModDisplay.Instance = new ModDisplay();
        }

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
        public float SpeedMultiplier
        {
            get
            {
                return GS.globalSwitch ? GS.speed : 1;
            }
            set
            {
                if (value > 0)
                {
                    Time.timeScale = value * gameTimeScale;
                    GS.speed = value;
                }
            }
        }

        public bool ToggleButtonInsideMenu => false;

        private bool buttonPressedFrame;
        private void Update()
        {
            if (!GS.globalSwitch)
            {
                ModDisplay.Instance.Display("");
                Time.timeScale = gameTimeScale;
                return;
            }

            if (GS.displayStyle != 2)
            {
                string speedString = GS.displayStyle == 0
                    ? SpeedMultiplier.ToString(SpeedMultiplier >= 10f ? "00.00" : "0.00", CultureInfo.InvariantCulture)
                    : (Math.Round(SpeedMultiplier * 100)).ToString("0.##\\%");
                ModDisplay.Instance.Display($"Game Speed: {speedString}");
            }
            else
            {
                ModDisplay.Instance.Display("");
            }

            SpeedMultiplier = SpeedMultiplier;

            if (!buttonPressedFrame && GS.binds.SpeedUp.IsPressed)
            {
                SpeedMultiplier += GS.step;
                buttonPressedFrame = true;
            }
            else if (!buttonPressedFrame && GS.binds.SlowDown.IsPressed)
            {
                SpeedMultiplier -= GS.step;
                buttonPressedFrame = true;
            }
            else if (!buttonPressedFrame && GS.binds.ToggleSpeed.WasPressed)
            {
                ToggleSpeed();
            }
            buttonPressedFrame = GS.binds.SlowDown.IsPressed || GS.binds.SpeedUp.IsPressed;
        }
        private void ToggleSpeed()
        {
            if (!GS.globalSwitch) return;

            float current = SpeedMultiplier;

            if (Math.Abs(current - 1.0f) < 0.01f)
            {
                SpeedMultiplier = _lastNonDefaultSpeed;
            }
            else
            {
                _lastNonDefaultSpeed = current;
                SpeedMultiplier = 1.0f;
            }
        }

        private IEnumerator QuitToMenu_Start(On.QuitToMenu.orig_Start orig, QuitToMenu self)
        {
            yield return orig(self);

            Time.timeScale = 1;
            ModDisplay.Instance.Display("");
        }

        private void GameManager_SetTimeScale_1(On.GameManager.orig_SetTimeScale_float orig, GameManager self, float newTimeScale)
        {
            Time.timeScale = newTimeScale * GS.speed;
            gameTimeScale = newTimeScale;
        }
        private void ChangeGlobalSwitchState(bool state)
        {
            GS.globalSwitch = state;
            if (state)
            {
                LoadCoroutineHooks();
                SpeedMultiplier = GS.speed;
            }
            else
            {
                UnloadCoroutineHooks();
                Time.timeScale = 1;
            }
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            if (menu != null) return menu.GetMenuScreen(modListMenu);

            menu = new Menu("SpeedChanger", new Element[]
            {
                new HorizontalOption
                (
                    name: "Global Switch",
                    description: "Turn mod On/Off",
                    values: new string[] { "On", "Off" },
                    applySetting: opt => ChangeGlobalSwitchState(opt == 0),
                    loadSetting: () => GS.globalSwitch ? 0 : 1
                ),
                new HorizontalOption
                (
                    name: "Display Style",
                    description: "Change how speed is displayed",
                    values: new string[] { "#.##", "%", "Off" },
                    applySetting: opt => GS.displayStyle = opt,
                    loadSetting: () => GS.displayStyle
                ),
                new KeyBind
                (
                    name: "Increase game speed",
                    playerAction: GS.binds.SpeedUp
                ),
                new KeyBind
                (
                    name: "Decrease game speed",
                    playerAction: GS.binds.SlowDown
                ),
                new KeyBind
                (
                    name: "Toggle Speed (1.0 ↔ Last)",
                    playerAction: GS.binds.ToggleSpeed
                )       
            });

            return menu.GetMenuScreen(modListMenu);
        }
    }
    public class SpeedChangerBinds : PlayerActionSet
    {
        public PlayerAction SpeedUp;
        public PlayerAction SlowDown;
        public PlayerAction ToggleSpeed;
        public SpeedChangerBinds()
        {
            SpeedUp = CreatePlayerAction("Speed Up");
            SpeedUp.AddDefaultBinding(InControl.Key.Key8);

            SlowDown = CreatePlayerAction("Slow Down");
            SlowDown.AddDefaultBinding(InControl.Key.Key9);
            ToggleSpeed = CreatePlayerAction("Toggle Speed");
            ToggleSpeed.AddDefaultBinding(InControl.Key.Key7);
        }
    }
}

using Modding;
using Modding.Menu;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Vasi;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.PlayerLoop;
using System.Globalization;

namespace HkSpeedUp
{
    [UsedImplicitly]
    public class HkSpeedUp : Mod, IMenuMod
    {

        private KeyCode bindUp = KeyCode.Alpha9;
        private KeyCode bindDown = KeyCode.Alpha8;

        private bool globalSwitch = true;
        private bool lockSwitch = false;
        private int formatType = 0;

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
                    Time.timeScale = value;
                    _speedMultiplier = value;
                }
            }
        }

        public override string GetVersion() => "1.2.3.4.5.6.7";

        private ILHook[] _coroutineHooks;

        bool IMenuMod.ToggleButtonInsideMenu => true;

        public bool ToggleButtonInsideMenu => true;

        private void ChangeGlobalSwitchState(bool state)
        {
            globalSwitch = state;
            if (!state)
                SpeedMultiplier = 1;
        }

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




        public override void Initialize()
        {
            Time.timeScale = SpeedMultiplier;

            ModHooks.HeroUpdateHook += Update;

            ModDisplay.Instance = new ModDisplay();
        }



        private void Update()
        {
            if (!globalSwitch) return;

            ModDisplay.Instance.Display("game speed: " + (formatType == 0 ?
                SpeedMultiplier.ToString(SpeedMultiplier >= 10f ? "00.00" : "0.00", CultureInfo.InvariantCulture) :
                (Math.Round(SpeedMultiplier*100)).ToString("0.##\\%")));
            
            ModDisplay.Instance.Update();

            if (lockSwitch) return;

            if (Input.GetKeyDown(bindUp))
                SpeedMultiplier += 0.05f;

            else if (Input.GetKeyDown(bindDown))
                SpeedMultiplier -= 0.05f;
        }
    }
}
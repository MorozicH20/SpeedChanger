using Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HkSpeedUp
{
    internal class ModDisplay
    {
        internal static ModDisplay Instance;

        private string DisplayText = "Boss Attacks";
        private DateTime DisplayExpireTime = DateTime.Now;
        private TimeSpan DisplayDuration = TimeSpan.FromSeconds(6);
        private Vector2 TextSize = new(800, 500);
        private Vector2 TextPosition = new(0.22f, 0.25f);

        private GameObject _canvas;
        private UnityEngine.UI.Text _text;

        private void Create()
        {
            if (_canvas != null) return;

            // Create base canvas
            _canvas = CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));

            CanvasGroup canvasGroup = _canvas.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            UnityEngine.Object.DontDestroyOnLoad(_canvas);

            _text = CanvasUtil.CreateTextPanel(
                _canvas, "Boss Attacks", 24, TextAnchor.LowerLeft,
                new CanvasUtil.RectData(TextSize, Vector2.zero, TextPosition, TextPosition),
                CanvasUtil.GetFont("Perpetua")
            ).GetComponent<UnityEngine.UI.Text>();
        }

        public void Destroy()
        {
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _text = null;
        }

        public void Update()
        {
            Create();

            _text.text = DisplayText;
            _canvas.SetActive(true);
        }
        public void Display(string text)
        {
            DisplayText = text.Trim();
            DisplayExpireTime = DateTime.Now + DisplayDuration;
            Update();
            Task.Delay(DisplayDuration + TimeSpan.FromMilliseconds(100)).ContinueWith(t => Update());
        }
    }
}

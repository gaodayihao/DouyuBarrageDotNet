using System;
using System.Collections.Generic;
using System.Linq;
using FlysEngine.Desktop;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.DXGI;

namespace DouyuBarrageDotNet.Desktop
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var barrages = new LinkedList<OnScreenBarrage>();
            var form = new LayeredRenderWindow { WindowState = FormWindowState.Maximized, TopMost = true };
            IDisposable connection = null;
            form.Load += (_, _) =>
            {
                connection = DouyuBarrage.ChatMessageFromUrl("https://www.douyu.com/74751").ToObservable().Subscribe(new Observer(form, barrages));
            };

            form.FormClosing += (_, _) => connection?.Dispose();

            form.UpdateLogic += (window, dt) =>
            {
                var node = barrages.First;
                while (node != null)
                {
                    var next = node.Next;
                    node.Value.MoveLeft(dt, window.Width / 19f);
                    if (!node.Value.IsOnScreen(window.XResource.RenderTarget.Size))
                        barrages.Remove(node);
                    node = next;
                }
            };

            form.Draw += (window, ctx) =>
            {
                ctx.Clear(Color.Transparent);
                foreach (var item in barrages)
                {
                    ctx.DrawTextLayout(item.Position, item.TextLayout, window.XResource.GetColor(item.Color),
                        DrawTextOptions.EnableColorFont);
                }
            };

            RenderLoop.Run(form, () => form.Render(1, PresentFlags.None));
        }
    }

    class Observer : IObserver<Barrage>
    {
        private readonly LayeredRenderWindow _renderWindow;
        private readonly LinkedList<OnScreenBarrage> _barrages;
        const float FontSize = 35;

        public Observer(LayeredRenderWindow renderWindow, LinkedList<OnScreenBarrage> barrages)
        {
            _renderWindow = renderWindow;
            _barrages = barrages;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Barrage value)
        {
            _renderWindow.Invoke(new Action(() =>
            {
                var osb = new OnScreenBarrage
                {
                    Position = new Vector2(_renderWindow.XResource.RenderTarget.Size.Width - 1, GetNewY()),
                    TextLayout = _renderWindow.XResource.TextLayouts[value.Message, FontSize],
                    Color = new Color((value.Color << 8) + 0xff),
                };
                _barrages.AddLast(osb);
            }));
        }

        float GetNewY()
        {
            float y = 0;
            while (_barrages.Reverse().Where(x => Math.Abs(x.Position.Y - y) < 0.001).Select(x => x.Rect.Right).FirstOrDefault() > _renderWindow.Width)
            {
                y += FontSize;
            }
            return y;
        }
    }

    class OnScreenBarrage
    {
        public Vector2 Position;

        internal TextLayout TextLayout;

        public Color Color;

        public RectangleF Rect => new RectangleF(Position.X, Position.Y, TextLayout.Metrics.Width, TextLayout.Metrics.Height);

        public bool IsOnScreen(Size2F screenSize) => Rect.Right > 0;

        public void MoveLeft(float dt, float speed)
        {
            Position.X -= dt * speed;
        }
    }
}

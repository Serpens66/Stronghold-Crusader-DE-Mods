using System;
using Noesis;
using CrusaderDE;

namespace LordOverviewTest
{
    // Reuse Vanilla's image-button states, as CastlePlanner does, with precomposed artwork.
    internal static class OverviewAppearance
    {
        private static readonly ImageSource[] artwork = new ImageSource[3];
        internal static Button CreateButton(FrameworkElement host)
        {
            var style = host.TryFindResource("BTN_Image") as Style ??
                throw new InvalidOperationException("Vanilla image button style missing.");
            string[] states = { "Normal", "Hover", "Pressed" };
            for (int i=0;i<states.Length;i++)
            {
                if (artwork[i] != null) continue;
                var image = new BitmapImage(new Uri("Assets/GUI/Sprites/LordOverviewTest_Crown"+states[i]+".png", UriKind.Relative));
                if (image.PixelWidth != 68 || image.PixelHeight != 68)
                    throw new InvalidOperationException("Overview crown asset failed to load: " + states[i]);
                artwork[i] = image;
            }
            var button = new Button { Style = style };
            PropEx.SetSprite1(button, artwork[0]);
            PropEx.SetSprite2(button, artwork[1]);
            PropEx.SetSprite3(button, artwork[2]);
            PropEx.SetSprite4(button, artwork[0]);
            return button;
        }

        internal static Grid PlayerText(string text, Brush foreground)
        {
            var grid = new Grid { VerticalAlignment=VerticalAlignment.Center };
            float[,] offsets={{-0.65f,0},{0.65f,0},{0,-0.65f},{0,0.65f}};
            for(int i=0;i<4;i++) grid.Children.Add(new TextBlock { Text=text, FontSize=16,
                Foreground=new SolidColorBrush(Color.FromArgb(255,35,28,21)),
                RenderTransform=new TranslateTransform(offsets[i,0],offsets[i,1]), IsHitTestVisible=false });
            grid.Children.Add(new TextBlock { Text=text, FontSize=16, Foreground=foreground,
                Effect=new DropShadowEffect {ShadowDepth=1,BlurRadius=1,Color=Color.FromArgb(230,20,15,10)}, IsHitTestVisible=false });
            return grid;
        }

        internal static Grid Portrait(ImageSource source)
        {
            var crop = new Grid { Width=28, Height=28, ClipToBounds=true };
            crop.Children.Add(new Image { Source=source, Width=30, Height=30, Margin=new Thickness(-1), Stretch=Stretch.Fill });
            return crop;
        }
    }
}

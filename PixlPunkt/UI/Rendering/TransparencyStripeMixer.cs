using System;
using Microsoft.UI.Xaml;

namespace PixlPunkt.UI.Rendering
{
    public static class TransparencyStripeMixer
    {
        public static byte LightR = 255;
        public static byte LightG = 255;
        public static byte LightB = 255;

        public static byte DarkR = 232;
        public static byte DarkG = 232;
        public static byte DarkB = 232;

        // NEW: event so previews can react immediately
        public static event Action? ColorsChanged;

        public static void ApplyTheme(ElementTheme theme)
        {
            if (theme == ElementTheme.Dark)
            {
                LightR = 48; LightG = 48; LightB = 48;
                DarkR = 36; DarkG = 36; DarkB = 36;
            }
            else
            {
                LightR = 255; LightG = 255; LightB = 255;
                DarkR = 232; DarkG = 232; DarkB = 232;
            }
            ColorsChanged?.Invoke();
        }
    }
}
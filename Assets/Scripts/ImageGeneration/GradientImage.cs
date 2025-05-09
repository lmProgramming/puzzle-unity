using UnityEngine;

namespace ImageGeneration
{
    public class GradientImage : ImageProvider
    {
        public Color color1 = Color.red;

        public Color color2 = Color.blue;
        public Color color3 = Color.green;
        public Color color4 = Color.yellow;

        public override Color[,] GetImage(int width, int height, int seed = 0)
        {
            var pixels = new Color[width, height];

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var u = (float)x / width;
                var v = (float)y / height;
                var c1 = Color.Lerp(color1, color2, u);
                var c2 = Color.Lerp(color3, color4, u);
                pixels[x, y] = Color.Lerp(c1, c2, v);
            }

            return pixels;
        }
    }
}
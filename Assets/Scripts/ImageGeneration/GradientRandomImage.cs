using UnityEngine;

namespace ImageGeneration
{
    public class GradientRandomImage : ImageProvider
    {
        private Color _color1;

        private Color _color2;
        private Color _color3;
        private Color _color4;

        private void Start()
        {
            _color1 = new Color(Random.value, Random.value, Random.value);
            _color2 = new Color(Random.value, Random.value, Random.value);
            _color3 = new Color(Random.value, Random.value, Random.value);
            _color4 = new Color(Random.value, Random.value, Random.value);
        }

        public override Color[,] GetImage(int width, int height, int seed = 0)
        {
            var pixels = new Color[width, height];

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var u = (float)x / width;
                var v = (float)y / height;
                var c1 = Color.Lerp(_color1, _color2, u);
                var c2 = Color.Lerp(_color3, _color4, u);
                pixels[x, y] = Color.Lerp(c1, c2, v);
            }

            return pixels;
        }
    }
}
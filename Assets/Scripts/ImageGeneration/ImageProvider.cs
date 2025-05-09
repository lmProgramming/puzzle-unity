using UnityEngine;

namespace ImageGeneration
{
    public abstract class ImageProvider : MonoBehaviour
    {
        public abstract Color[,] GetImage(int width, int height, int seed = 0);
    }
}
using System;
using UnityEngine;

namespace Hooligapps.SupportGate.UI
{
    /// <summary>
    /// Превью картинки для плитки вложения — как в веб-форме, где браузер показывает
    /// миниатюру. Декодируется в полный размер и сразу уменьшается: скриншот
    /// экрана целиком держать в памяти ради плитки 112 px незачем.
    /// </summary>
    public static class SupportGatePreview
    {
        public const int MaxSide = 256;

        public static Texture2D Create(string contentType, byte[] data)
        {
            if (data == null || data.Length == 0 || !IsDecodable(contentType))
            {
                return null;
            }

            var full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!full.LoadImage(data, false))
                {
                    return null;
                }

                // Без графического устройства (batchmode -nographics) блит не работает.
                var headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
                if (headless || (full.width <= MaxSide && full.height <= MaxSide))
                {
                    var keep = full;
                    full = null;
                    keep.name = "SupportGatePreview";
                    return keep;
                }

                return Downscale(full);
            }
            catch (Exception)
            {
                // Битый файл — сервер его всё равно примет или отвергнет сам, а
                // форма покажет расширение вместо картинки.
                return null;
            }
            finally
            {
                if (full != null)
                {
                    UnityEngine.Object.Destroy(full);
                }
            }
        }

        private static bool IsDecodable(string contentType)
        {
            // Texture2D.LoadImage понимает только PNG и JPEG.
            return contentType == "image/png" || contentType == "image/jpeg" || contentType == "image/jpg";
        }

        private static Texture2D Downscale(Texture2D source)
        {
            var scale = Mathf.Min((float)MaxSide / source.width, (float)MaxSide / source.height);
            var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;

                var result = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "SupportGatePreview" };
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply(false, true);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}

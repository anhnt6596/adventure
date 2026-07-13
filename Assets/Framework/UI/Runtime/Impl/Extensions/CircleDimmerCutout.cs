using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public class CircleDimmerCutout : VisualElement
    {
        public float MaxAlpha { get; set; } = 0.5f;
        public float FadeWidth { get; set; } = 60f;

        public Vector2 CutoutCenter => _center;
        public float CutoutRadius => _radius;

        private Vector2 _center;
        private float _radius;
        private bool _hasCutout;
        private float _alpha = 0.5f;

        private Texture2D _gradientTex;
        private Texture2D _whiteTex;
        private const int TexSize = 128;

        public CircleDimmerCutout()
        {
            generateVisualContent += OnGenerateVisualContent;
            CreateTextures();
        }

        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
            MarkDirtyRepaint();
        }

        public void SetCutout(Vector2 center, float radius)
        {
            _center = center;
            _radius = radius;
            _hasCutout = true;
            MarkDirtyRepaint();
        }

        public void ClearCutout()
        {
            _hasCutout = false;
            MarkDirtyRepaint();
        }

        private void CreateTextures()
        {
            // 1x1 white texture for solid dim quads
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            // Radial gradient texture for the fade circle
            _gradientTex = new Texture2D(TexSize, TexSize, TextureFormat.Alpha8, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var half = TexSize * 0.5f;
            var pixels = new Color32[TexSize * TexSize];

            for (var y = 0; y < TexSize; y++)
            {
                for (var x = 0; x < TexSize; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float t;
                    if (dist <= 0.5f) t = 0f;
                    else if (dist >= 1f) t = 1f;
                    else t = (dist - 0.5f) * 2f;

                    // smoothstep
                    t = t * t * (3f - 2f * t);

                    var a = (byte)(t * 255);
                    pixels[y * TexSize + x] = new Color32(255, 255, 255, a);
                }
            }

            _gradientTex.SetPixels32(pixels);
            _gradientTex.Apply();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var size = contentRect;
            if (size.width <= 0 || size.height <= 0) return;

            if (!_hasCutout)
            {
                DrawSolidQuad(mgc, 0, 0, size.width, size.height);
                return;
            }

            var outerRadius = _radius + FadeWidth;
            var left = _center.x - outerRadius;
            var right = _center.x + outerRadius;
            var top = _center.y - outerRadius;
            var bottom = _center.y + outerRadius;

            var cl = Mathf.Max(0, left);
            var cr = Mathf.Min(size.width, right);
            var ct = Mathf.Max(0, top);
            var cb = Mathf.Min(size.height, bottom);

            // Top strip
            if (ct > 0)
                DrawSolidQuad(mgc, 0, 0, size.width, ct);

            // Bottom strip
            if (cb < size.height)
                DrawSolidQuad(mgc, 0, cb, size.width, size.height);

            // Left strip
            if (cl > 0)
                DrawSolidQuad(mgc, 0, ct, cl, cb);

            // Right strip
            if (cr < size.width)
                DrawSolidQuad(mgc, cr, ct, size.width, cb);

            // Gradient circle
            DrawGradientCircle(mgc, outerRadius);
        }

        private void DrawSolidQuad(MeshGenerationContext mgc, float x0, float y0, float x1, float y1)
        {
            if (x1 <= x0 || y1 <= y0) return;

            var md = mgc.Allocate(4, 6, _whiteTex);
            if (md.vertexCount == 0) return;

            var tint = new Color(0f, 0f, 0f, _alpha);

            md.SetNextVertex(new Vertex { position = new Vector3(x0, y0, Vertex.nearZ), uv = new Vector2(0, 0), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x1, y0, Vertex.nearZ), uv = new Vector2(1, 0), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x1, y1, Vertex.nearZ), uv = new Vector2(1, 1), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x0, y1, Vertex.nearZ), uv = new Vector2(0, 1), tint = tint });

            md.SetNextIndex(0);
            md.SetNextIndex(1);
            md.SetNextIndex(2);
            md.SetNextIndex(0);
            md.SetNextIndex(2);
            md.SetNextIndex(3);
        }

        private void DrawGradientCircle(MeshGenerationContext mgc, float outerRadius)
        {
            var md = mgc.Allocate(4, 6, _gradientTex);
            if (md.vertexCount == 0) return;

            var tint = new Color(0f, 0f, 0f, _alpha);

            var x0 = _center.x - outerRadius;
            var y0 = _center.y - outerRadius;
            var x1 = _center.x + outerRadius;
            var y1 = _center.y + outerRadius;

            md.SetNextVertex(new Vertex { position = new Vector3(x0, y0, Vertex.nearZ), uv = new Vector2(0, 1), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x1, y0, Vertex.nearZ), uv = new Vector2(1, 1), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x1, y1, Vertex.nearZ), uv = new Vector2(1, 0), tint = tint });
            md.SetNextVertex(new Vertex { position = new Vector3(x0, y1, Vertex.nearZ), uv = new Vector2(0, 0), tint = tint });

            md.SetNextIndex(0);
            md.SetNextIndex(1);
            md.SetNextIndex(2);
            md.SetNextIndex(0);
            md.SetNextIndex(2);
            md.SetNextIndex(3);
        }
    }
}

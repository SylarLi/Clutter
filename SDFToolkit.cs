using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SDFToolKits : EditorWindow
{
    [MenuItem("Window/SDFToolKits")]
    public static void Create()
    {
        GetWindow<SDFToolKits>().Show();
    }

    private Texture2D input;

    private void OnGUI()
    {
        input = (Texture2D)EditorGUILayout.ObjectField("Texture", input, typeof(Texture2D), false);
        if (GUILayout.Button("Test"))
        {
            if (input != null)
                new SDFGenerator(input).Generate();
        }
    }

    private class SDFGenerator
    {
        private static float InsideDefault = -99999;
        private static float OutsideDefault = 99999;

        private struct Point
        {
            public int X;
            public int Y;
            public float Distance;
        }

        private Texture2D _source;

        private int _sourceWidth;
        private int _sourceHeight;

        private Texture2D _sdfTex;

        public Texture2D SDFTex => _sdfTex;

        private float[] _raw;
        private float[] _points;
        private float[] _inside;
        private float[] _outside;
        private float[] _distance;
        private float[] _normalized;

        private int _distanceWidth;
        private int _distanceHeight;

        public SDFGenerator(Texture2D texture)
        {
            _source = texture;
            _sourceWidth = _source.width;
            _sourceHeight = _source.height;
        }

        public void Generate()
        {
            EstimateDistance();
            CalculateEdge();
            SweepBy8ssedt();
            //Downsampling();
            //Downsampling();
            //Downsampling();
            Normalize2();
            GenSDFTex();
        }

        private void EstimateDistance()
        {
            var colors = _source.GetPixels();
            _raw = new float[colors.Length];
            for (var i = colors.Length - 1; i >= 0; i--)
            {
                _raw[i] = EstimateInside(colors[i]) ? InsideDefault : OutsideDefault;
            }
        }

        private void CalculateEdge()
        {
            _points = new float[_raw.Length];
            for (var i = _raw.Length - 1; i >= 0; i--)
            {
                var x = i % _sourceWidth;
                var y = i / _sourceWidth;
                _points[i] = CheckEdge(x, y) ?
                    (CheckOutside(x, y) ? 0.5f : -0.5f) :
                    _raw[i];
            }
        }

        private void SweepBy8ssedt()
        {
            BuildSweepGrids();
            SweepGrid(_inside);
            SweepGrid(_outside);
            Merge();
        }

        private void SweepByBF()
        {
            _distance = _points.Select((d, i) =>
            {
                var index = FindNearestEdgePoint(i);
                var dx = i % _sourceWidth - index % _sourceWidth;
                var dy = i / _sourceWidth - index / _sourceWidth;
                var dist = Mathf.Sqrt(dx * dx + dy * dy) + 0.5f;
                if (d < 0) dist = -dist;
                return dist;
            }).ToArray();
        }

        private int FindNearestEdgePoint(int index)
        {
            var ret = -1;
            var open = new Queue<int>();
            open.Enqueue(index);
            var close = new List<int>();
            var neibors = new int[4];
            while (open.Count > 0)
            {
                var i = open.Dequeue();
                if (_points[i] < 1)
                {
                    ret = i;
                    break;
                }
                close.Add(i);
                neibors[0] = i - 1;
                neibors[1] = i + 1;
                neibors[2] = i - _sourceWidth;
                neibors[3] = i + _sourceWidth;
                foreach (var n in neibors)
                {
                    if (n >= 0 && n < _points.Length && 
                        !open.Contains(n) && !close.Contains(n))
                        open.Enqueue(n);
                }
            }
            return ret;
        }

        private void Downsampling()
        {
            var sampling = new float[_distance.Length / 4];
            var sampleWidth = _distanceWidth / 2;
            var sampleHeight = _distanceHeight /= 2;
            for (var i = sampling.Length - 1; i >= 0; i--)
            {
                var sx = (i % sampleWidth) * 2;
                var sy = (i / sampleHeight) * 2;
                var dist = _distance[sy * _distanceWidth + sx] * 0.25f +
                    _distance[sy * _distanceWidth + sx + 1] * 0.25f +
                    _distance[(sy + 1) * _distanceWidth + sx + 1] * 0.25f +
                    _distance[(sy + 1) * _distanceWidth + sx] * 0.25f;
                sampling[i] = dist / 2;
            }
            _distance = sampling;
            _distanceWidth = sampleWidth;
            _distanceHeight = sampleHeight;
        }

        /// <summary>
        /// 直接按距离值归一化
        /// </summary>
        private void Normalize1()
        {
            _normalized = _distance.Select(d =>
            {
                if (d <= -1) return 1;
                else if (d >= 1) return 0;
                return 1 - (d + 1) * 0.5f;
            }).ToArray();
        }

        /// <summary>
        /// 按距离最大值归一化
        /// </summary>
        private void Normalize2()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (var d in _distance)
            {
                if (d < min) min = d;
                if (d > max) max = d;
            }
            var mag = Mathf.Max(Mathf.Abs(max), Mathf.Abs(min));
            if (mag == 0) mag = 1;
            _normalized = _distance.Select(d =>
            {
                if (d > 0)
                    return Mathf.Lerp(0.5f, 0, d / mag);
                else if (d < 0)
                    return Mathf.Lerp(0.5f, 1, -d / mag);
                return 0.5f;
            }).ToArray();
        }

        private void GenSDFTex()
        {
            var pixels = _normalized.Select(i => new Color(i, i, i)).ToArray();
            _sdfTex = new Texture2D(_distanceWidth, _distanceHeight, TextureFormat.RGB24, false);
            _sdfTex.SetPixels(pixels);
            var bytes = _sdfTex.EncodeToPNG();
            File.WriteAllBytes("Assets/sdf.png", bytes);
        }

        private void BuildSweepGrids()
        {
            _inside = new float[_points.Length];
            _outside = new float[_points.Length];
            for (var i = _points.Length - 1; i >= 0; i--)
            {
                var dist = _points[i];
                _inside[i] = dist >= 0 ? 0 : -dist;
                _outside[i] = dist >= 0 ? dist : 0;
            }
        }

        private void SweepGrid(float[] grid)
        {
            for (var y = 0; y < _sourceHeight; y++)
            {
                for (var x = 0; x < _sourceWidth; x++)
                {
                    Compare(grid, x, y, -1, 0);
                    Compare(grid, x, y, 0, -1);
                    Compare(grid, x, y, -1, -1);
                    Compare(grid, x, y, 1, -1);
                }
                for (var x = _sourceWidth - 1; x >= 0; x--)
                {
                    Compare(grid, x, y, 1, 0);
                }
            }
            for (var y = _sourceHeight - 1; y >= 0; y--)
            {
                for (var x = _sourceWidth - 1; x >= 0; x--)
                {
                    Compare(grid, x, y, 1, 0);
                    Compare(grid, x, y, 0, 1);
                    Compare(grid, x, y, -1, 1);
                    Compare(grid, x, y, 1, 1);
                }
                for (var x = 0; x < _sourceWidth; x++)
                {
                    Compare(grid, x, y, -1, 0);
                }
            }
        }

        private void Merge()
        {
            _distance = _outside.Select((d, i) => d - _inside[i]).ToArray();
            _distanceWidth = _sourceWidth;
            _distanceHeight = _sourceHeight;
        }

        private void Compare(float[] grid, int x, int y, int xOffset, int yOffset)
        {
            var nx = x + xOffset;
            var ny = y + yOffset;
            if (nx < 0 || nx >= _sourceWidth ||
                ny < 0 || ny >= _sourceHeight)
                return;
            var index = y * _sourceWidth + x;
            var cdist = grid[index];
            var ndist = grid[ny * _sourceWidth + nx] + Mathf.Sqrt(xOffset * xOffset + yOffset * yOffset);
            if (cdist > ndist)
                grid[index] = ndist;
        }

        private bool EstimateInside(Color color)
        {
            return color.a > 0.9f;
        }

        private bool CheckOutside(int x, int y)
        {
            if (x < 0 || x >= _sourceWidth ||
                y < 0 || y >= _sourceHeight)
                return true;
            return _raw[y * _sourceWidth + x] > 0;
        }

        private bool CheckEdge(int x, int y)
        {
            var outside = CheckOutside(x, y);
            return outside != CheckOutside(x - 1, y - 1) ||
                outside != CheckOutside(x - 1, y) ||
                outside != CheckOutside(x, y - 1) ||
                outside != CheckOutside(x + 1, y + 1) ||
                outside != CheckOutside(x + 1, y) ||
                outside != CheckOutside(x, y + 1) ||
                outside != CheckOutside(x - 1, y + 1) ||
                outside != CheckOutside(x + 1, y - 1);

        }
    }
}

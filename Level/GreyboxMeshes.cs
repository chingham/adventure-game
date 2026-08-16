using System.Numerics;
using Quark.Assets;

namespace AdventureGame.Level;

static class GreyboxMeshes {
    public static Geometry Terrain(float sizeX, float sizeY, int cols, int rows, float amplitude, UvMapping uv = default) {
        var uvExtent = uv.Extent(sizeX, sizeY);
        var vertices = new Vertex[(cols + 1) * (rows + 1)];
        for (var j = 0; j <= rows; j++) {
            for (var i = 0; i <= cols; i++) {
                var x = (i / (float)cols - 0.5f) * sizeX;
                var y = (j / (float)rows - 0.5f) * sizeY;
                vertices[j * (cols + 1) + i] = new Vertex(
                    new Vector3(x, y, Height(x, y, amplitude)),
                    Normal(x, y, amplitude),
                    new Vector2(i / (float)cols, 1 - j / (float)rows) * uvExtent);
            }
        }

        var indices = new List<uint>(cols * rows * 6);
        for (var j = 0; j < rows; j++) {
            for (var i = 0; i < cols; i++) {
                var v00 = (uint)(j * (cols + 1) + i);
                var v10 = v00 + 1;
                var v01 = v00 + (uint)(cols + 1);
                var v11 = v01 + 1;
                indices.Add(v00); indices.Add(v10); indices.Add(v11);
                indices.Add(v00); indices.Add(v11); indices.Add(v01);
            }
        }

        return Geometry.FromVertices(vertices, indices.ToArray());
    }

    static float Height(float x, float y, float amplitude) {
        var h = 0.6f * MathF.Sin(x * 0.6f) * MathF.Cos(y * 0.6f)
            + 0.4f * MathF.Sin(x * 0.3f + y * 0.2f + 1f);
        return amplitude * (h + 1f);
    }
    static Vector3 Normal(float x, float y, float amplitude) {
        const float e = 0.05f;
        var dx = Height(x + e, y, amplitude) - Height(x - e, y, amplitude);
        var dy = Height(x, y + e, amplitude) - Height(x, y - e, amplitude);
        return Vector3.Normalize(new Vector3(-dx / (2 * e), -dy / (2 * e), 1));
    }
}

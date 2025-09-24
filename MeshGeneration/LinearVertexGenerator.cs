using Godot;
using Structures;
using UtilityLibrary;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration
{
    public class LinearVertexGenerator : IVertexGenerator
    {
        public Point[] GenerateVertices(int count, Point start, Point end)
        {
            Logger.EnterFunction("LinearVertexGenerator.GenerateVertices", $"count: {count}, start: {start.Index}, end: {end.Index}");
            Logger.Debug($"Starting vertex generation with {count} vertices between points {start.Index} and {end.Index}", "LinearVertexGenerator");
            
            if (count <= 0) 
            {
                Logger.Info("Vertex count is zero or negative, returning empty array", "LinearVertexGenerator");
                Logger.ExitFunction("LinearVertexGenerator.GenerateVertices", "Point[0]");
                return new Point[0];
            }
            
            Point[] vertices = new Point[count];
            float step = 1.0f / (count + 1);
            Logger.Debug($"Calculated step size: {step}", "LinearVertexGenerator");

            for (int i = 0; i < count; ++i)
            {
                float t = (i + 1) * step;
                Vector3 pos = start.ToVector3().Lerp(end.ToVector3(), t);
                vertices[i] = new Point(pos);
                
                Logger.Debug($"Generated vertex {i} at position ({pos.X}, {pos.Y}, {pos.Z}) with parameter t={t}", "LinearVertexGenerator");
                
                if (VertexPoints.ContainsKey(vertices[i].Index))
                {
                    vertices[i] = VertexPoints[vertices[i].Index];
                    Logger.Debug($"Vertex {vertices[i].Index} already exists, using existing vertex", "LinearVertexGenerator");
                }
                else
                {
                    VertexPoints.Add(vertices[i].Index, vertices[i]);
                    Logger.Debug($"Added new vertex {vertices[i].Index} to VertexPoints dictionary", "LinearVertexGenerator");
                }
            }
            
            Logger.Info($"Successfully generated {vertices.Length} vertices", "LinearVertexGenerator");
            Logger.ExitFunction("LinearVertexGenerator.GenerateVertices", $"Point[{vertices.Length}]");
            return vertices;
        }

        public float GetParameterization(float index, float total)
        {
            Logger.EnterFunction("LinearVertexGenerator.GetParameterization", $"index: {index}, total: {total}");
            float parameter = (index + 1.0f) / (total + 1.0f);
            Logger.Debug($"Calculated parameterization: {parameter}", "LinearVertexGenerator");
            Logger.ExitFunction("LinearVertexGenerator.GetParameterization", parameter.ToString());
            return parameter;
        }

        public bool ValidateConfiguration(int vertexCount)
        {
            Logger.EnterFunction("LinearVertexGenerator.ValidateConfiguration", $"vertexCount: {vertexCount}");
            bool isValid = vertexCount >= 0;
            Logger.Debug($"Configuration validation result: {isValid}", "LinearVertexGenerator");
            Logger.ExitFunction("LinearVertexGenerator.ValidateConfiguration", isValid.ToString());
            return isValid;
        }
    }
}


using System;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration
{
    public class GeometricVertexGenerator : IVertexGenerator
    {
        public float Exponent { get; set; } = 2.0f;
        
        public Point[] GenerateVertices(int count, Point start, Point end)
        {
            Logger.EnterFunction("GeometricVertexGenerator.GenerateVertices", $"count: {count}, start: {start.Index}, end: {end.Index}, exponent: {Exponent}");
            Logger.Debug($"Starting geometric vertex generation with {count} vertices between points {start.Index} and {end.Index}", "GeometricVertexGenerator");
            
            if (count <= 0) 
            {
                Logger.Info("Vertex count is zero or negative, returning empty array", "GeometricVertexGenerator");
                Logger.ExitFunction("GeometricVertexGenerator.GenerateVertices", "Point[0]");
                return new Point[0];
            }
            
            Point[] vertices = new Point[count];
            Vector3 startVector = start.Position;
            Vector3 endVector = end.Position;
            Logger.Debug($"Start vector: ({startVector.X}, {startVector.Y}, {startVector.Z}), End vector: ({endVector.X}, {endVector.Y}, {endVector.Z})", "GeometricVertexGenerator");

            for (int i = 0; i < count; ++i)
            {
                float t = GetParameterization(i, count);
                Vector3 pos = startVector.Lerp(endVector, t);
                vertices[i] = new Point(pos);
                Logger.Debug($"Generated vertex {i} at position ({pos.X}, {pos.Y}, {pos.Z}) with parameter t={t}", "GeometricVertexGenerator");
            }
            
            Logger.Info($"Successfully generated {vertices.Length} geometric vertices with exponent {Exponent}", "GeometricVertexGenerator");
            Logger.ExitFunction("GeometricVertexGenerator.GenerateVertices", $"Point[{vertices.Length}]");
            return vertices;
        }

        public float GetParameterization(float index, float total)
        {
            Logger.EnterFunction("GeometricVertexGenerator.GetParameterization", $"index: {index}, total: {total}, exponent: {Exponent}");
            float normalized = (index) / (total + 1.0f);
            float parameter = (float)Math.Pow(normalized, 1.0f / Exponent);
            Logger.Debug($"Calculated parameterization - normalized: {normalized}, result: {parameter}", "GeometricVertexGenerator");
            Logger.ExitFunction("GeometricVertexGenerator.GetParameterization", parameter.ToString());
            return parameter;
        }

        public bool ValidateConfiguration(int vertexCount)
        {
            Logger.EnterFunction("GeometricVertexGenerator.ValidateConfiguration", $"vertexCount: {vertexCount}");
            bool isValid = vertexCount >= 0;
            Logger.Debug($"Configuration validation result: {isValid}", "GeometricVertexGenerator");
            Logger.ExitFunction("GeometricVertexGenerator.ValidateConfiguration", isValid.ToString());
            return isValid;
        }
    }
}

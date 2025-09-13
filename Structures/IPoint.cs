using System;
namespace Structures;
public interface IPoint
{
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
    int Index { get; set; }
    float Stress { get; set; }
}

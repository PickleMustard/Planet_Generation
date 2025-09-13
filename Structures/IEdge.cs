namespace Structures;
public interface IEdge {
  Point P {get; }
  Point Q {get; }
  int Index{get; }
  float Stress{get; set; }
}

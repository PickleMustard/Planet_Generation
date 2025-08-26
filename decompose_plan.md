# Conversation History

---

**User** (*2025-08-22 14:25:26*)

Currently, in @GenerateDocArrayMesh.cs, the starting dodecahedron is decomposed by adding an additional vertex along all edges and triangulating the new shape. This is repeated for subdivide times. This causes the # of vertices to grow by an exponential power of two. I would like to implement a system that can define the number of vertices to generate along each iteration. 
Lets come up with a general plan for the large steps to work on. Think about what might be needed along the way to implement the larger goal. Create an ordered list with those steps. Finally, compile a small summary of the task alongside a description of the task.

[File: GenerateDocArrayMesh.cs]

Called the Read tool with the following input: {"filePath":"/home/picklemustard/Programming/Game-Development/delaunay-triangulation-map-generation/GenerateDocArrayMesh.cs"}

<file>
00001| using Godot;
00002| using System;
00003| using System.Collections.Generic;
00004| using System.Linq;
00005| 
00006| public static class Extension
00007| {
00008|     public static Vector3[] ToVectors3(this IEnumerable<Point> points) => points.Select(point => point.ToVector3()).ToArray();
00009|     public static Vector3 ToVector3(this Point point) => new Vector3((float)point.X, (float)point.Y, (float)point.Z);
00010|     public static Vector2 ToVector2(this Point point) => new Vector2((float)point.X, (float)point.Y);
00011|     public static Point[] ToPoints(this IEnumerable<Vector3> vertices) => vertices.Select(vertex => vertex.ToPoint()).ToArray();
00012|     public static Point ToPoint(this Vector3 vertex) => new Point(vertex);
00013|     public static Edge ReverseEdge(this Edge e) { var t = e.Q; e.Q = e.P; e.P = t; return e; }
00014| }
00015| 
00016| public partial class GenerateDocArrayMesh : MeshInstance3D
00017| {
00018|     public struct Face
00019|     {
00020|         public Point[] v;
00021| 
00022|         public Face(Point v0, Point v1, Point v2)
00023|         {
00024|             v = new Point[] { v0, v1, v2 };
00025|         }
00026|         public Face(params Point[] points)
00027|         {
00028|             v = points;
00029|         }
00030|         public Face(IEnumerable<Point> points)
00031|         {
00032|             v = points.ToArray();
00033|         }
00034|     }
00035| 
00036|     public DelaunatorSharp.Delaunator dl;
00037|     static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
00038|     Vector3 origin = new Vector3(0, 0, 0);
00039|     public List<Point> VertexPoints;
00040|     public List<Vector3> normals;
00041|     public List<Vector2> uvs;
00042|     public List<int> indices;
00043|     public List<Face> faces;
00044|     public int VertexIndex = 0;
00045|     public int testIndex = 0;
00046| 
00047|     public List<Edge> baseEdges = new List<Edge>();
00048|     public List<Triangle> baseTris = new List<Triangle>();
00049|     public List<Face> dualFaces = new List<Face>();
00050|     public List<Point> circumcenters = new List<Point>();
00051|     public List<Edge> generatedEdges = new List<Edge>();
00052|     public Dictionary<(int, int), Edge> worldEdgeMap = new Dictionary<(int, int), Edge>();
00053|     public List<Triangle> generatedTris = new List<Triangle>();
00054|     public List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
00055|     RandomNumberGenerator rand = new RandomNumberGenerator();
00056| 
00057|     [Export]
00058|     public int subdivide = 1;
00059| 
00060|     [Export]
00061|     public int size = 5;
00062| 
00063|     [Export]
00064|     public bool ProjectToSphere = true;
00065| 
00066|     [Export]
00067|     public bool GenerateRealistic = true;
00068| 
00069|     [Export]
00070|     public ulong Seed = 5001;
00071| 
00072|     [Export]
00073|     public int NumAbberations = 3;
00074| 
00075|     [Export]
00076|     public int NumDeformationCycles = 3;
00077| 
00078|     [Export]
00079|     public int NumContinents = 5;
00080| 
00081|     [Export]
00082|     public float ConvergentBoundaryStrength = 0.5f;
00083| 
00084|     [Export]
00085|     public float DivergentBoundaryStrength = 0.3f;
00086| 
00087|     [Export]
00088|     public float TransformBoundaryStrength = 0.1f;
00089| 
00090|     [Export]
00091|     public bool ShouldDisplayBiomes = true;
00092| 
00093|     public override void _Ready()
00094|     {
00095|         rand.Seed = Seed;
00096|         PolygonRendererSDL.DrawPoint(this, size, new Vector3(0, 0, 0), 0.1f, Colors.White);
00097|         //Generate the starting dodecahedron
00098|         PopulateArrays();
00099|         //Split the faces n times
00100|         GenerateNonDeformedFaces();
00101|         List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
00102| 
00103| 
00104|         //Construct Adjacency Matrices for all triangles and edges given all points
00105|         GenerateTriangleList();
00106|         //RenderTriangleAndConnections(tris[0]);
00107| 
00108|         //var p = VertexPoints[300];
00109|         //List<VoronoiCell> VoronoiPoints = new List<VoronoiCell>();
00110|         GD.Print(VertexPoints.Count);
00111|         GD.Print(baseTris.Count);
00112|         var OptimalArea = (4.0f * Mathf.Pi * size * size) / baseTris.Count;
00113|         GD.Print($"Optimal Area of Triangle: {OptimalArea}");
00114|         var OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
00115|         GD.Print($"Optimal Side Length of Triangle: {OptimalSideLength}");
00116|         var OptimalCentroidLength = Mathf.Cos(Mathf.DegToRad(30.0f)) * .5f * OptimalSideLength;
00117|         GD.Print($"Optimal Length from Vertex of Triangle to Centroid: {OptimalCentroidLength}");
00118|         int alteredIndex = 0;
00119|         //var randomTri = baseTris[baseTris.Count - baseTris.Count / 2];
00120|         for (int deforms = 0; deforms < NumDeformationCycles; deforms++)
00121|         {
00122|             GD.Print($"Deformation Cycle: {deforms} | Deform Amount: {(2f + deforms) / (deforms + 1)}");
00123|             for (int i = 0; i < NumAbberations; i++)
00124|             {
00125|                 var randomTri = baseTris[rand.RandiRange(0, baseTris.Count - 1)];
00126|                 var randomTriPoint = randomTri.Points[rand.RandiRange(0, 2)];
00127|                 var edgesWithPoint = baseEdges.Where(e => randomTri.Points.Contains(e.Q) && randomTri.Points.Contains(e.P));
00128|                 var edgesWithPointList = edgesWithPoint.ToList();
00129|                 List<Edge> edgesFromPoint = baseEdges.Where(e => e.P == randomTriPoint).ToList();
00130|                 List<Edge> edgesToPoint = baseEdges.Where(e => e.Q == randomTriPoint).ToList();
00131|                 List<Edge> allEdges = new List<Edge>(edgesFromPoint);
00132|                 allEdges.AddRange(edgesToPoint);
00133|                 bool shouldRedo = false;
00134|                 foreach (Edge e in edgesFromPoint)
00135|                 {
00136|                     List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.Q).ToList();
00137|                     List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.Q).ToList();
00138| 
00139|                     //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
00140|                     if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;
00141| 
00142|                 }
00143|                 foreach (Edge e in edgesToPoint)
00144|                 {
00145|                     List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.P).ToList();
00146|                     List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.P).ToList();
00147| 
00148|                     //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
00149|                     if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;
00150| 
00151|                 }
00152|                 //GD.Print($"Edges from Point: {edgesToPoint.Count + edgesFromPoint.Count}");
00153|                 bool EnoughEdges = edgesFromPoint.Count + edgesToPoint.Count > 5;
00154| 
00155| 
00156|                 if (EnoughEdges && !shouldRedo)
00157|                 //if (edgesFromPoint.Count + edgesToPoint.Count > 5 && edgesToPoint.Count + edgesFromPoint.Count < 7)
00158|                 {
00159|                     if (edgesWithPointList.Count > 0)
00160|                     {
00161|                         var trisWithEdge = baseTris.Where(tri => tri.Points.Contains(edgesWithPointList[0].P) && tri.Points.Contains(edgesWithPointList[0].Q));
00162|                         var tempTris1 = trisWithEdge.ElementAt(0);
00163|                         var tempTris2 = trisWithEdge.ElementAt(1);
00164|                         alteredIndex = tempTris1.Index;
00165|                         var points1 = tempTris1.Points;
00166|                         var points2 = tempTris2.Points;
00167| 
00168|                         var sharedEdge = edgesWithPointList[0];
00169|                         var sharedPoint1 = sharedEdge.Q;
00170|                         var sharedPoint2 = sharedEdge.P;
00171|                         var t1UnsharedPoint = tempTris1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
00172|                         var t2UnsharedPoint = tempTris2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
00173|                         var sharedEdgeLength = (sharedEdge.P.ToVector3() - sharedEdge.Q.ToVector3()).Length();
00174|                         var newEdgeLength = (t1UnsharedPoint.ToVector3() - t2UnsharedPoint.ToVector3()).Length();
00175|                         if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > OptimalSideLength / .5f)
00176|                         {
00177|                             i--;
00178|                             continue;
00179|                         }
00180|                         points1[0] = sharedPoint1;
00181|                         points1[2] = t1UnsharedPoint;
00182|                         points1[1] = t2UnsharedPoint;
00183| 
00184|                         points2[0] = sharedPoint2;
00185|                         points2[1] = t1UnsharedPoint;
00186|                         points2[2] = t2UnsharedPoint;
00187|                         sharedEdge.Q = t1UnsharedPoint;
00188|                         sharedEdge.P = t2UnsharedPoint;
00189| 
00190|                         //var index = tempTris1.Index;
00191|                         //tempTris1.Index = tempTris2.Index;
00192|                         //tempTris2.Index = index;
00193| 
00194|                         var otherEdgesT1 = tempTris1.Edges.Where(e => e != sharedEdge).ToList();
00195|                         otherEdgesT1[0].P = sharedPoint1;
00196|                         otherEdgesT1[0].Q = t1UnsharedPoint;
00197|                         otherEdgesT1[1].Q = sharedPoint1;
00198|                         otherEdgesT1[1].P = t2UnsharedPoint;
00199| 
00200|                         var otherEdgesT2 = tempTris2.Edges.Where(e => e != sharedEdge).ToList();
00201|                         otherEdgesT2[0].Q = sharedPoint2;
00202|                         otherEdgesT2[0].P = t2UnsharedPoint;
00203|                         otherEdgesT2[1].P = sharedPoint2;
00204|                         otherEdgesT2[1].Q = t1UnsharedPoint;
00205|                     }
00206|                 }
00207|                 else
00208|                 {
00209|                     i--;
00210|                     continue;
00211|                 }
00212|             }
00213| 
00214| 
00215|             GD.Print("Relaxing");
00216|             for (int index = 0; index < 12; index++)
00217|             {
00218|                 foreach (Point p in VertexPoints)
00219|                 {
00220|                     var trianglesWithPoint = baseTris.Where(t => t.Points.Contains(p));
00221|                     Vector3 average = new Vector3(0, 0, 0);
00222|                     foreach (Triangle t in trianglesWithPoint)
00223|                     {
00224|                         Vector3 triCenter = new Vector3(0, 0, 0);
00225|                         triCenter = t.Points[0].ToVector3();
00226|                         triCenter += t.Points[1].ToVector3();
00227|                         triCenter += t.Points[2].ToVector3();
00228|                         triCenter /= 3f;
00229|                         average += triCenter;
00230|                     }
00231|                     average /= trianglesWithPoint.ToList().Count;
00232|                     p.Position = average;
00233|                     var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
00234|                 }
00235|             }
00236|         }
00237| 
00238|         GD.Print("Triangulating");
00239|         foreach (Point p in VertexPoints)
00240|         {
00241|             //Find all triangles that contain the current point
00242|             var triangleOfP = baseTris.Where(e => e.Points.Any(a => a == p));
00243|             List<Triangle> trianglesWithPoint = triangleOfP.ToList();
00244|             List<Point> triCircumcenters = new List<Point>();
00245|             foreach (var tri in trianglesWithPoint)
00246|             {
00247|                 var v3 = tri.Points.ToVectors3();
00248|                 var ac = v3[2] - v3[0];
00249|                 var ab = v3[1] - v3[0];
00250|                 var abXac = ab.Cross(ac);
00251|                 var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
00252|                 float circumsphereRadius = vToCircumsphereCenter.Length();
00253|                 var cc = v3[0] + vToCircumsphereCenter;
00254|                 if (triCircumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc))) continue;
00255|                 if (circumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc)))
00256|                 {
00257|                     var usedCC = circumcenters.Where(cir => Mathf.Equals(cir.ToVector3(), cc));
00258|                     triCircumcenters.Add(usedCC.ElementAt(0));
00259|                 }
00260|                 else
00261|                 {
00262|                     circumcenters.Add(new Point(cc, circumcenters.Count));
00263|                     triCircumcenters.Add(circumcenters[circumcenters.Count - 1]);
00264|                 }
00265|             }
00266|             Face dualFace = new Face(triCircumcenters.ToList());
00267|             dualFaces.Add(dualFace);
00268|             Vector3 center = new Vector3(0, 0, 0);
00269|             for (int i = 0; i < triCircumcenters.Count; i++) center += triCircumcenters[i].ToVector3();
00270|             center /= triCircumcenters.Count;
00271|             center = center.Normalized();
00272| 
00273|             var centroid = new Vector3(0, 0, 0);
00274|             var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
00275|             var v2 = triCircumcenters[2].ToVector3() - triCircumcenters[0].ToVector3();
00276|             var UnitNorm = v1.Cross(v2);
00277|             UnitNorm = UnitNorm.Normalized();
00278|             if (UnitNorm.Dot(triCircumcenters[0].ToVector3()) < 0f)
00279|             {
00280|                 UnitNorm = -UnitNorm;
00281|             }
00282|             VoronoiCell calculated = TriangulatePoints(UnitNorm, triCircumcenters, circumcenters, VoronoiCells.Count);
00283|             calculated.IsBorderTile = false;
00284|             if (calculated != null)
00285|             {
00286|                 VoronoiCells.Add(calculated);
00287|             }
00288|         }
00289| 
00290|         /*Edge testEdge = generatedEdges[0];
00291|         GD.Print($"Number of Edges: {generatedEdges.Count}, Number of World Edges: {worldEdgeMap.Count}");
00292|         foreach (var edges in generatedEdges)
00293|         {
00294|             if ((edges.P == testEdge.P && edges.Q == testEdge.Q) || (edges.P == testEdge.Q && edges.Q == testEdge.P))
00295|             {
00296|                 GD.Print($"Original Edge {testEdge} has duplicate edge {edges}");
00297|             }
00298|         }*/
00299|         var continents = FloodFillContinentGeneration(VoronoiCells);
00300|         foreach (VoronoiCell vc in VoronoiCells)
00301|         {
00302|             var cellNeighbors = GetCellNeighbors(VoronoiCells, vc.Index);
00303|             float averageHeight = vc.Height;
00304|             List<Edge> OutsideEdges = new List<Edge>();
00305|             List<int> BoundingContinentIndex = new List<int>();
00306|             foreach (int neighbor in cellNeighbors)
00307|             {
00308|                 //GD.Print($"VC: {vc.Index}, {vc.ContinentIndex} | Neighbor: {VoronoiCells[neighbor].Index}, {VoronoiCells[neighbor].ContinentIndex}");
00309|                 //GD.Print($"Continent: {continents[vc.ContinentIndex]}, Boundary Cells: {continents[vc.ContinentIndex].boundaryCells}");
00310|                 if (VoronoiCells[neighbor].ContinentIndex != vc.ContinentIndex)
00311|                 {
00312|                     vc.IsBorderTile = true;
00313|                     continents[vc.ContinentIndex].boundaryCells.Add(vc);
00314|                     continents[vc.ContinentIndex].neighborContinents.Add(VoronoiCells[neighbor].ContinentIndex);
00315|                     continents[VoronoiCells[neighbor].ContinentIndex].neighborContinents.Add(vc.ContinentIndex);
00316|                     VoronoiCells[neighbor].IsBorderTile = true;
00317|                     BoundingContinentIndex.Add(VoronoiCells[neighbor].ContinentIndex);
00318|                     foreach (Point p in vc.Points)
00319|                     {
00320|                         foreach (Point p2 in VoronoiCells[neighbor].Points)
00321|                         {
00322|                             if (p.Equals(p2))
00323|                             {
00324|                                 p.continentBorder = true;
00325|                             }
00326|                         }
00327|                     }
00328|                     foreach (Edge e in vc.Edges)
00329|                     {
00330|                         foreach (Edge e2 in VoronoiCells[neighbor].Edges)
00331|                         {
00332|                             if (e.Equals(e2) || e.ReverseEdge().Equals(e2))
00333|                             {
00334|                                 OutsideEdges.Add(e);
00335|                             }
00336|                         }
00337|                     }
00338| 
00339|                 }
00340|                 averageHeight += VoronoiCells[neighbor].Height;
00341|             }
00342|             vc.BoundingContinentIndex = BoundingContinentIndex.ToArray();
00343|             vc.OutsideEdges = OutsideEdges.ToArray();
00344|             vc.Height = averageHeight / (cellNeighbors.Length + 1);
00345|             foreach (Point p in vc.Points)
00346|             {
00347|                 p.Height = vc.Height;
00348|             }
00349|         }
00350| 
00351|         GD.Print($"Number of Cells in mesh: {VoronoiCells.Count}");
00352|         GD.Print("Generating Mesh");
00353|         GD.Print($"Cell Height: {circumcenters[0].Height}");
00354|         //GenerateSurfaceMesh(VoronoiCells, circumcenters);
00355|         foreach (Point p in circumcenters)
00356|         {
00357|             var trianglesWithPoint = generatedTris.Where(t => t.Points.Contains(p));
00358|             float height = p.Height;
00359|             int counter = 1;
00360|             foreach (Triangle t in trianglesWithPoint)
00361|             {
00362|                 foreach (Point p2 in t.Points)
00363|                 {
00364|                     height += p2.Height;
00365|                     counter++;
00366|                 }
00367|             }
00368|             height /= counter;
00369|             p.Height = height;
00370|             var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
00371|         }
00372|         CalculateBoundaryStress(continents, VoronoiCells, circumcenters);
00373|         ApplyStressToTerrain(continents, VoronoiCells);
00374|         AssignBiomes(continents, VoronoiCells);
00375|         GenerateFromContinents(continents, circumcenters);
00376|         DrawContinentBorders(continents);
00377|         foreach (var continent in continents)
00378|         {
00379|             GD.Print(continent.Value.ToString());
00380|         }
00381| 
00382|     }
00383| 
00384|     private void AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
00385|     {
00386|         foreach (var continent in continents)
00387|         {
00388|             Continent c = continent.Value;
00389|             c.averageMoisture = BiomeAssigner.CalculateMoisture(c, rand, 0.5f);
00390|             foreach (var cell in c.cells)
00391|             {
00392|                 cell.Biome = BiomeAssigner.AssignBiome(cell.Height, c.averageMoisture);
00393|             }
00394|         }
00395|     }
00396| 
00397|     public void CalculateBoundaryStress(Dictionary<int, Continent> continents, List<VoronoiCell> cells, List<Point> points)
00398|     {
00399|         // Initialize stress fields for each continent
00400|         foreach (KeyValuePair<int, Continent> continentPair in continents)
00401|         {
00402|             int continentIndex = continentPair.Key;
00403|             Continent continent = continentPair.Value;
00404| 
00405|             // Reset stress accumulation for this continent
00406|             continent.stressAccumulation = 0f;
00407|             continent.neighborStress = new Dictionary<int, float>();
00408|             continent.boundaryTypes = new Dictionary<int, Continent.BOUNDARY_TYPE>();
00409| 
00410|             // Save updated continent back to dictionary
00411|             continents[continentIndex] = continent;
00412|         }
00413| 
00414|         // Calculate stress between neighboring continents
00415|         foreach (KeyValuePair<int, Continent> continentPair in continents)
00416|         {
00417|             int continentIndex = continentPair.Key;
00418|             Continent continent = continentPair.Value;
00419|             Vector3 continentCenter = continent.averagedCenter;
00420| 
00421|             // Find neighboring continents
00422|             foreach (int neighborIndex in continent.neighborContinents)
00423|             {
00424|                 Continent neighbor = continents[neighborIndex];
00425|                 Vector3 neighborCenter = neighbor.averagedCenter;
00426| 
00427|                 // Calculate relative movement vector
00428|                 Vector2 relativeMovement = neighbor.movementDirection - continent.movementDirection;
00429|                 float distance = (neighborCenter - continentCenter).Length();
00430| 
00431|                 // Normalize distance to a reasonable scale (assuming sphere radius is 'size')
00432|                 float normalizedDistance = distance / (2 * size);
00433| 
00434|                 // Calculate stress based on relative velocity and distance
00435|                 // This is a simplified model - you might want to adjust the formula
00436|                 float stress = relativeMovement.Length() / (normalizedDistance + 0.1f); // Add small value to avoid division by zero
00437| 
00438|                 // Determine boundary type based on relative movement
00439|                 Continent.BOUNDARY_TYPE boundaryType;
00440|                 float dotProduct = continent.movementDirection.Dot(neighbor.movementDirection);
00441| 
00442|                 if (dotProduct > 0.5f) // Moving in similar directions
00443|                 {
00444|                     boundaryType = Continent.BOUNDARY_TYPE.Transform;
00445|                 }
00446|                 else if (dotProduct < -0.5f) // Moving towards each other
00447|                 {
00448|                     boundaryType = Continent.BOUNDARY_TYPE.Divergent;
00449|                 }
00450|                 else // Moving apart or perpendicular
00451|                 {
00452|                     boundaryType = Continent.BOUNDARY_TYPE.Convergent;
00453|                 }
00454| 
00455|                 // Update continent's stress and boundary type dictionaries
00456|                 continent.neighborStress[neighborIndex] = stress * 2.0f;
00457|                 continent.boundaryTypes[neighborIndex] = boundaryType;
00458| 
00459|                 // Accumulate total stress
00460|                 continent.stressAccumulation += stress;
00461|             }
00462| 
00463|             // Save updated continent back to dictionary
00464|             continents[continentIndex] = continent;
00465|         }
00466|     }
00467| 
00468|     public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
00469|     {
00470|         // First, apply stress directly to boundary cells
00471|         foreach (KeyValuePair<int, Continent> continentPair in continents)
00472|         {
00473|             int continentIndex = continentPair.Key;
00474|             Continent continent = continentPair.Value;
00475| 
00476|             // Process each boundary cell of this continent
00477|             foreach (VoronoiCell boundaryCell in continent.boundaryCells)
00478|             {
00479|                 // For each neighboring continent that this boundary cell touches
00480|                 foreach (int neighborIndex in boundaryCell.BoundingContinentIndex)
00481|                 {
00482|                     // Check if this neighbor is actually a neighbor of the current continent
00483|                     if (continent.neighborContinents.Contains(neighborIndex))
00484|                     {
00485|                         // Get the boundary type and stress for this specific neighbor
00486|                         if (continent.boundaryTypes.ContainsKey(neighborIndex) &&
00487|                             continent.neighborStress.ContainsKey(neighborIndex))
00488|                         {
00489|                             Continent.BOUNDARY_TYPE boundaryType = continent.boundaryTypes[neighborIndex];
00490|                             float neighborStress = continent.neighborStress[neighborIndex];
00491| 
00492|                             // Apply height modification based on boundary type and stress
00493|                             switch (boundaryType)
00494|                             {
00495|                                 case Continent.BOUNDARY_TYPE.Convergent:
00496|                                     // Compressing - might create mountains or trenches
00497|                                     boundaryCell.Height += neighborStress * ConvergentBoundaryStrength;
00498|                                     break;
00499|                                 case Continent.BOUNDARY_TYPE.Divergent:
00500|                                     // Pulling apart - might create rifts
00501|                                     boundaryCell.Height -= neighborStress * DivergentBoundaryStrength; // Example multiplier
00502|                                     break;
00503|                                 case Continent.BOUNDARY_TYPE.Transform:
00504|                                     // Sliding past - might create fault lines
00505|                                     // Could add some noise or specific patterns
00506|                                     boundaryCell.Height += (float)rand.RandfRange(-0.1f, 0.1f) * neighborStress * TransformBoundaryStrength;
00507|                                     break;
00508|                             }
00509| 
00510|                             // Update the height of all points in this boundary cell
00511|                             foreach (Point p in boundaryCell.Points)
00512|                             {
00513|                                 p.Height = boundaryCell.Height;
00514|                             }
00515|                         }
00516|                     }
00517|                 }
00518|             }
00519|         }
00520| 
00521|         // Then, propagate stress from boundary cells to interior cells
00522|         foreach (KeyValuePair<int, Continent> continentPair in continents)
00523|         {
00524|             Continent continent = continentPair.Value;
00525| 
00526|             // Create a list of cells to process, starting with boundary cells
00527|             List<VoronoiCell> cellsToProcess = new List<VoronoiCell>(continent.boundaryCells);
00528|             HashSet<VoronoiCell> processedCells = new HashSet<VoronoiCell>();
00529|             Queue<VoronoiCell> processedCellsQueue = new Queue<VoronoiCell>();
00530| 
00531| 
00532|             // Process cells in layers, moving inward from the boundaries
00533|             while (cellsToProcess.Count > 0 || processedCellsQueue.Count > 0)
00534|             {
00535|                 GD.Print($"Cells to Process: {cellsToProcess.Count} | Processed Cells Queue: {processedCellsQueue.Count}");
00536|                 VoronoiCell currentCell = null;
00537|                 if (cellsToProcess.Count > 0)
00538|                 {
00539|                     currentCell = cellsToProcess[0];
00540|                     cellsToProcess.RemoveAt(0);
00541|                 }
00542|                 else
00543|                 {
00544|                     currentCell = processedCellsQueue.Dequeue();
00545|                 }
00546| 
00547|                 // Skip if already processed
00548|                 if (processedCells.Contains(currentCell))
00549|                     continue;
00550| 
00551|                 processedCells.Add(currentCell);
00552| 
00553|                 // Get neighboring cells within the same continent
00554|                 int[] neighborIndices = GetCellNeighbors(cells, currentCell.Index);
00555|                 foreach (int neighborIndex in neighborIndices)
00556|                 {
00557|                     VoronoiCell neighborCell = cells[neighborIndex];
00558| 
00559|                     // Only process neighbors that belong to the same continent and haven't been processed
00560|                     if (neighborCell.ContinentIndex == currentCell.ContinentIndex &&
00561|                                                                                   !processedCells.Contains(neighborCell))
00562|                     {
00563|                         // Calculate distance between cell centers
00564|                         float distance = (currentCell.Center - neighborCell.Center).Length();
00565| 
00566|                         // Propagate a fraction of the height difference based on distance
00567|                         // This creates a smoothing effect from boundaries inward
00568|                         float heightDifference = currentCell.Height - neighborCell.Height;
00569|                         float propagationFactor = 0.7f / (distance + 1.0f); // Adjust this factor as needed
00570|                         neighborCell.Height += heightDifference * propagationFactor;
00571| 
00572|                         // Add this neighbor to the processing queue
00573|                         cellsToProcess.Add(neighborCell);
00574| 
00575|                         // Update the height of all points in this neighbor cell
00576|                         foreach (Point p in neighborCell.Points)
00577|                         {
00578|                             p.Height = neighborCell.Height;
00579|                         }
00580|                     }
00581|                 }
00582|             }
00583|         }
00584|     }
00585| 
00586|     public int[] GetCellNeighbors(List<VoronoiCell> cells, int index)
00587|     {
00588|         var currentCell = cells[index];
00589|         HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
00590|         foreach (Point p in currentCell.Points)
00591|         {
00592|             var neighboringCells = cells.Where(vc => vc.Points.Any(vcp => vcp == p));
00593|             foreach (VoronoiCell vc in neighboringCells)
00594|             {
00595|                 neighbors.Add(vc);
00596|             }
00597|         }
00598|         List<int> neighborIndices = new List<int>();
00599|         foreach (VoronoiCell vc in neighbors)
00600|         {
00601|             neighborIndices.Add(vc.Index);
00602|         }
00603|         return neighborIndices.ToArray();
00604|     }
00605| 
00606|     public HashSet<int> GenerateStartingCells(List<VoronoiCell> cells)
00607|     {
00608|         HashSet<int> startingCells = new HashSet<int>();
00609|         while (startingCells.Count < NumContinents && startingCells.Count < cells.Count)
00610|         {
00611|             int position = rand.RandiRange(0, cells.Count - 1);
00612|             startingCells.Add(position);
00613|         }
00614|         return startingCells;
00615|     }
00616| 
00617|     public void DrawContinentBorders(Dictionary<int, Continent> continents)
00618|     {
00619|         Color[] colors = new Color[] {
00620|             Colors.Red,
00621|             Colors.Green,
00622|             Colors.Blue,
00623|             Colors.Yellow,
00624|             Colors.Pink,
00625|             Colors.Aqua,
00626|             Colors.White,
00627|             Colors.Gray,
00628|             Colors.Black,
00629|             Colors.DarkGray,
00630|             Colors.LightGray,
00631|             Colors.Purple,
00632|             Colors.Gold,
00633|             Colors.Orange,
00634|             Colors.Brown,
00635|             Colors.Maroon,
00636|             Colors.DeepPink,
00637|             Colors.RoyalBlue,
00638|             Colors.SteelBlue,
00639|             Colors.CornflowerBlue,
00640|             Colors.SkyBlue,
00641|             Colors.LightSteelBlue,
00642|             Colors.LightBlue,
00643|             Colors.PowderBlue,
00644|             Colors.CadetBlue,
00645|             Colors.MidnightBlue,
00646|             Colors.DarkBlue,
00647|             Colors.MediumBlue,
00648|             Colors.BlueViolet,
00649|             Colors.Indigo,
00650|             Colors.DarkOliveGreen,
00651|             Colors.CadetBlue,
00652|             Colors.SteelBlue,
00653|             Colors.CornflowerBlue,
00654|         };
00655|         foreach (var vc in continents)
00656|         {
00657|             var boundaries = vc.Value.boundaryCells;
00658|             foreach (var b in boundaries)
00659|             {
00660|                 if (b.IsBorderTile)
00661|                 {
00662|                     for (int i = 0; i < b.OutsideEdges.Length; i++)
00663|                     {
00664|                         Edge e1 = b.OutsideEdges[i];
00665|                         Point p1 = e1.P;
00666|                         Point p2 = e1.Q;
00667|                         Vector3 pos1 = p1.ToVector3().Normalized() * (size + p1.Height / 100f);
00668|                         Vector3 pos2 = p2.ToVector3().Normalized() * (size + p2.Height / 100f);
00669|                         PolygonRendererSDL.DrawLine(this, 1.005f, pos1, pos2, Colors.Black);
00670|                     }
00671|                 }
00672|             }
00673|         }
00674|     }
00675| 
00676|     public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
00677|     {
00678|         Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
00679|         HashSet<int> startingCells = GenerateStartingCells(cells);
00680|         var queue = startingCells.ToList();
00681|         int[] neighborChart = new int[cells.Count];
00682|         for (int i = 0; i < neighborChart.Length; i++)
00683|         {
00684|             neighborChart[i] = -1;
00685|         }
00686|         foreach (int i in startingCells)
00687|         {
00688|             var continent = new Continent(i,
00689|                     new List<VoronoiCell>(),
00690|                     new HashSet<VoronoiCell>(),
00691|                     new HashSet<Point>(),
00692|                     new List<Point>(),
00693|                     new Vector3(0f, 0f, 0f),
00694|                     new Vector3(0f, 0f, 0f),
00695|                     new Vector3(0f, 0f, 0f),
00696|                     new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)), rand.RandiRange(-360, 360),
00697|                     Continent.CRUST_TYPE.Continental, rand.RandfRange(-10f, 10f), rand.RandfRange(1.0f, 5.0f),
00698|                     new HashSet<int>(), 0f,
00699|                     new Dictionary<int, float>(),
00700|                     new Dictionary<int, Continent.BOUNDARY_TYPE>());
00701|             neighborChart[i] = i;
00702|             continent.StartingIndex = i;
00703|             continent.cells.Add(cells[i]);
00704|             foreach (Point p in cells[i].Points)
00705|             {
00706|                 continent.points.Add(p);
00707|             }
00708|             continents[i] = continent;
00709| 
00710|             var neighborIndices = GetCellNeighbors(cells, i);
00711|             foreach (var nb in neighborIndices)
00712|             {
00713|                 if (neighborChart[nb] == -1)
00714|                 {
00715|                     neighborChart[nb] = neighborChart[i];
00716|                     queue.Add(nb);
00717|                 }
00718|             }
00719|         }
00720|         testIndex = queue[0];
00721|         for (int i = 0; i < queue.Count; i++)
00722|         {
00723|             var pos = rand.RandiRange(i, (queue.Count - 1));
00724|             var currentVCell = queue[pos];
00725|             queue[pos] = queue[i];
00726|             var neighborIndices = GetCellNeighbors(cells, currentVCell);
00727|             foreach (var nb in neighborIndices)
00728|             {
00729|                 if (neighborChart[nb] == -1)
00730|                 {
00731|                     neighborChart[nb] = neighborChart[currentVCell];
00732|                     queue.Add(nb);
00733|                 }
00734|             }
00735|         }
00736| 
00737|         for (int i = 0; i < neighborChart.Length; i++)
00738|         {
00739|             if (neighborChart[i] != -1)
00740|             {
00741|                 var continent = continents[neighborChart[i]];
00742|                 continent.cells.Add(cells[i]);
00743|                 cells[i].Height = continent.averageHeight;
00744|                 cells[i].ContinentIndex = continent.StartingIndex;
00745|                 foreach (Point p in cells[i].Points)
00746|                 {
00747|                     p.Position = p.Position.Normalized();
00748|                     p.Height = continent.averageHeight;
00749|                     continent.points.Add(p);
00750|                 }
00751|             }
00752|         }
00753| 
00754|         foreach (var keyValuePair in continents)
00755|         {
00756|             var continent = keyValuePair.Value;
00757|             GD.Print($"StartingIndex: {continent.StartingIndex}");
00758|             foreach (Point p in continent.points)
00759|             {
00760|                 continent.averagedCenter += p.Position;
00761|             }
00762| 
00763|             continent.averagedCenter /= continent.points.Count;
00764|             continent.averagedCenter = continent.averagedCenter.Normalized();
00765|             var v1 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
00766|             var v2 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
00767|             var UnitNorm = v1.Cross(v2);
00768|             //UnitNorm = UnitNorm / size;
00769|             if (UnitNorm.Dot(continent.averagedCenter) < 0f)
00770|             {
00771|                 UnitNorm = -UnitNorm;
00772|             }
00773|             //UnitNorm *= (float)size;
00774|             var uAxis = v1;// * (float)size;
00775|             var vAxis = UnitNorm.Cross(uAxis);// * (float)size;
00776|             uAxis = uAxis.Normalized();
00777|             vAxis = vAxis.Normalized();
00778|             foreach (VoronoiCell vc in continent.cells)
00779|             {
00780|                 Vector3 average = Vector3.Zero;
00781|                 foreach (Point p in vc.Points)
00782|                 {
00783|                     average += p.Position;
00784|                 }
00785|                 average /= vc.Points.Length;
00786|                 average = average.Normalized();
00787|                 float radius = (continent.averagedCenter - average).Length();
00788|                 vc.MovementDirection = continent.movementDirection + new Vector2(radius * Mathf.Cos(continent.rotation), radius * Mathf.Sin(continent.rotation));
00789|                 //Find Plane Equation
00790|                 var vcRadius = (average - vc.Points[0].ToVector3().Normalized()).Length() * .9f;
00791|                 var vcUnitNorm = v1.Cross(v2);
00792|                 var projectionRatio = (uAxis - UnitNorm).Length() / vcRadius;
00793|                 vcUnitNorm /= projectionRatio;
00794|                 vcUnitNorm = vcUnitNorm.Normalized();
00795| 
00796| 
00797|                 var d = UnitNorm.X * (vc.Points[0].X) + UnitNorm.Y * (vc.Points[0].Y) + UnitNorm.Z * (vc.Points[0].Z);
00798|                 var newZ = (d - (UnitNorm.X * vc.Points[0].X) - (UnitNorm.Y * vc.Points[0].Y)) / UnitNorm.Z;
00799|                 var newZ2 = (d / UnitNorm.Z);
00800|                 //GD.Print($"Movement Direction: {vc.MovementDirection}");
00801|                 var directionPoint = uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
00802|                 directionPoint *= vcRadius;
00803|                 directionPoint += average;
00804|                 directionPoint = directionPoint.Normalized();
00805| 
00806| 
00807|                 //GD.Print($"Plane Equation for {vc} is {d} = {UnitNorm.X}a + {UnitNorm.Y}b + {UnitNorm.Z}c");
00808| 
00809|                 PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), average, directionPoint, UnitNorm, vcRadius, Colors.Black);
00810| 
00811| 
00812|                 //}
00813|             }
00814|         }
00815| 
00816|         return continents;
00817|     }
00818| 
00819|     public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, List<Point> TrueCircumcenters, int index)
00820|     {
00821|         var u = new Vector3(0, 0, 0);
00822|         if (!Mathf.Equals(unitNorm.X, 0.0f))
00823|         {
00824|             u = new Vector3(-unitNorm.Y, unitNorm.X, 0.0f);
00825|         }
00826|         else if (!Mathf.Equals(unitNorm.Y, 0.0f))
00827|         {
00828|             u = new Vector3(-unitNorm.Z, 0, unitNorm.Y);
00829|         }
00830|         else
00831|         {
00832|             u = new Vector3(1, 0, 0);
00833|         }
00834|         u = u.Normalized();
00835|         var v = unitNorm.Cross(u);
00836| 
00837|         List<Point> projectedPoints = new List<Point>();
00838|         var ccs = TriCircumcenters.ToVectors3();
00839|         for (int i = 0; i < TriCircumcenters.Count; i++)
00840|         {
00841|             var projection = new Vector2((ccs[i] - ccs[0]).Dot(u), (ccs[i] - ccs[0]).Dot(v));
00842|             projectedPoints.Add(new Point(new Vector3(projection.X, projection.Y, 0.0f), TriCircumcenters[i].Index));
00843|         }
00844| 
00845|         //Order List of 2D points in clockwise order
00846|         var orderedPoints = ReorderPoints(projectedPoints);
00847|         var orderedPointsReversed = new List<Point>(orderedPoints);
00848|         orderedPointsReversed.Reverse();
00849| 
00850|         List<Point> TriangulatedIndices = new List<Point>();
00851|         List<Triangle> Triangles = new List<Triangle>();
00852|         HashSet<Edge> CellEdges = new HashSet<Edge>();
00853|         Edge[] triEdges;
00854|         Point v1, v2, v3;
00855|         Vector3 v1Tov2, v1Tov3, triangleCrossProduct;
00856|         float angleTriangleFace;
00857|         while (orderedPoints.Count > 3)
00858|         {
00859|             for (int i = 0; i < orderedPoints.Count; i++)
00860|             {
00861|                 var a = GetOrderedPoint(orderedPoints, i);
00862|                 var b = GetOrderedPoint(orderedPoints, i - 1);
00863|                 var c = GetOrderedPoint(orderedPoints, i + 1);
00864| 
00865|                 Vector3 tab = b.ToVector3() - a.ToVector3();
00866|                 Vector3 tac = c.ToVector3() - a.ToVector3();
00867|                 Vector2 ab = new Vector2(tab.X, tab.Y);
00868|                 Vector2 ac = new Vector2(tac.X, tac.Y);
00869| 
00870|                 if (ab.Cross(ac) < 0.0f)
00871|                 {
00872|                     continue;
00873|                 }
00874| 
00875|                 bool isEar = true;
00876|                 for (int j = 0; j < orderedPoints.Count; j++)
00877|                 {
00878|                     if (orderedPoints[j].Index == a.Index || orderedPoints[j].Index == b.Index || orderedPoints[j].Index == c.Index)
00879|                     {
00880|                         continue;
00881|                     }
00882|                     Vector2 p = new Vector2(orderedPoints[j].X, orderedPoints[j].Y);
00883|                     if (IsPointInTriangle(p, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), new Vector2(c.X, c.Y), false))
00884|                     {
00885|                         isEar = false;
00886|                         break;
00887|                     }
00888|                 }
00889|                 if (isEar)
00890|                 {
00891|                     //Take the 3 points in 3D space and generate the normal
00892|                     //If angle between triangle Normal and UnitNormal <90
00893|                     v1 = TrueCircumcenters[c.Index];
00894|                     v2 = TrueCircumcenters[a.Index];
00895|                     v3 = TrueCircumcenters[b.Index];
00896| 
00897|                     v1Tov2 = v2.ToVector3() - v1.ToVector3();
00898|                     v1Tov3 = v3.ToVector3() - v1.ToVector3();
00899| 
00900|                     triangleCrossProduct = v1Tov2.Cross(v1Tov3);
00901|                     triangleCrossProduct = triangleCrossProduct.Normalized();
00902|                     angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
00903|                     if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
00904|                     { //Inverse Winding
00905|                         triEdges = new Edge[3];
00906|                         if (!worldEdgeMap.TryGetValue((a.Index, b.Index), out triEdges[0]) && !worldEdgeMap.TryGetValue((b.Index, a.Index), out triEdges[0]))
00907|                         {
00908|                             triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[a.Index], TrueCircumcenters[b.Index]);
00909|                             worldEdgeMap.Add((a.Index, b.Index), triEdges[0]);
00910|                             generatedEdges.Add(triEdges[0]);
00911|                         }
00912|                         if (!worldEdgeMap.TryGetValue((c.Index, a.Index), out triEdges[1]) && !worldEdgeMap.TryGetValue((a.Index, c.Index), out triEdges[1]))
00913|                         {
00914|                             triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[c.Index], TrueCircumcenters[a.Index]);
00915|                             worldEdgeMap.Add((c.Index, a.Index), triEdges[1]);
00916|                             generatedEdges.Add(triEdges[1]);
00917|                         }
00918|                         if (!worldEdgeMap.TryGetValue((b.Index, c.Index), out triEdges[2]) && !worldEdgeMap.TryGetValue((c.Index, b.Index), out triEdges[2]))
00919|                         {
00920|                             triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[b.Index], TrueCircumcenters[c.Index]);
00921|                             worldEdgeMap.Add((b.Index, c.Index), triEdges[2]);
00922|                             generatedEdges.Add(triEdges[2]);
00923|                         }
00924|                         Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[c.Index], TrueCircumcenters[a.Index], TrueCircumcenters[b.Index] }, triEdges.ToList()));
00925|                         TriangulatedIndices.Add(TrueCircumcenters[c.Index]);
00926|                         TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
00927|                         TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
00928|                         CellEdges.Add(triEdges[0]);
00929|                         CellEdges.Add(triEdges[1]);
00930|                         CellEdges.Add(triEdges[2]);
00931|                     }
00932|                     else
00933|                     {
00934|                         triEdges = new Edge[3];
00935|                         if (!worldEdgeMap.TryGetValue((b.Index, a.Index), out triEdges[0]) && !worldEdgeMap.TryGetValue((a.Index, b.Index), out triEdges[0]))
00936|                         {
00937|                             triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[b.Index], TrueCircumcenters[a.Index]);
00938|                             worldEdgeMap.Add((b.Index, a.Index), triEdges[0]);
00939|                             generatedEdges.Add(triEdges[0]);
00940|                         }
00941|                         if (!worldEdgeMap.TryGetValue((a.Index, c.Index), out triEdges[1]) && !worldEdgeMap.TryGetValue((c.Index, a.Index), out triEdges[1]))
00942|                         {
00943|                             triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[a.Index], TrueCircumcenters[c.Index]);
00944|                             worldEdgeMap.Add((a.Index, c.Index), triEdges[1]);
00945|                             generatedEdges.Add(triEdges[1]);
00946|                         }
00947|                         if (!worldEdgeMap.TryGetValue((c.Index, b.Index), out triEdges[2]) && !worldEdgeMap.TryGetValue((b.Index, c.Index), out triEdges[2]))
00948|                         {
00949|                             triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[c.Index], TrueCircumcenters[b.Index]);
00950|                             worldEdgeMap.Add((c.Index, b.Index), triEdges[2]);
00951|                             generatedEdges.Add(triEdges[2]);
00952|                         }
00953|                         Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[b.Index], TrueCircumcenters[a.Index], TrueCircumcenters[c.Index] }, triEdges.ToList()));
00954|                         TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
00955|                         TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
00956|                         TriangulatedIndices.Add(TrueCircumcenters[c.Index]);
00957|                         CellEdges.Add(triEdges[0]);
00958|                         CellEdges.Add(triEdges[1]);
00959|                         CellEdges.Add(triEdges[2]);
00960|                     }
00961| 
00962|                     orderedPoints.RemoveAt(i);
00963|                     break;
00964|                 }
00965|             }
00966|         }
00967|         v1 = TrueCircumcenters[orderedPoints[2].Index];
00968|         v2 = TrueCircumcenters[orderedPoints[1].Index];
00969|         v3 = TrueCircumcenters[orderedPoints[0].Index];
00970| 
00971|         v1Tov2 = v2.ToVector3() - v1.ToVector3();
00972|         v1Tov3 = v3.ToVector3() - v1.ToVector3();
00973| 
00974|         triangleCrossProduct = v1Tov2.Cross(v1Tov3);
00975|         triangleCrossProduct = triangleCrossProduct.Normalized();
00976|         angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
00977|         if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
00978|         {
00979|             triEdges = new Edge[3];
00980|             if (!worldEdgeMap.TryGetValue((orderedPoints[1].Index, orderedPoints[0].Index), out triEdges[0]) && !worldEdgeMap.TryGetValue((orderedPoints[0].Index, orderedPoints[1].Index), out triEdges[0]))
00981|             {
00982|                 triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[0].Index]);
00983|                 worldEdgeMap.Add((orderedPoints[1].Index, orderedPoints[0].Index), triEdges[0]);
00984|                 generatedEdges.Add(triEdges[0]);
00985|             }
00986|             if (!worldEdgeMap.TryGetValue((orderedPoints[2].Index, orderedPoints[1].Index), out triEdges[1]) && !worldEdgeMap.TryGetValue((orderedPoints[1].Index, orderedPoints[2].Index), out triEdges[1]))
00987|             {
00988|                 triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[1].Index]);
00989|                 worldEdgeMap.Add((orderedPoints[2].Index, orderedPoints[1].Index), triEdges[1]);
00990|                 generatedEdges.Add(triEdges[1]);
00991|             }
00992|             if (!worldEdgeMap.TryGetValue((orderedPoints[0].Index, orderedPoints[2].Index), out triEdges[2]) && !worldEdgeMap.TryGetValue((orderedPoints[2].Index, orderedPoints[0].Index), out triEdges[2]))
00993|             {
00994|                 triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[2].Index]);
00995|                 worldEdgeMap.Add((orderedPoints[0].Index, orderedPoints[2].Index), triEdges[2]);
00996|                 generatedEdges.Add(triEdges[2]);
00997|             }
00998|             Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[0].Index] }, triEdges.ToList()));
00999|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[2].Index]);
01000|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[1].Index]);
01001|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[0].Index]);
01002|             CellEdges.Add(triEdges[0]);
01003|             CellEdges.Add(triEdges[1]);
01004|             CellEdges.Add(triEdges[2]);
01005|         }
01006|         else
01007|         {
01008|             triEdges = new Edge[3];
01009|             if (!worldEdgeMap.TryGetValue((orderedPoints[0].Index, orderedPoints[1].Index), out triEdges[0]) && !worldEdgeMap.TryGetValue((orderedPoints[1].Index, orderedPoints[0].Index), out triEdges[0]))
01010|             {
01011|                 triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[1].Index]);
01012|                 worldEdgeMap.Add((orderedPoints[0].Index, orderedPoints[1].Index), triEdges[0]);
01013|                 generatedEdges.Add(triEdges[0]);
01014|             }
01015|             if (!worldEdgeMap.TryGetValue((orderedPoints[1].Index, orderedPoints[2].Index), out triEdges[1]) && !worldEdgeMap.TryGetValue((orderedPoints[2].Index, orderedPoints[1].Index), out triEdges[1]))
01016|             {
01017|                 triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[2].Index]);
01018|                 worldEdgeMap.Add((orderedPoints[1].Index, orderedPoints[2].Index), triEdges[1]);
01019|                 generatedEdges.Add(triEdges[1]);
01020|             }
01021|             if (!worldEdgeMap.TryGetValue((orderedPoints[2].Index, orderedPoints[0].Index), out triEdges[2]) && !worldEdgeMap.TryGetValue((orderedPoints[0].Index, orderedPoints[2].Index), out triEdges[2]))
01022|             {
01023|                 triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[0].Index]);
01024|                 worldEdgeMap.Add((orderedPoints[2].Index, orderedPoints[0].Index), triEdges[2]);
01025|                 generatedEdges.Add(triEdges[2]);
01026|             }
01027|             Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[2].Index] }, triEdges.ToList()));
01028|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[0].Index]);
01029|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[1].Index]);
01030|             TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[2].Index]);
01031|             CellEdges.Add(triEdges[0]);
01032|             CellEdges.Add(triEdges[1]);
01033|             CellEdges.Add(triEdges[2]);
01034|         }
01035| 
01036|         generatedTris.AddRange(Triangles);
01037|         VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), CellEdges.ToArray());
01038|         return GeneratedCell;
01039|     }
01040| 
01041|     public bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, bool reversed)
01042|     {
01043|         var ab = b - a;
01044|         var bc = c - b;
01045|         var ca = a - c;
01046| 
01047|         var ap = p - a;
01048|         var bp = p - b;
01049|         var cp = p - c;
01050| 
01051|         if (reversed)
01052|         {
01053| 
01054|             if (ab.Cross(ap) < 0f || bc.Cross(bp) < 0f || ca.Cross(cp) < 0f)
01055|             {
01056|                 return false;
01057|             }
01058|         }
01059|         else
01060|         {
01061|             if (ab.Cross(ap) > 0f || bc.Cross(bp) > 0f || ca.Cross(cp) > 0f)
01062|             {
01063|                 return false;
01064|             }
01065|         }
01066|         return true;
01067|     }
01068| 
01069|     public Point GetOrderedPoint(List<Point> points, int index)
01070|     {
01071|         if (index >= points.Count)
01072|         {
01073|             return points[index % points.Count];
01074|         }
01075|         else if (index < 0)
01076|         {
01077|             return points[index % points.Count + points.Count];
01078|         }
01079|         else
01080|         {
01081|             return points[index];
01082|         }
01083|     }
01084| 
01085|     public List<Point> ReorderPoints(List<Point> points)
01086|     {
01087|         var average = new Vector3(0, 0, 0);
01088|         foreach (Point p in points)
01089|         {
01090|             average += p.ToVector3();
01091|         }
01092|         average /= points.Count;
01093|         var center = new Vector2(average.X, average.Y);
01094|         //if (center.X <= 0f) { shouldInvertX = true; }
01095|         List<Point> orderedPoints = new List<Point>();
01096|         for (int i = 0; i < points.Count; i++)
01097|         {
01098|             orderedPoints.Add(new Point(new Vector3(points[i].X, points[i].Y, less(center, new Vector2(points[i].X, points[i].Y))), points[i].Index));
01099|         }
01100|         orderedPoints = orderedPoints.OrderBy(p => p.Z).ToList();
01101|         for (int i = 0; i < orderedPoints.Count; i++)
01102|         {
01103|             points[i] = new Point(new Vector3(orderedPoints[i].X, orderedPoints[i].Y, 0.0f), orderedPoints[i].Index);
01104|         }
01105|         return points;
01106|     }
01107|     public float less(Vector2 center, Vector2 a)
01108|     {
01109|         float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.X - center.X, a.Y - center.Y)) + 360) % 360;
01110|         return a1;
01111|     }
01112| 
01113|     public void RenderTriangleAndConnections(Triangle tri)
01114|     {
01115|         int i = 0;
01116|         foreach (var p in tri.Points)
01117|         {
01118|             //GD.Print($"{p.ToVector3()}");
01119|         }
01120|         foreach (Edge e in tri.Edges)
01121|         {
01122|             //GD.Print(e);
01123|         }
01124|         var edgesFromTri = baseEdges.Where(e => tri.Points.Any(a => a == e.P || a == e.Q));
01125|         if (!ProjectToSphere)
01126|         {
01127|             PolygonRendererSDL.DrawLine(this, size, tri.Points[0].ToVector3(), tri.Points[1].ToVector3());
01128|             PolygonRendererSDL.DrawLine(this, size, tri.Points[1].ToVector3(), tri.Points[2].ToVector3());
01129|             PolygonRendererSDL.DrawLine(this, size, tri.Points[2].ToVector3(), tri.Points[0].ToVector3());
01130|             foreach (Point p in tri.Points)
01131|             {
01132|                 //GD.Print(p);
01133|                 //GD.Print(p.ToVector3());
01134|                 switch (i)
01135|                 {
01136|                     case 0:
01137|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Red);
01138|                         break;
01139|                     case 1:
01140|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Green);
01141|                         break;
01142|                     case 2:
01143|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Blue);
01144|                         break;
01145|                 }
01146|                 i++;
01147|             }
01148|         }
01149|         else
01150|         {
01151|             PolygonRendererSDL.DrawLine(this, size, tri.Points[0].ToVector3().Normalized(), tri.Points[1].ToVector3().Normalized());
01152|             PolygonRendererSDL.DrawLine(this, size, tri.Points[1].ToVector3().Normalized(), tri.Points[2].ToVector3().Normalized());
01153|             PolygonRendererSDL.DrawLine(this, size, tri.Points[2].ToVector3().Normalized(), tri.Points[0].ToVector3().Normalized());
01154|             foreach (Point p in tri.Points)
01155|             {
01156|                 //GD.Print(p);
01157|                 //GD.Print(p.ToVector3());
01158|                 switch (i)
01159|                 {
01160|                     case 0:
01161|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Red);
01162|                         break;
01163|                     case 1:
01164|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Green);
01165|                         break;
01166|                     case 2:
01167|                         PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Blue);
01168|                         break;
01169|                 }
01170|                 i++;
01171|             }
01172|         }
01173|         foreach (var edge in edgesFromTri)
01174|         {
01175|             //GD.Print($"Edge: {edge.Index} from {edge.P.ToVector3()} to {edge.Q.ToVector3()}");
01176|         }
01177| 
01178| 
01179|         //foreach(Triangle tri in tris) {
01180| 
01181|         //}
01182|         /*foreach (Edge edge in edges)
01183|         {
01184|             foreach (var p in tri.Points)
01185|             {
01186|                 if (edge.P.Index == p.Index || edge.Q.Index == p.Index)
01187|                 {
01188|                     if (ProjectToSphere)
01189|                         DrawLine(edge.P.ToVector3().Normalized(), edge.Q.ToVector3().Normalized());
01190|                     else
01191|                         DrawLine(edge.P.ToVector3(), edge.Q.ToVector3());
01192|                 }
01193|             }
01194|             //DrawLine(edge.P.ToVector3().Normalized(), edge.Q.ToVector3().Normalized());
01195| 
01196|         }*/
01197|     }
01198| 
01199|     public Vector3 ConvertToSpherical(Vector3 pos)
01200|     {
01201|         Vector3 sphere = new Vector3(
01202|             Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2)),
01203|             Mathf.Acos(pos.Z / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2))),
01204|             Mathf.Sign(pos.Y) * Mathf.Acos(pos.X / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2)))
01205|             );
01206|         return sphere;
01207|     }
01208| 
01209|     public Vector3 ConvertToCartesian(Vector3 sphere)
01210|     {
01211|         Vector3 cart = new Vector3(
01212|             sphere.X * Mathf.Sin(sphere.Y) * Mathf.Cos(sphere.Z),
01213|             sphere.X * Mathf.Sin(sphere.Y) * Mathf.Sin(sphere.Z),
01214|             sphere.X * Mathf.Cos(sphere.Y)
01215|             );
01216|         return cart;
01217|     }
01218| 
01219|     public void GenerateTriangleList()
01220|     {
01221|         foreach (Face f in faces)
01222|         {
01223|             //if(VertexPoints.Any(p => p.ToVector3() == tempVector)) {
01224|             //  var existingPoint = VertexPoints.Where(a => a.ToVector3() == tempVector);
01225|             //  return existingPoint.ElementAt(0);
01226|             //}
01227|             Edge[] triEdges = new Edge[3];
01228|             for (int i = 0, j = f.v.Length - 1; i < f.v.Length; j = i++)
01229|             {
01230|                 if (baseEdges.Any(e => e.P == f.v[j] && e.Q == f.v[i]))
01231|                 {
01232|                     triEdges[i] = baseEdges.Where(a => a.P == f.v[j] && a.Q == f.v[i]).ElementAt(0);
01233|                 }
01234|                 else if (baseEdges.Any(e => e.P == f.v[i] && e.Q == f.v[j]))
01235|                 {
01236|                     Edge e = baseEdges.Where(a => a.P == f.v[i] && a.Q == f.v[j]).ElementAt(0);
01237|                     e = e.ReverseEdge();
01238|                     triEdges[i] = e;
01239|                 }
01240|                 else
01241|                 {
01242|                     triEdges[i] = new Edge(baseEdges.Count, f.v[j], f.v[i]);
01243|                     baseEdges.Add(triEdges[i]);
01244|                 }
01245|             }
01246|             baseTris.Add(new Triangle(baseTris.Count, f.v.ToList(), triEdges.ToList()));
01247|         }
01248|     }
01249| 
01250|     public void GenerateNonDeformedFaces()
01251|     {
01252|         for (int i = 0; i < subdivide; i++)
01253|         {
01254|             var tempFaces = new List<Face>();
01255|             foreach (Face face in faces)
01256|             {
01257|                 tempFaces.AddRange(Subdivide(face));
01258|             }
01259|             faces = new List<Face>(tempFaces);
01260|         }
01261|     }
01262| 
01263|     public void GenerateFromContinents(Dictionary<int, Continent> continents, List<Point> circumcenters)
01264|     {
01265|         foreach (var keyValuePair in continents)
01266|         {
01267|             GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters);
01268|         }
01269|     }
01270| 
01271|     private Color GetVertexColor(float height)
01272|     {
01273|         // Single continuous formula without branching
01274|         // Height range: -10 (deep water) to +10 (mountains)
01275|         float normalizedHeight = (height + 10f) / 20f; // 0-1 range
01276| 
01277|         // Smooth color transition using mathematical functions
01278|         // Blue(220) -> Cyan(180) -> Green(120) -> Yellow(50) -> Brown(30) -> Dark Brown(10)
01279|         float hue = 220f - (210f * normalizedHeight);
01280| 
01281|         // Saturation curve: low for water, high for land
01282|         float saturation = 0.3f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi);
01283| 
01284|         // Value curve: darker for deep water and high mountains, brighter for land
01285|         float value = 0.4f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi * 0.8f + 0.2f);
01286| 
01287|         // Ensure values are within valid range
01288|         hue = Mathf.Clamp(hue, 0f, 360f);
01289|         saturation = Mathf.Clamp(saturation, 0.2f, 1f);
01290|         value = Mathf.Clamp(value, 0.2f, 1f);
01291| 
01292|         return Color.FromHsv(hue / 360f, saturation, value);
01293|     }
01294| 
01295|     private Color GetBiomeColor(VoronoiCell.BiomeType biome, float height)
01296|     {
01297|         switch (biome)
01298|         {
01299|             case VoronoiCell.BiomeType.Tundra:
01300|                 return new Color(0.85f, 0.85f, 0.8f); // Light gray-white
01301|             case VoronoiCell.BiomeType.Icecap:
01302|                 return Colors.White;
01303|             case VoronoiCell.BiomeType.Desert:
01304|                 return new Color(0.9f, 0.8f, 0.5f); // Sandy yellow
01305|             case VoronoiCell.BiomeType.Grassland:
01306|                 return new Color(0.5f, 0.8f, 0.3f); // Green
01307|             case VoronoiCell.BiomeType.Forest:
01308|                 return new Color(0.2f, 0.6f, 0.2f); // Dark green
01309|             case VoronoiCell.BiomeType.Rainforest:
01310|                 return new Color(0.1f, 0.4f, 0.1f); // Very dark green
01311|             case VoronoiCell.BiomeType.Taiga:
01312|                 return new Color(0.4f, 0.5f, 0.3f); // Dark green-brown
01313|             case VoronoiCell.BiomeType.Ocean:
01314|                 return new Color(0.1f, 0.3f, 0.7f); // Deep blue
01315|             case VoronoiCell.BiomeType.Coastal:
01316|                 return new Color(0.6f, 0.8f, 0.9f); // Light blue
01317|             case VoronoiCell.BiomeType.Mountain:
01318|                 return new Color(0.6f, 0.5f, 0.4f); // Brown-gray
01319|             default:
01320|                 return Colors.Gray;
01321|         }
01322|     }
01323| 
01324|     public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, List<Point> circumcenters)
01325|     {
01326|         RandomNumberGenerator randy = new RandomNumberGenerator();
01327|         //float height = averageHeight;
01328|         randy.Seed = Seed;
01329|         var arrMesh = Mesh as ArrayMesh;
01330|         var st = new SurfaceTool();
01331|         st.Begin(Mesh.PrimitiveType.Triangles);
01332|         var material = new StandardMaterial3D();
01333|         material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
01334|         material.VertexColorUseAsAlbedo = true;
01335|         //material.AlbedoColor = Colors.Pink;
01336|         st.SetMaterial(material);
01337|         foreach (VoronoiCell vor in VoronoiList)
01338|         {
01339|             Color biomeColor = Colors.Pink;
01340|             if (ShouldDisplayBiomes) {
01341|                 biomeColor = GetBiomeColor(vor.Biome, vor.Height);
01342|             }
01343|             for (int i = 0; i < vor.Points.Length / 3; i++)
01344|             {
01345|                 var centroid = Vector3.Zero;
01346|                 centroid += vor.Points[3 * i].ToVector3();
01347|                 centroid += vor.Points[3 * i + 1].ToVector3();
01348|                 centroid += vor.Points[3 * i + 2].ToVector3();
01349|                 centroid /= 3.0f;
01350| 
01351|                 var normal = (vor.Points[3 * i + 1].ToVector3() - vor.Points[3 * i].ToVector3()).Cross(vor.Points[3 * i + 2].ToVector3() - vor.Points[3 * i].ToVector3()).Normalized();
01352|                 var tangent = (vor.Points[3 * i].ToVector3() - centroid).Normalized();
01353|                 var bitangent = normal.Cross(tangent).Normalized();
01354|                 var min_u = Mathf.Inf;
01355|                 var min_v = Mathf.Inf;
01356|                 var max_u = -Mathf.Inf;
01357|                 var max_v = -Mathf.Inf;
01358|                 for (int j = 0; j < 3; j++)
01359|                 {
01360|                     var rel_pos = vor.Points[3 * i + j].ToVector3() - centroid;
01361|                     var u = rel_pos.Dot(tangent);
01362|                     var v = rel_pos.Dot(bitangent);
01363|                     min_u = Mathf.Min(min_u, u);
01364|                     min_v = Mathf.Min(min_v, v);
01365|                     max_u = Mathf.Max(max_u, u);
01366|                     max_v = Mathf.Max(max_v, v);
01367| 
01368|                     var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
01369|                     st.SetUV(uv);
01370|                     //st.SetNormal(tangent);
01371|                     if (ProjectToSphere)
01372|                     {
01373|                         st.SetColor(biomeColor == Colors.Pink ? GetVertexColor(vor.Points[3 * i + j].Height): biomeColor);
01374|                         st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 100f));
01375|                     }
01376|                     else
01377|                     {
01378|                         st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 10f));
01379|                     }
01380|                 }
01381|             }
01382|         }
01383|         st.GenerateNormals();
01384|         st.Commit(arrMesh);
01385|     }
01386| 
01387|     public Face[] Subdivide(Face face)
01388|     {
01389|         List<Face> subfaces = new List<Face>();
01390|         var subVector1 = GetMiddle(face.v[0], face.v[1]);
01391|         var subVector2 = GetMiddle(face.v[1], face.v[2]);
01392|         var subVector3 = GetMiddle(face.v[2], face.v[0]);
01393|         //VertexPoints.Add(subVector1);
01394|         //VertexPoints.Add(subVector2);
01395|         //VertexPoints.Add(subVector3);
01396| 
01397|         subfaces.Add(new Face(face.v[0], subVector1, subVector3));
01398|         subfaces.Add(new Face(subVector1, face.v[1], subVector2));
01399|         subfaces.Add(new Face(subVector2, face.v[2], subVector3));
01400|         subfaces.Add(new Face(subVector3, subVector1, subVector2));
01401| 
01402|         return subfaces.ToArray();
01403|     }
01404| 
01405|     public void AddJitter(Point original, Point jitter)
01406|     {
01407|         var tempVector = (jitter.ToVector3() + original.ToVector3()) / 2.0f;
01408|         tempVector = tempVector.Normalized();
01409|         original.Position = tempVector;
01410|     }
01411| 
01412|     public Point GetMiddle(Point v1, Point v2)
01413|     {
01414|         //var tempVector = (v1 + v2) / 2.0f;
01415|         var tempVector = (v2.ToVector3() - v1.ToVector3()) * 0.5f + v1.ToVector3();
01416|         //tempVector.Normalized();
01417| 
01418|         if (VertexPoints.Any(p => p.ToVector3() == tempVector))
01419|         {
01420|             var existingPoint = VertexPoints.Where(a => a.ToVector3() == tempVector);
01421|             return existingPoint.ElementAt(0);
01422|         }
01423|         Point middlePoint = new Point(tempVector, VertexPoints.Count);
01424| 
01425|         VertexPoints.Add(middlePoint);
01426|         return middlePoint;
01427|     }
01428| 
01429|     public void PopulateArrays()
01430|     {
01431|         VertexPoints = new List<Point> {
01432|                         new Point(new Vector3(0, 1, TAU), 0),
01433|                         new Point( new Vector3(0, -1, TAU), 1),
01434|                         new Point( new Vector3(0, -1, -TAU), 2),
01435|                         new Point( new Vector3(0, 1, -TAU), 3),
01436|                         new Point(new Vector3(1, TAU, 0), 4),
01437|                         new Point( new Vector3(-1, TAU, 0), 5),
01438|                         new Point( new Vector3(-1, -TAU, 0), 6),
01439|                         new Point( new Vector3(1, -TAU, 0), 7),
01440|                         new Point(new Vector3(TAU, 0, 1), 8),
01441|                         new Point( new Vector3(TAU, 0, -1), 9),
01442|                         new Point( new Vector3(-TAU, 0, -1), 10),
01443|                         new Point( new Vector3(-TAU, 0, 1), 11)};
01444|         VertexIndex = 12;
01445|         // for(int i = 0; i < cartesionPoints.Count; i++) {
01446|         //   cartesionPoints[i] = cartesionPoints[i].Normalized();
01447|         // }
01448|         normals = new List<Vector3>();
01449|         uvs = new List<Vector2>();
01450|         foreach (Point point in VertexPoints)
01451|         {
01452|             point.Position = point.Position.Normalized();
01453|             normals.Add(point.ToVector3());
01454|         }
01455|         faces = new List<Face>();
01456|         indices = new List<int> {
01457|       0, 5, 4,
01458|       0, 11, 5,
01459|       0, 4, 8,
01460|       0, 8, 1,
01461|       0, 1, 11,
01462|       3, 4, 5,
01463|       3, 5, 10,
01464|       3, 9, 4,
01465|       3, 10, 2,
01466|       3, 2, 9,
01467|       10, 5, 11,
01468|       10, 11, 6,
01469|       8, 4, 9,
01470|       8, 9, 7,
01471|       1, 7, 6,
01472|       1, 6, 11,
01473|       1, 8, 7,
01474|       2, 10, 6,
01475|       2, 7, 9,
01476|       2, 6, 7,
01477|     };
01478|         for (int i = 0; i < indices.Count; i += 3)
01479|         {
01480|             faces.Add(new Face(VertexPoints[indices[i]], VertexPoints[indices[i + 1]], VertexPoints[indices[i + 2]]));
01481|         }
01482| 
01483|     }
01484| 
01485| }
01486| 
01487| 
</file>

---

**Assistant** (*2025-08-22 14:25:26*)

I'll create a comprehensive plan for implementing a flexible vertex generation system that allows defining the number of vertices per iteration instead of the current exponential growth.

[Tool: todowrite]

I'll create a comprehensive plan for implementing a flexible vertex generation system that allows defining the number of vertices per iteration instead of the current exponential growth.

## Task Summary
**Objective**: Replace the current exponential vertex growth system in @GenerateDocArrayMesh.cs with a configurable vertex generation system that allows specifying exact vertex counts per subdivision iteration.

**Current Issue**: The existing system doubles vertices each iteration (2^n growth), making it impossible to control mesh density precisely.

## Ordered Implementation Plan

### 1. **Analyze Current System** (High Priority)
- **Location**: `GenerateDocArrayMesh.cs:1389-1403` (Subdivide method)
- **Current Behavior**: Fixed 4-way subdivision creating exponential growth
- **Key Files**: `GenerateDocArrayMesh.cs`, `GenerateDodecahedron.cs`

### 2. **Design New Interface** (High Priority)
- **Create**: `IVertexGenerator` interface with methods:
  - `GenerateVertices(int count, Edge edge, float t)`
  - `GetVertexPositions(int subdivisionLevel)`
- **Parameters**: vertex count per edge, distribution pattern (linear/geometric/custom)

### 3. **Implement Core Algorithm** (High Priority)
- **New Class**: `ConfigurableSubdivider`
- **Methods**:
  - `SubdivideFace(Face face, int verticesPerEdge)`
  - `DistributeVertices(int count, Vector3 start, Vector3 end)`
- **Edge Cases**: Handle odd vertex counts, boundary conditions

### 4. **Vertex Distribution Strategies** (Medium Priority)
- **Linear**: Equal spacing along edges
- **Geometric**: Logarithmic spacing for smooth transitions
- **Custom**: User-defined interpolation functions

### 5. **Update Mesh Generation** (Medium Priority)
- **Modify**: `GenerateNonDeformedFaces()` to use new subdivider
- **Add**: Configuration export variables in Godot inspector
- **Maintain**: Backward compatibility with existing subdivide parameter

### 6. **Godot Integration** (Medium Priority)
- **Export Variables**:
  - `vertices_per_edge: int[]` (array for each subdivision level)
  - `distribution_mode: enum` (linear, geometric, custom)
  - `custom_curve: Curve` (for custom distributions)

### 7. **Testing Framework** (Low Priority)
- **Unit Tests**: Validate vertex counts match specifications
- **Integration Tests**: Verify mesh topology integrity
- **Performance Tests**: Benchmark against old system

### 8. **Documentation** (Low Priority)
- **Usage Guide**: How to configure vertex counts
- **API Reference**: New classes and methods
- **Migration Guide**: Converting from old system

## Technical Implementation Details

### Core Algorithm Structure
```csharp
public interface IVertexGenerator {
    Point[] GenerateVertices(int count, Edge edge);
    float GetParameterization(int index, int total);
}

public class LinearVertexGenerator : IVertexGenerator {
    public Point[] GenerateVertices(int count, Edge edge) {
        var step = 1.0f / (count + 1);
        return Enumerable.Range(1, count)
            .Select(i => Point.Lerp(edge.P, edge.Q, i * step))
            .ToArray();
    }
}
```

### Configuration Example
```csharp
[Export]
public int[] VerticesPerEdge = {2, 4, 8}; // 2 vertices after 1st subdivide, 4 after 2nd, etc.

[Export]
public VertexDistribution Distribution = VertexDistribution.Linear;
```

This plan provides a complete roadmap for transitioning from exponential to configurable vertex generation while maintaining compatibility with existing code.


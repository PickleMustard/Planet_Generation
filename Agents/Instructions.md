# Refactor Agents Instructions

Purpose: Refactor mesh topology storage to a canonical, half-edge–backed model while preserving current behavior. Centralize point/edge/triangle interactions behind `StructureDatabase` facades, deduplicate points, unify edge identity via `EdgeKey`, and insert validation gates.

Global Requirements:
- Keep current functionality and public behavior intact.
- Use canonical registries as source of truth; mirror legacy maps until all callers migrate.
- Enforce invariants: unique points by index, twin half-edges exist and are symmetric, ≤2 triangles per undirected edge, adjacency symmetry.
- Thread-safety: wrap registry mutations with the existing `lockObject`.
- Deliverable cadence: first respond “READY FOR REVIEW” with a summary; after approval, implement and test locally.

Validation:
- Provide `StructureDatabase.Validate(string stage)`; non-mutating; logs issues.
- Call it at end-of-base-pass, pre-deform, and post-voronoi.

Backward Compatibility:
- Introduce `Base.*` and `Dual.*` internal containers; expose obsolete read-only views for old fields until migration completes.
- Preserve edge indices where the code depends on them (e.g., deformation).

---

## Agent: StructureDatabase.cs (Phases 0, 1, 3–4, 5–6)

Objectives:
- Phase 0: Add canonical registries and `Validate()`.
- Phase 1: Add facade APIs and shim legacy methods to use registries.
- Phase 3–4: Consolidate maps, add `Base.*`/`Dual.*`, and [Obsolete] read-only views for old fields.
- Phase 5–6: Add cleanup notes and ensure `Validate()` is callable.

File:
- `Scripts/MeshGeneration/StructureDatabase.cs`

Tasks:
- Registries: `PointsById`, `HalfEdgeById`, `UndirectedEdgeIndex`, `TrianglesById`, `TrianglesByEdgeKey`, `OutHalfEdgesByPoint`.
- Facades:
  - Points: `GetOrCreatePoint(Vector3)`, `GetOrCreatePoint(int, Vector3)`.
  - Edges: `GetOrCreateEdge(Point,Point)`, `GetOrCreateEdge(Point,Point,int)`, `TryGetEdge(Point,Point,out Edge)`, `Edge[] GetIncidentHalfEdges(Point)`.
  - Triangles: `Triangle AddTriangle(List<Point>)`, `IEnumerable<Triangle> GetIncidentTriangles(Point)`, `List<Triangle> GetTrianglesByEdgeIndex(int)`, `bool FlipEdge(Edge e, out Triangle t1, out Triangle t2)` (eligibility).
- Shims: Modify `AddPoint`, `AddEdge` (all overloads), `AddTriangle(Triangle)`, `GetEdgesFromPoint`, `UpdateWorldEdgeMap`, `RemoveEdge`, `UpdateEdge`, `UpdateTriangle` to call registries first, then mirror legacy maps.
- Consolidation: Add `Base.*`/`Dual.*` internal containers later; [Obsolete] read-only views for `VertexPoints`, `Edges`, `HalfEdgesFrom`, `EdgeTriangles`, `BaseTris`, `worldHalfEdgeMapFrom/To`, `circumcenters`.
- Validation gates: Hook `Validate()` judiciously (debug/flag).

Acceptance Criteria:
- No behavior changes to callers.
- New APIs compile; old maps remain usable and consistent.
- `Validate()` asserts invariants; no side effects.

---

## Agent: EdgeKey.cs (Phase 0)

Objective: Introduce direction-agnostic edge identity.

File:
- `Scripts/MeshGeneration/EdgeKey.cs`

Tasks:
- Namespace: `MeshGeneration`.
- `public readonly struct EdgeKey : IEquatable<EdgeKey>` with `int A,B` where `A < B`.
- Factories: `From(Structures.Point a, Structures.Point b)`, `From(int a, int b)`.
- Equality, `GetHashCode`, `ToString`, operators.
- XML docs summarizing canonical undirected keys.

Acceptance Criteria:
- Value semantics stable across sessions; used as dictionary keys.

---

## Agent: HalfEdge.cs (Phase 0)

Objective: Introduce directed half-edge representation.

File:
- `Scripts/MeshGeneration/HalfEdge.cs`

Tasks:
- Namespace: `MeshGeneration`.
- `public class HalfEdge` with properties:
  - `int Id` (internal set), `Point From`, `Point To`, `HalfEdge Twin`, `Triangle Left`, `EdgeKey Key`.
- Internal constructor assigns `Id`, `From`, `To`, `Key`.
- `ToString()` and XML docs.

Acceptance Criteria:
- No external dependencies beyond `Structures.Point/Triangle`.

---

## Agent: BaseMeshGeneration.cs (Phase 2)

Objective: Migrate to StructureDatabase facades; preserve behavior.

File:
- `Scripts/MeshGeneration/BaseMeshGeneration.cs`

Tasks:
- `PopulateArrays()`: Build base 12 points via `db.GetOrCreatePoint(index, pos)` and edges via `db.GetOrCreateEdge(p,q)`.
- `GenerateNonDeformedFaces()`: Remove direct `StrDb.Edges.Clear()`; add TODO for DB phase reset.
- `GenerateTriangleList()`: Replace manual edge/triangle wiring with `db.AddTriangle(points)`.
- `DeformMesh()`: Use `db.GetIncidentHalfEdges(p)`, `db.GetTrianglesByEdgeIndex(index)`, `db.RemoveEdge(index)`, and `db.GetOrCreateEdge(..., index)` for rewiring.

Acceptance Criteria:
- Output identical; indices preserved where required.

---

## Agent: CelestialBodyMesh.cs (Phase 2b and 5–6)

Objectives: Centralize edge access; add validation gates.

File:
- `Scripts/MeshGeneration/CelestialBodyMesh.cs`

Tasks:
- `RenderTriangleAndConnections()`: Replace direct map access with `db.GetIncidentHalfEdges(p)` (or `db.GetEdgesFromPoint(p)` as a temporary wrapper if needed).
- `GenerateFirstPass()`: Add `StrDb.Validate("end-of-base-pass")` near method end.
- `GenerateSecondPass()`: Add `StrDb.Validate("post-voronoi")` after Voronoi generation block.

Acceptance Criteria:
- Visual output unchanged; debug-only validation logs acceptable.

---

## Agent: VoronoiCellGeneration.cs (Phase 2b)

Objective: Use facades for incident traversal and circumcenters; canonicalize edge/cell mapping with `EdgeKey`.

File:
- `Scripts/MeshGeneration/VoronoiCellGeneration.cs`

Tasks:
- `GenerateVoronoiCells()`: Replace `StrDb.GetEdgesFromPoint(p)` with `db.GetIncidentHalfEdges(p)`; create circumcenters via `db.GetOrCreateCircumcenter(index, pos)`.
- `TriangulatePoints()`: Register cell membership via `db.AddCellForVertex(point, cell)` and `db.AddCellForEdge(EdgeKey, cell)` using direction-agnostic keys.

Acceptance Criteria:
- Same number of cells/vertices/edges; no duplicate edges across reversals.

---

## Deliverables and Review Flow

- Step 1: Implement each agent’s process, implement changes, run local validations, and report outcomes.
- Step 2: You review and merge as desired.

---

## Status Update (2025-09-28)

### Completed
- Phase 0: Canonical topology
  - `MeshGeneration.EdgeKey`: direction-agnostic, stable edge identity.
  - `MeshGeneration.HalfEdge`: directed edges with `Twin`, `Left`, `Key`.
  - `StructureDatabase`: canonical registries (`PointsById`, `HalfEdgeById`, `UndirectedEdgeIndex`, `TrianglesById`, `TrianglesByEdgeKey`, `OutHalfEdgesByPoint`), validation method `Validate(stage)`.
- Phase 1: Facades and shims
  - Points: `GetOrCreatePoint(Vector3)`, `GetOrCreatePoint(int, Vector3)`.
  - Edges: `TryGetEdge`, `GetOrCreateEdge(p,q)`, `GetOrCreateEdge(p,q,index)`.
  - Incident traversal: `GetIncidentHalfEdges(Point)` (returns legacy `Edge[]`).
  - Triangles: `AddTriangle(List<Point>)`, `GetIncidentTriangles(Point)`, `GetTrianglesByEdgeIndex(int)`, `FlipEdge(eligibility only)`.
  - Voronoi helpers: `GetOrCreateCircumcenter`, `AddCellForVertex`, `AddCellForEdge(EdgeKey)`, internal `EdgeKeyCellMap`.
  - Legacy shims call registries first and mirror legacy maps.
- Phase 2 / 2b: Callers updated + validation gates
  - `BaseMeshGeneration`: uses `db.GetOrCreatePoint`, `db.AddTriangle`, incident APIs for deform; removed direct DB clears (left TODO reset hook).
  - `CelestialBodyMesh`: rendering uses `db.GetIncidentHalfEdges(p)`; validation gates present at `pre-deform` and `end-of-base-pass`.
  - `VoronoiCellGeneration`: uses incident traversal via `db.GetIncidentHalfEdges(p)`, triangle lookup via `GetTrianglesByEdgeIndex`, circumcenters via `GetOrCreateCircumcenter`, and cell registration via `AddCellForVertex`/`AddCellForEdge(EdgeKey)`.
- Phase 3–4 groundwork (new this session)
  - Added internal containers: `Base` and `Dual` on `StructureDatabase` exposing read-only views over current legacy maps.
  - Added `[Obsolete]` read-only views for legacy fields: `VertexPoints`, `Edges`, `EdgeTriangles`, `HalfEdgesFrom`, `BaseTris`, `worldHalfEdgeMapFrom/To`, `circumcenters`.
  - Added `EnableValidation` flag to toggle `Validate(stage)`.
  - Added `ResetPhase(MeshState)` hook to clear dual-phase structures when entering base phase.
- Phase 3–4 migrations (new):
  - `TectonicGeneration`: replaced `GetEdgesFromPoint` with `GetIncidentHalfEdges`; reads edge-to-cells via `Dual.EdgeCells` (read-only) instead of `EdgeMap`; behavior unchanged.
  - `CelestialBodyMesh`: de-duplicated consecutive `StrDb.Validate("post-voronoi")` calls in `GenerateSecondPass()`.
- Build: `dotnet build` succeeds (only pre-existing warnings).
- Behavior: Preserved; indices and outputs remain stable.

### In Progress
- Consolidating around `Base.*` / `Dual.*` containers as the eventual source of truth (currently they provide read-only views).
- Tightening/placing validation gates; `post-voronoi` validation now wired in `CelestialBodyMesh.GenerateSecondPass()` and `VoronoiCellGeneration.GenerateVoronoiCells()`.

### Remaining (Phases 3–6)
- Phase 3–4
  - Route more callers to canonical facades and to `Base.*`/`Dual.*` containers.
  - Replace remaining direct legacy writes with updates via facades; leave legacy reads as `[Obsolete]` views until fully migrated.
  - Use `ResetPhase(MeshState.BaseMesh)` instead of ad hoc clears between phases where appropriate.
  - Add a debug/flag control path around validation gates using `EnableValidation`.
- Phase 5–6
  - Final cleanup: remove direct legacy writes; keep only read-only views.
  - Optional: implement full edge-flip mutation (beyond eligibility) if needed.
  - Ensure all validation gates are well-placed and configurable.
  - Migrate `TectonicGeneration` and other consumers to canonical facades.

### Code Touchpoints
- Added: `Scripts/MeshGeneration/EdgeKey.cs`, `Scripts/MeshGeneration/HalfEdge.cs`.
- Updated: `Scripts/MeshGeneration/StructureDatabase.cs`, `Scripts/MeshGeneration/BaseMeshGeneration.cs`, `Scripts/MeshGeneration/CelestialBodyMesh.cs`, `Scripts/MeshGeneration/VoronoiCellGeneration.cs`,
  `Scripts/MeshGeneration/ConfigurableSubdivider.cs`, `Scripts/MeshGeneration/LinearVertexGenerator.cs`, `Scripts/MeshGeneration/ConstrainedDelauneyTriangulation.cs`, `Scripts/MeshGeneration/SphericalDelaunayTriangulation.cs`.
- New this session (StructureDatabase): `Base`/`Dual` containers, `[Obsolete]` legacy views, `EnableValidation`, `ResetPhase`.

### Acceptance Criteria Tracking
- No behavior changes to callers: Met.
- New APIs compile; old maps remain usable and consistent: Met.
- `Validate(stage)` asserts invariants and is non-mutating: Met; toggleable via `EnableValidation`.
- Post-Voronoi validation gate: Met; placed after Voronoi generation in `CelestialBodyMesh.GenerateSecondPass()` and at the end of `VoronoiCellGeneration.GenerateVoronoiCells()`.

### Next Actions
- Consolidate direct reads/writes:
  - Prefer `StrDb.Legacy*` read-only views for any remaining legacy reads while migrating.
  - Replace direct writes to legacy maps with facades (`GetOrCreatePoint`, `GetOrCreateEdge`, `AddTriangle`, `GetIncidentHalfEdges`, `GetTrianglesByEdgeIndex`, `AddCellForVertex`, `AddCellForEdge`).
- TectonicGeneration migration (Phase 3–4 target):
  - Replace `StrDb.GetEdgesFromPoint` calls with `StrDb.GetIncidentHalfEdges` (facade alias retained for now, both are equivalent).
  - Encapsulate EdgeMap access behind a small helper or use `AddCellForEdge(EdgeKey)` pathway in producers; consumers should treat EdgeMap as read-only via `Dual.EdgeCells`.
  - Keep behavior identical; no change in stress calculations.
- Validation gates:
  - Keep `EnableValidation` toggled; gates currently at `pre-deform`, `end-of-base-pass`, and `post-voronoi`.
  - Optional: de-duplicate multiple `post-voronoi` calls in `CelestialBodyMesh` to a single call at method end.
- Phase Reset:
  - Continue using `StrDb.ResetPhase(StructureDatabase.MeshState.BaseMesh)` when entering base phase instead of ad-hoc clears.
- Plan incremental commits to minimize risk during consolidation.


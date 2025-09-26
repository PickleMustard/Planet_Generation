extends MeshInstance3D

const TAU = (1.0 + sqrt(5.0)) / 2.0  # Golden ratio (~1.618)

func _ready():
	var mesh = ArrayMesh.new()
	var arrays = []
	arrays.resize(Mesh.ARRAY_MAX)
	
	var vertices = generate_vertices()
	var faces = generate_faces()
	
	var mesh_vertices = PackedVector3Array()
	var mesh_uvs = PackedVector2Array()
	var mesh_indices = PackedInt32Array()
	
	for face in faces:
		var face_verts = []
		for idx in face: face_verts.append(vertices[idx])
		
		# Calculate face centroid
		var centroid = Vector3.ZERO
		for v in face_verts: centroid += v
		centroid /= face_verts.size()
		
		# Create local coordinate system
		var normal = (face_verts[1] - face_verts[0]).cross(
					face_verts[2] - face_verts[0]).normalized()
		var tangent = (face_verts[0] - centroid).normalized()
		var bitangent = normal.cross(tangent).normalized()
		
		# Project vertices to UV space
		var uvs = []
		var min_u = INF; var max_u = -INF
		var min_v = INF; var max_v = -INF
		
		for v in face_verts:
			var rel_pos = v - centroid
			var u = rel_pos.dot(tangent)
			var v_proj = rel_pos.dot(bitangent)
			uvs.append(Vector2(u, v_proj))
			min_u = min(min_u, u); max_u = max(max_u, u)
			min_v = min(min_v, v_proj); max_v = max(max_v, v_proj)
		
		# Normalize UVs
		var u_range = max_u - min_u
		var v_range = max_v - min_v
		var normalized_uvs = uvs.map(func(uv):
			return Vector2(
				(uv.x - min_u) / u_range if u_range != 0 else 0.0,
				(uv.y - min_v) / v_range if v_range != 0 else 0.0
			)
		)
		
		# Add vertices and UVs
		var base_idx = mesh_vertices.size()
		mesh_vertices.append_array(face_verts)
		mesh_uvs.append_array(normalized_uvs)
		
		# Triangulate pentagon (3 triangles)
		mesh_indices.append_array([
			base_idx, base_idx+1, base_idx+2,
			#base_idx, base_idx+2, base_idx+3,
			#base_idx, base_idx+3, base_idx+4
		])
	
	# Configure mesh arrays
	arrays[Mesh.ARRAY_VERTEX] = mesh_vertices
	arrays[Mesh.ARRAY_TEX_UV] = mesh_uvs
	arrays[Mesh.ARRAY_INDEX] = mesh_indices
	
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	self.mesh = mesh

func generate_vertices():
	var verts = []
	
	# Group 1: (0, ±1, ±TAU)
	for y in [-1, 1]:
		for z in [-TAU, TAU]:
			verts.append(Vector3(0, y, z))
	
	# Group 2: (±1, ±TAU, 0)
	for x in [-1, 1]:
		for y in [-TAU, TAU]:
			verts.append(Vector3(x, y, 0))
	
	# Group 3: (±TAU, 0, ±1)
	for x in [-TAU, TAU]:
		for z in [-1, 1]:
			verts.append(Vector3(x, 0, z))
	
	return verts

func generate_faces():
	# Precomputed face indices for standard dodecahedron
	# Each row represents a pentagonal face with 5 vertex indices
	#return [
	#	[0, 1, 5, 9, 4],    [0, 4, 8, 11, 3],   [0, 3, 7, 10, 1],
	#	[1, 10, 6, 5, 0],   [5, 6, 15, 9, 0],   [9, 15, 14, 8, 4],
	#	[8, 14, 18, 11, 3], [11, 18, 19, 7, 2], [7, 19, 17, 10, 1],
	#	[10, 17, 16, 6, 5], [6, 16, 13, 15, 9], [15, 13, 12, 14, 8],
	#	[14, 12, 2, 18, 11],[18, 2, 19, 7, 11], [19, 2, 12, 13, 16],
	#	[16, 17, 10, 6, 13],[12, 13, 16, 17, 19],[2, 12, 19, 18, 14],
	#	[3, 7, 2, 12, 8],   [4, 9, 15, 14, 8],  [5, 6, 15, 9, 0]
	#]
	
	return [
		[0, 5, 4], 
		[0, 11, 5],
	  [0, 4, 8],
	  [0, 8, 1],
	  [0, 1, 11],
	  [3, 4, 5],
	  [3, 5, 10],
	  [3, 9, 4],
	  [3, 10, 2],
	  [3, 2, 9],
	  [10, 5, 11],
	  [10, 11, 6],
	  [8, 4, 9],
	  [8, 9, 7],
	  [1, 7, 6],
	  [1, 6, 11],
	  [1, 8, 7],
	  [2, 10, 6],
	  [2, 7, 9],
	  [2, 6, 7],
	]

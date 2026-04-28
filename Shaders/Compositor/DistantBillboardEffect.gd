@tool
class_name DistantBillboardEffect
extends CompositorEffect

var rd: RenderingDevice
var shader: RID
var pipeline: RID
var position_buffer: RID  # SSBO holding distant object positions
var sampler: RID
var billboard_texture: RID

# Uniform sets rebuilt per frame
var uniform_set: RID

var positions: PackedVector4Array  # xyz = world pos, w = apparent size

func _init() -> void:
	effect_callback_type = EFFECT_CALLBACK_TYPE_POST_TRANSPARENT
	needs_motion_vectors = false
	needs_normal_roughness = false


func set_positions(new_positions: PackedVector4Array) -> void:
	positions = new_positions
	_update_position_buffer()

func _update_position_buffer() -> void:
	if rd == null:
		return
	if position_buffer.is_valid():
		rd.free_rid(position_buffer)

	var bytes := positions.to_byte_array()
	position_buffer = rd.storage_buffer_create(bytes.size(), bytes)

func _setup_shader() -> void:
	# Load the GLSL shader
	var shader_file := load("res://Shaders/billboard_computer.glsl")
	var shader_spirv :RDShaderSPIRV = shader_file.get_spirv()
	shader = rd.shader_create_from_spirv(shader_spirv)
	pipeline = rd.compute_pipeline_create(shader)

func _render_callback(
	p_effect_callback_type: int,
	p_render_data: RenderData
) -> void:
	if p_effect_callback_type != EFFECT_CALLBACK_TYPE_POST_TRANSPARENT:
		return
	if positions.size() == 0:
		return

	rd = RenderingServer.get_rendering_device()
	if shader == RID() or not shader.is_valid():
		_setup_shader()
		_update_position_buffer()

	var render_scene_buffers := p_render_data.get_render_scene_buffers()
	var render_scene_data := p_render_data.get_render_scene_data()

	if render_scene_buffers == null or render_scene_data == null:
		return

	var size :Vector2i = render_scene_buffers.get_internal_size()
	if size.x == 0 or size.y == 0:
		return

	# Get view matrices for projection
	var view_matrix :Transform3D = render_scene_data.get_view_transform(0)
	var proj_matrix := render_scene_data.get_view_projection(0)
	var cam_pos := view_matrix.origin

	# Pack camera data into a uniform buffer
	var cam_data := PackedFloat32Array()
	# View matrix (16 floats)
	for row in 4:
		for col in 4:
			# Flatten the basis + origin into a 4x4
			if col < 3 and row < 3:
				cam_data.append(view_matrix.basis[col][row])
			elif col == 3 and row < 3:
				cam_data.append(view_matrix.origin[row])
			elif row == 3 and col < 3:
				cam_data.append(0.0)
			else:
				cam_data.append(1.0)
	# Projection matrix (16 floats)
	for col in 4:
		for row in 4:
			cam_data.append(proj_matrix[col][row])
	# Screen size (2 floats) + count (1 float) + padding
	cam_data.append(float(size.x))
	cam_data.append(float(size.y))
	cam_data.append(float(positions.size()))
	cam_data.append(0.0)

	var cam_bytes := cam_data.to_byte_array()
	var cam_buffer := rd.uniform_buffer_create(cam_bytes.size(), cam_bytes)

	# Get the color framebuffer image to write into
	var color_image :RID = render_scene_buffers.get_color_layer(0)
	var depth_image :RID = render_scene_buffers.get_depth_layer(0)

	# Build uniform set:
	# binding 0 = camera uniform buffer
	# binding 1 = position SSBO
	# binding 2 = color image (write)
	# binding 3 = depth image (read/write)
	var uniforms: Array[RDUniform] = []

	var u_cam := RDUniform.new()
	u_cam.uniform_type = RenderingDevice.UNIFORM_TYPE_UNIFORM_BUFFER
	u_cam.binding = 0
	u_cam.add_id(cam_buffer)
	uniforms.append(u_cam)

	var u_pos := RDUniform.new()
	u_pos.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER
	u_pos.binding = 1
	u_pos.add_id(position_buffer)
	uniforms.append(u_pos)

	var u_color := RDUniform.new()
	u_color.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
	u_color.binding = 2
	u_color.add_id(color_image)
	uniforms.append(u_color)

	var u_depth := RDUniform.new()
	u_depth.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
	u_depth.binding = 3
	u_depth.add_id(depth_image)
	uniforms.append(u_depth)

	uniform_set = rd.uniform_set_create(uniforms, shader, 0)

	# Dispatch compute
	var compute_list := rd.compute_list_begin()
	rd.compute_list_bind_compute_pipeline(compute_list, pipeline)
	rd.compute_list_bind_uniform_set(compute_list, uniform_set, 0)
	# One workgroup per billboard, 64 threads per group to cover sprite pixels
	rd.compute_list_dispatch(
		compute_list,
		positions.size(),
		1,
		1
	)
	rd.compute_list_end()

	# Clean up per-frame resources
	rd.free_rid(cam_buffer)

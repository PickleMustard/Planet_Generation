shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D texture_albedo : source_color, filter_linear_mipmap_anisotropic;
uniform float brightness : hint_range(0.0, 2.0) = 1.0;
uniform float scale = 1.0;

void vertex() {
	// Direction and distance from camera to body
	vec3 to_target = NODE_POSITION_WORLD - CAMERA_POSITION_WORLD;
	float dist = length(to_target);
	vec3 dir = to_target / dist;

	// Derive far plane from the projection matrix
	float far_plane = PROJECTION_MATRIX[3][2] / PROJECTION_MATRIX[2][2];

	// Clamp to a proxy distance inside the frustum when beyond the far plane
	float proxy_dist = min(dist, far_plane * 0.9);
	float scale_ratio = proxy_dist / dist;

	// Billboard vertex in world space (MODEL_MATRIX carries billboard facing + node scale)
	vec3 vertex_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 offset = vertex_world - NODE_POSITION_WORLD;

	// Reposition at proxy distance, preserving apparent angular size
	vec3 proxy_center = CAMERA_POSITION_WORLD + dir * proxy_dist;
	vec3 world_pos = proxy_center + offset * scale_ratio;

	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world_pos, 1.0);
}

void fragment() {
	vec4 tex = texture(texture_albedo, UV);
	//ALBEDO = tex.rgb * COLOR.rgb * brightness;
	//ALPHA = tex.a * COLOR.a;
	//ALPHA_SCISSOR_THRESHOLD = 0.1;
}

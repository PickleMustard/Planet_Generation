shader_type spatial;

// texture for "top" side
uniform sampler2D top_texture;

// texture for sides/bottom
uniform sampler2D other_texture;

void vertex() {

	// convert the normal at this point into a VEC3 that can be read by
	// fragment() below, using COLOR to store it (although it's not a color)
	COLOR.rgb = NORMAL;

}

void fragment() {

	// find the relevant colour from both textures
	vec4 other_texture_col = texture( other_texture , UV.xy );

	vec4 top_texture_col = texture( top_texture , UV.xy );

	// "mix" (interpolate) the two texture colors, based on the normal
	// (COLOR.rgb.y, not really a color, but the normal assigned to
	// COLOR in vertex() above) at that point, which will be 1 for the "top"
	// side and hence show the top texture

	ALBEDO = mix( other_texture_col.rgb , top_texture_col.rgb , vec3(COLOR.rgb.y) );

}
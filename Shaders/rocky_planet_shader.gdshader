shader_type spatial;

render_mode unshaded, cull_back;

void fragment() {
    // Use the vertex color set by CelestialBodyMesh for biome colors
    ALBEDO = COLOR.rgb;
}
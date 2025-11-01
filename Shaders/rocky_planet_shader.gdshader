shader_type spatial;

render_mode unshaded, cull_back;

void fragment() {
    // Use the vertex color set by UnifiedCelestialMesh for biome colors
    ALBEDO = COLOR.rgb;
}
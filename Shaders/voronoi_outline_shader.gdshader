shader_type spatial;

render_mode unshaded;

uniform vec4 outline_color : source_color = vec4(0.8, 0.8, 0.8, 1.0); // Light gray
uniform vec4 base_color : source_color = vec4(1.0, 1.0, 1.0, 1.0); // Base color, can be overridden by vertex colors if needed

varying vec3 cell_normal;

void vertex() {
    // Assuming a custom vertex attribute "cell_normal" is set with the cell's average normal (e.g., normalized center)
    // For packing: use UV2 for x,y and COLOR.r for z, or set via SetCustom in C#
    cell_normal = vec3(UV2.x, UV2.y, COLOR.r); // Adjust based on your data packing
}

void fragment() {
    // Detect edges by checking derivatives of cell_normal
    vec3 dnormal_x = dFdx(cell_normal);
    vec3 dnormal_y = dFdy(cell_normal);
    float edge_strength = length(dnormal_x) + length(dnormal_y);

    // Smooth transition for outline
    float outline = smoothstep(0.0, 1.0, edge_strength);

    // Mix base color with outline color
    ALBEDO = mix(base_color.rgb, outline_color.rgb, outline);
}
shader_type spatial;

render_mode unshaded, blend_mix, depth_draw_opaque, cull_back;

// The index of the currently selected continent. -1.0 means no selection.
uniform float selected_continent_index = -1.0;

// Semi-transparent fill color for the continent interior.
uniform vec4 fill_color : source_color = vec4(0.2, 0.6, 1.0, 0.2);

// Darker border color drawn along the continent perimeter.
uniform vec4 border_color : source_color = vec4(0.08, 0.2, 0.5, 0.7);

// Controls how far inward the darkened border extends (0 = thin, 1 = wide).
uniform float border_width : hint_range(0.01, 1.0) = 0.4;

// Threshold for detecting the crisp continent edge via screen-space derivatives.
uniform float edge_threshold : hint_range(0.001, 1.0) = 0.02;

// The Voronoi cell index, flat-interpolated so derivatives detect cell boundaries.
varying flat float cell_id;

// The continent index for this fragment, passed from custom attribute.
varying flat float continent_id;

// Continent-border proximity: 1.0 at vertices on the continent boundary, 0.0 at
// interior vertices. Smooth interpolation across triangles creates a natural
// gradient from the perimeter inward.
varying float border_proximity;

void vertex() {
    cell_id = CUSTOM0.r;
    border_proximity = CUSTOM0.g;
    continent_id = CUSTOM0.b;
}

void fragment() {
    // Check if this fragment's continent matches the selected continent
    bool is_selected = abs(continent_id - selected_continent_index) < 0.5;
    
    if (!is_selected) {
        discard;
    }

    // --- Crisp continent-edge outline ---
    // Where border_proximity changes rapidly in screen space we are at the
    // transition between border and interior vertices, i.e. the continent edge.
    float dborder_dx = dFdx(border_proximity);
    float dborder_dy = dFdy(border_proximity);
    float border_edge_strength = abs(dborder_dx) + abs(dborder_dy);
    bool on_continent_edge = border_edge_strength > edge_threshold && border_proximity > 0.1;

    if (on_continent_edge) {
        ALBEDO = border_color.rgb;
        ALPHA = border_color.a;
    } else {
        // Smooth gradient: darken fragments closer to the continent perimeter.
        float border_factor = smoothstep(1.0 - border_width, 1.0, border_proximity);
        vec4 blended = mix(fill_color, border_color, border_factor);
        ALBEDO = blended.rgb;
        ALPHA = blended.a;
    }
}

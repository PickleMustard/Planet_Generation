shader_type spatial;

render_mode unshaded, blend_mix, depth_draw_opaque, cull_back;

// The index of the currently selected Voronoi cell. -1.0 means no selection.
uniform float selected_cell_id = -1.0;

// Pulse effect parameters
uniform bool pulse_enabled = true;
uniform float pulse_speed : hint_range(0.1, 10.0) = 2.0;
uniform float pulse_min_alpha : hint_range(0.0, 1.0) = 0.15;
uniform float pulse_max_alpha : hint_range(0.0, 1.0) = 0.4;

// Semi-transparent fill color for the selected cell interior.
uniform vec4 fill_color : source_color = vec4(1.0, 0.8, 0.0, 0.25);

// Solid outline color for the selected cell boundary.
uniform vec4 outline_color : source_color = vec4(1.0, 0.8, 0.0, 1.0);

// Controls the threshold for edge detection sensitivity.
uniform float outline_threshold : hint_range(0.001, 1.0) = 0.01;

// The Voronoi cell index for this fragment, passed from vertex stage.
// Using flat interpolation ensures no blending of cell IDs across triangle
// boundaries, which is critical for clean derivative-based edge detection.
varying flat float cell_id;

void vertex() {
    // UV2.x carries the Voronoi cell index set during mesh generation.
    cell_id = CUSTOM0.r;
}

void fragment() {
    float dcell_dx = dFdx(cell_id);
    float dcell_dy = dFdy(cell_id);
    float edge_strength = abs(dcell_dx) + abs(dcell_dy);
    bool is_boundary = edge_strength > outline_threshold;

    if (selected_cell_id < 0.0) {
        discard;
    }

    bool is_selected = abs(cell_id - selected_cell_id) < 0.5;

    // Calculate pulse alpha modulation
    float pulse_alpha = fill_color.a;
    if (pulse_enabled) {
        float pulse_factor = (sin(TIME * pulse_speed) + 1.0) * 0.5; // Map to 0-1 range
        pulse_alpha = mix(pulse_min_alpha, pulse_max_alpha, pulse_factor);
    }

    if (is_selected && is_boundary) {
        ALBEDO = outline_color.rgb;
        ALPHA = outline_color.a;
    } else if (is_selected) {
        ALBEDO = fill_color.rgb;
        ALPHA = pulse_alpha;
    } else if (is_boundary) {
        float neighbor_estimate_x = cell_id - dcell_dx;
        float neighbor_estimate_y = cell_id - dcell_dy;
        bool neighbor_is_selected = abs(neighbor_estimate_x - selected_cell_id) < 0.5
                                 || abs(neighbor_estimate_y - selected_cell_id) < 0.5;
        if (neighbor_is_selected) {
            ALBEDO = outline_color.rgb;
            ALPHA = outline_color.a;
        } else {
            discard;
        }
    } else {
        discard;
    }
}

shader_type spatial;

render_mode unshaded, blend_mix, depth_draw_opaque, cull_back;

// The index of the currently selected Voronoi cell. -1.0 means no selection.
uniform float selected_cell_id = -1.0;

// Semi-transparent fill color for the selected cell interior.
uniform vec4 fill_color : source_color = vec4(1.0, 0.8, 0.0, 0.25);

// Solid outline color for the selected cell boundary.
uniform vec4 outline_color : source_color = vec4(1.0, 0.8, 0.0, 1.0);

// Controls the threshold for edge detection sensitivity.
uniform float outline_threshold : hint_range(0.001, 1.0) = 0.01;

// Pulse effect parameters (toggled on when cell info window is open)
uniform bool pulse_enabled = false;
uniform float pulse_speed : hint_range(0.1, 10.0) = 2.0;
uniform float pulse_min_alpha : hint_range(0.0, 1.0) = 0.15;
uniform float pulse_max_alpha : hint_range(0.0, 1.0) = 0.4;

// Building placement highlighting.
// placement_cell_count > 0 activates placement mode (overrides normal selection).
uniform int placement_cell_count = 0;
uniform float placement_cell_ids[16];
uniform float placement_cell_valid[16]; // 1.0 = valid (green), 0.0 = invalid (red)
uniform vec4 valid_fill_color : source_color = vec4(0.0, 0.8, 0.0, 0.3);
uniform vec4 valid_outline_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);
uniform vec4 invalid_fill_color : source_color = vec4(0.8, 0.0, 0.0, 0.3);
uniform vec4 invalid_outline_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);

// The Voronoi cell index for this fragment, passed from vertex stage.
// Using flat interpolation ensures no blending of cell IDs across triangle
// boundaries, which is critical for clean derivative-based edge detection.
varying flat float cell_id;

void vertex() {
    // UV2.x carries the Voronoi cell index set during mesh generation.
    cell_id = CUSTOM0.r;
}

// Check if cell_id matches any placement cell. Returns index or -1.
int find_placement_cell(float frag_cell_id) {
    for (int i = 0; i < placement_cell_count; i++) {
        if (abs(frag_cell_id - placement_cell_ids[i]) < 0.5) {
            return i;
        }
    }
    return -1;
}

// Check if any neighbor estimate matches a placement cell.
int find_placement_neighbor(float neighbor_x, float neighbor_y) {
    for (int i = 0; i < placement_cell_count; i++) {
        if (abs(neighbor_x - placement_cell_ids[i]) < 0.5 ||
            abs(neighbor_y - placement_cell_ids[i]) < 0.5) {
            return i;
        }
    }
    return -1;
}

void fragment() {
    float dcell_dx = dFdx(cell_id);
    float dcell_dy = dFdy(cell_id);
    float edge_strength = abs(dcell_dx) + abs(dcell_dy);
    bool is_boundary = edge_strength > outline_threshold;

    // Placement mode: multi-cell red/green highlighting
    if (placement_cell_count > 0) {
        int match_idx = find_placement_cell(cell_id);

        if (match_idx >= 0) {
            bool valid = placement_cell_valid[match_idx] > 0.5;
            vec4 fc = valid ? valid_fill_color : invalid_fill_color;
            vec4 oc = valid ? valid_outline_color : invalid_outline_color;

            if (is_boundary) {
                ALBEDO = oc.rgb;
                ALPHA = oc.a;
            } else {
                ALBEDO = fc.rgb;
                ALPHA = fc.a;
            }
        } else if (is_boundary) {
            float neighbor_x = cell_id - dcell_dx;
            float neighbor_y = cell_id - dcell_dy;
            int neighbor_idx = find_placement_neighbor(neighbor_x, neighbor_y);
            if (neighbor_idx >= 0) {
                bool valid = placement_cell_valid[neighbor_idx] > 0.5;
                vec4 oc = valid ? valid_outline_color : invalid_outline_color;
                ALBEDO = oc.rgb;
                ALPHA = oc.a;
            } else {
                discard;
            }
        } else {
            discard;
        }
    } else {
    // Normal selection mode
    if (selected_cell_id < 0.0) {
        discard;
    }

    bool is_selected = abs(cell_id - selected_cell_id) < 0.5;

    // Calculate pulse alpha modulation
    float current_fill_alpha = fill_color.a;
    if (pulse_enabled) {
        float pulse_factor = (sin(TIME * pulse_speed) + 1.0) * 0.5;
        current_fill_alpha = mix(pulse_min_alpha, pulse_max_alpha, pulse_factor);
    }

    if (is_selected && is_boundary) {
        ALBEDO = outline_color.rgb;
        ALPHA = outline_color.a;
    } else if (is_selected) {
        ALBEDO = fill_color.rgb;
        ALPHA = current_fill_alpha;
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
}

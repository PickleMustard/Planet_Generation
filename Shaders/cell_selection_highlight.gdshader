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

// Placement preview. Per-cell highlight codes are uploaded as an R8 texture indexed
// by VoronoiCell.Index. Codes:
//   0 = none, 1 = footprint valid, 2 = footprint invalid,
//   3 = new coverage, 4 = dominant existing grid, 5 = absorbed existing grid.
uniform sampler2D placement_cell_data : filter_nearest, repeat_disable;
uniform ivec2 placement_data_size = ivec2(0, 0);
uniform bool placement_data_active = false;

// Footprint colors (codes 1 and 2).
uniform vec4 valid_fill_color : source_color = vec4(0.0, 0.8, 0.0, 0.3);
uniform vec4 valid_outline_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);
uniform vec4 invalid_fill_color : source_color = vec4(0.8, 0.0, 0.0, 0.3);
uniform vec4 invalid_outline_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);

// Coverage colors (codes 3, 4, 5).
uniform vec4 coverage_fill_color : source_color = vec4(1.0, 0.9, 0.0, 0.35);
uniform vec4 coverage_outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 hatch_color : source_color = vec4(0.0, 0.0, 0.0, 0.7);
uniform float hatch_spacing = 0.02;
uniform float hatch_width : hint_range(0.0, 1.0) = 0.35;

// The Voronoi cell index for this fragment, passed from vertex stage.
// Using flat interpolation ensures no blending of cell IDs across triangle
// boundaries, which is critical for clean derivative-based edge detection.
varying flat float cell_id;
varying vec3 world_position;

void vertex() {
    // CUSTOM0.r carries the Voronoi cell index set during mesh generation.
    cell_id = CUSTOM0.r;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

int sample_code(float cid) {
    if (placement_data_size.x <= 0) {
        return 0;
    }
    int id = int(cid + 0.5);
    if (id < 0) {
        return 0;
    }
    int x = id - (id / placement_data_size.x) * placement_data_size.x;
    int y = id / placement_data_size.x;
    if (y >= placement_data_size.y) {
        return 0;
    }
    return int(texelFetch(placement_cell_data, ivec2(x, y), 0).r * 255.0 + 0.5);
}

void fragment() {
    float dcell_dx = dFdx(cell_id);
    float dcell_dy = dFdy(cell_id);
    float edge_strength = abs(dcell_dx) + abs(dcell_dy);
    bool is_boundary = edge_strength > outline_threshold;

    if (placement_data_active) {
        int code = sample_code(cell_id);

        // Footprint cells: existing green/red semantics.
        if (code == 1 || code == 2) {
            bool valid = code == 1;
            vec4 fc = valid ? valid_fill_color : invalid_fill_color;
            vec4 oc = valid ? valid_outline_color : invalid_outline_color;
            if (is_boundary) {
                ALBEDO = oc.rgb;
                ALPHA = oc.a;
            } else {
                ALBEDO = fc.rgb;
                ALPHA = fc.a;
            }
        } else if (code == 3 || code == 4 || code == 5) {
            // Coverage cells.
            vec3 albedo = coverage_fill_color.rgb;
            float alpha = coverage_fill_color.a;

            // Cross-hatch overlay for absorbed grids (code 5).
            if (code == 5) {
                // Two perpendicular stripe directions in world space produce a cross-hatch.
                float h1 = abs(fract(dot(world_position, vec3(1.0, 0.7, 0.4)) / hatch_spacing) - 0.5);
                float h2 = abs(fract(dot(world_position, vec3(-0.4, 0.7, 1.0)) / hatch_spacing) - 0.5);
                float half_width = hatch_width * 0.5;
                if (h1 < half_width || h2 < half_width) {
                    albedo = mix(albedo, hatch_color.rgb, hatch_color.a);
                    alpha = max(alpha, hatch_color.a);
                }
            }

            // Outer perimeter for existing grids (codes 4 and 5): outline when at least
            // one neighbor is outside the merged cluster.
            if ((code == 4 || code == 5) && is_boundary) {
                int nx_code = sample_code(cell_id - dcell_dx);
                int ny_code = sample_code(cell_id - dcell_dy);
                bool nx_outside = nx_code != 4 && nx_code != 5;
                bool ny_outside = ny_code != 4 && ny_code != 5;
                if (nx_outside || ny_outside) {
                    albedo = coverage_outline_color.rgb;
                    alpha = coverage_outline_color.a;
                }
            }

            ALBEDO = albedo;
            ALPHA = alpha;
        } else {
            // code == 0: transparent, except bleed outline when adjacent to a footprint.
            if (is_boundary) {
                int nx_code = sample_code(cell_id - dcell_dx);
                int ny_code = sample_code(cell_id - dcell_dy);
                if (nx_code == 1 || ny_code == 1) {
                    ALBEDO = valid_outline_color.rgb;
                    ALPHA = valid_outline_color.a;
                } else if (nx_code == 2 || ny_code == 2) {
                    ALBEDO = invalid_outline_color.rgb;
                    ALPHA = invalid_outline_color.a;
                } else {
                    discard;
                }
            } else {
                discard;
            }
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

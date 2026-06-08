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
//   0  = none
//   1  = footprint valid
//   2  = footprint invalid
//   3  = building coverage range (yellow)
//   >=16 = existing grid cell, encoded as 16 + slot*3 + pattern where
//          slot (0..15)   selects the orange shade (distinguishes grids), and
//          pattern (0/1/2) selects the diagonal-line overlay:
//          0 = none, 1 = single diagonal (placement joins this grid),
//          2 = X (placement would merge this grid with another).
uniform sampler2D placement_cell_data : filter_nearest, repeat_disable;
uniform ivec2 placement_data_size = ivec2(0, 0);
uniform bool placement_data_active = false;

// Footprint colors (codes 1 and 2).
uniform vec4 valid_fill_color : source_color = vec4(0.0, 0.8, 0.0, 0.3);
uniform vec4 valid_outline_color : source_color = vec4(0.0, 1.0, 0.0, 1.0);
uniform vec4 invalid_fill_color : source_color = vec4(0.8, 0.0, 0.0, 0.3);
uniform vec4 invalid_outline_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);

// Building coverage color (code 3, yellow).
uniform vec4 coverage_fill_color : source_color = vec4(1.0, 0.9, 0.0, 0.35);

// Existing-grid colors (codes >= 16). grid_base_color is the brightest orange; each
// grid slot scales its brightness so neighbouring grids read as distinct shades.
uniform vec4 grid_base_color : source_color = vec4(1.0, 0.5, 0.0, 0.45);
uniform vec4 coverage_outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 hatch_color : source_color = vec4(0.0, 0.0, 0.0, 0.85);
uniform float hatch_spacing = 0.04;
uniform float hatch_width : hint_range(0.0, 1.0) = 0.18;

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

bool is_grid_code(int c) { return c >= 16; }
int grid_slot(int c) { return (c - 16) / 3; }
int grid_pattern(int c) { int g = c - 16; return g - (g / 3) * 3; }

// Distinct orange shade per grid slot: scale the base orange's brightness by a
// golden-ratio-spread factor so adjacent grids never share a shade.
vec3 grid_shade(int slot) {
    float h = fract(float(slot) * 0.61803399);
    float v = 0.55 + 0.45 * h;
    return grid_base_color.rgb * v;
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
        } else if (code == 3) {
            // Building coverage range: yellow fill.
            ALBEDO = coverage_fill_color.rgb;
            ALPHA = coverage_fill_color.a;
        } else if (is_grid_code(code)) {
            // Existing grid cell: orange shade per slot, optional diagonal line(s).
            int slot = grid_slot(code);
            int pattern = grid_pattern(code);
            vec3 albedo = grid_shade(slot);
            float alpha = grid_base_color.a;

            if (pattern != 0) {
                // First stripe direction always present; the perpendicular second
                // direction is added only for the X (pattern 2).
                float h1 = abs(fract(dot(world_position, vec3(1.0, 0.7, 0.4)) / hatch_spacing) - 0.5);
                float half_width = hatch_width * 0.5;
                bool on_line = h1 < half_width;
                if (pattern == 2) {
                    float h2 = abs(fract(dot(world_position, vec3(-0.4, 0.7, 1.0)) / hatch_spacing) - 0.5);
                    on_line = on_line || h2 < half_width;
                }
                if (on_line) {
                    albedo = mix(albedo, hatch_color.rgb, hatch_color.a);
                    alpha = max(alpha, hatch_color.a);
                }
            }

            // Perimeter outline where a neighbor belongs to a different grid (or none),
            // so adjacent grids of similar shade stay visually separated.
            if (is_boundary) {
                int nx_code = sample_code(cell_id - dcell_dx);
                int ny_code = sample_code(cell_id - dcell_dy);
                bool nx_diff = !is_grid_code(nx_code) || grid_slot(nx_code) != slot;
                bool ny_diff = !is_grid_code(ny_code) || grid_slot(ny_code) != slot;
                if (nx_diff || ny_diff) {
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

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

// The Voronoi cell index for this fragment, passed from vertex stage.
// Using flat interpolation ensures no blending of cell IDs across triangle
// boundaries, which is critical for clean derivative-based edge detection.
varying flat float cell_id;

void vertex() {
    // UV2.x carries the Voronoi cell index set during mesh generation.
    cell_id = UV2.x;
}

void fragment() {
    // Early-out if nothing is selected: discard fragment entirely.
    if (selected_cell_id < 0.0) {
        discard;
    }

    // Determine if this fragment belongs to the selected cell.
    // Cell IDs are integers cast to float, so a threshold of 0.5 handles
    // floating-point imprecision.
    bool is_selected = abs(cell_id - selected_cell_id) < 0.5;

    // Detect Voronoi cell boundaries using screen-space derivatives.
    // With flat interpolation, cell_id is constant within a triangle.
    // dFdx/dFdy compare values between adjacent fragments; at triangle
    // edges where the cell ID changes, these derivatives will be non-zero.
    float dcell_dx = dFdx(cell_id);
    float dcell_dy = dFdy(cell_id);
    float edge_strength = abs(dcell_dx) + abs(dcell_dy);
    bool is_boundary = edge_strength > outline_threshold;

    if (is_selected && is_boundary) {
        // Boundary of the selected cell: draw solid outline.
        ALBEDO = outline_color.rgb;
        ALPHA = outline_color.a;
    } else if (is_selected) {
        // Interior of the selected cell: draw semi-transparent fill.
        ALBEDO = fill_color.rgb;
        ALPHA = fill_color.a;
    } else if (is_boundary) {
        // Check if an adjacent fragment belongs to the selected cell.
        // If the boundary is between the selected cell and a neighbor,
        // the derivative jump will include the selected cell's ID.
        // We detect this by checking if the cell_id +/- derivative
        // lands near the selected_cell_id.
        float neighbor_estimate_x = cell_id - dcell_dx;
        float neighbor_estimate_y = cell_id - dcell_dy;
        bool neighbor_is_selected = abs(neighbor_estimate_x - selected_cell_id) < 0.5
                                 || abs(neighbor_estimate_y - selected_cell_id) < 0.5;
        if (neighbor_is_selected) {
            // This fragment is on the OTHER side of a selected cell boundary.
            ALBEDO = outline_color.rgb;
            ALPHA = outline_color.a;
        } else {
            discard;
        }
    } else {
        // Not selected, not a boundary: fully transparent.
        discard;
    }
}

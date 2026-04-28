#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

// Set 0, Binding 0: Camera data (std140, 160 bytes)
layout(set = 0, binding = 0, std140) uniform CameraData {
  mat4 view_proj_matrix; // 64 bytes
  mat4 clip_proj_matrix; // 64 bytes
  vec4 camera_position; // 16 bytes (xyz = pos, w = unused)
  vec2 screen_size; // 8 bytes
  float object_count; // 4 bytes
  float min_indicator_px; // 4 bytes
} camera;

// Set 0, Binding 1: Body data SSBO (std430)
// Each body = 2 vec4s (32 bytes):
//   data[i*2+0] = (world_pos.xyz, body_radius)
//   data[i*2+1] = (fallback_color.rgb, texture_layer_index)
//     texture_layer_index < 0 means no texture, use fallback color
layout(set = 0, binding = 1, std430) restrict readonly buffer BodyData {
  vec4 data[];
} bodies;

// Set 0, Binding 2: Color framebuffer (read-write for compositing)
layout(rgba16f, set = 0, binding = 2) uniform image2D color_image;

// Set 0, Binding 3: Depth framebuffer (read-only for occlusion test)
layout(r32f, set = 0, binding = 3) uniform restrict readonly image2D depth_image;

// Set 1, Binding 0: Billboard texture array (one 64x64 layer per body)
layout(set = 1, binding = 0) uniform sampler2DArray billboard_textures;

// Debug modes (controlled by min_indicator_px encoding):
// min_indicator_px >= 1000.0 => debug mode: paint red corner + green body markers
// min_indicator_px < 1000.0  => normal mode

void main() {
  ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
  int screen_w = int(camera.screen_size.x);
  int screen_h = int(camera.screen_size.y);

  float sceneDepth = imageLoad(depth_image, pixel).r;
  vec4 finalColor = imageLoad(color_image, pixel);

  // Bounds check - critical for out-of-range workgroups
  if (pixel.x >= screen_w || pixel.y >= screen_h)
    return;

  bool debug_mode = camera.min_indicator_px >= 1000.0;
  float actual_min_px = debug_mode ? (camera.min_indicator_px - 1000.0) : camera.min_indicator_px;
  // Ensure minimum is at least 4px in debug mode for visibility
  if (debug_mode) actual_min_px = max(actual_min_px, 8.0);

  // === DEBUG: Paint a red rectangle in the top-left corner ===
  // This verifies the shader is running and can write to the framebuffer
  if (debug_mode && pixel.x < 80 && pixel.y < 80) {
    // Checkerboard pattern to verify pixel addressing
    bool checker = ((pixel.x / 10) + (pixel.y / 10)) % 2 == 0;
    vec4 debug_color = checker ? vec4(1.0, 0.0, 0.0, 1.0) : vec4(0.0, 0.0, 1.0, 1.0);
    imageStore(color_image, pixel, debug_color);
    return;
  }

  // === DEBUG: Paint body count in top-right corner as colored bar ===
  if (debug_mode && pixel.y < 20 && pixel.x >= screen_w - 200) {
    int count = int(camera.object_count);
    int bar_width = count * 20; // 20px per body
    int bar_x = pixel.x - (screen_w - 200);
    if (bar_x < bar_width) {
      imageStore(color_image, pixel, vec4(0.0, 1.0, 1.0, 1.0)); // green bar
      return;
    }
  }

  // Sample depth with normalized UVs (not pixel coords!)
  vec2 depth_uv = (vec2(pixel) + 0.5) / camera.screen_size;
  float existing_depth = imageLoad(depth_image, ivec2(depth_uv)).r;

  // Godot Forward+ reversed-Z: near=1.0, far=0.0, sky clear=0.0
  // Skip pixels where real geometry exists (depth > 0)
  // In debug mode, skip the depth test to always draw indicators
  if (!debug_mode && existing_depth > 1e-7)
    return;

  int count = int(camera.object_count);
  /*float best_view_dist = 1e30;
                                      int best_body_idx = -1;
                                      vec2 best_offset = vec2(0.0);
                                      float best_pixel_radius = 0.0;

                                      for (int i = 0; i < count; i++) {
                                        vec4 pos_rad = bodies.data[i * 2];
                                        vec3 world_pos = pos_rad.xyz;
                                        float body_radius = pos_rad.w;

                                        // Distance from camera to body
                                        vec3 to_body = world_pos - camera.camera_position.xyz;
                                        float view_dist = length(to_body);
                                        //if (view_dist < 0.001) continue;

                                        // Project to view space via combined view-projection
                                        vec4 view_pos = camera.view_proj_matrix * vec4(world_pos, 1.0);

                                        // Project to clip space
                                        vec4 clip_pos = camera.clip_proj_matrix * view_pos;

                                        // Skip bodies behind camera (clip_pos.w <= 0 means behind near plane)
                                        if (clip_pos.w <= 0.0) continue;

                                        // NDC
                                        vec3 ndc = clip_pos.xyz / clip_pos.w;

                                        // Skip bodies far outside screen (with margin for large indicators)
                                        if (abs(ndc.x) > 1.2 || abs(ndc.y) > 1.2) continue;

                                        // Convert to screen coordinates
                                        // NDC y is up, image y is down -- flip
                                        vec2 screen_center;
                                        screen_center.x = (ndc.x * 0.5 + 0.5) * camera.screen_size.x;
                                        screen_center.y = (1.0 - (ndc.y * 0.5 + 0.5)) * camera.screen_size.y;

                                        // Calculate indicator pixel radius from angular size
                                        // In clip space, body_radius / clip_pos.w gives NDC-space radius
                                        // Multiply by half screen height to get pixel radius
                                        float pixel_radius = (body_radius / clip_pos.w) * camera.screen_size.y * 0.5;

                                        // Enforce minimum indicator size
                                        pixel_radius = max(pixel_radius, actual_min_px);

                                        // Distance from this pixel to the indicator center
                                        vec2 offset = vec2(pixel) - screen_center;
                                        float dist_to_center = length(offset);
                                        if (dist_to_center > pixel_radius) continue;

                                        // Closest body wins overlap
                                        if (view_dist < best_view_dist) {
                                          best_view_dist = view_dist;
                                          best_body_idx = i;
                                          best_offset = offset;
                                          best_pixel_radius = pixel_radius;
                                        }
                                      }

                                      if (best_body_idx < 0)
                                        return;

                                      // Compute UV within the indicator circle: map [-radius, +radius] to [0, 1]
                                      vec2 uv = best_offset / best_pixel_radius * 0.5 + 0.5;

                                      vec4 col_flags = bodies.data[best_body_idx * 2 + 1];
                                      float tex_layer = col_flags.w;

                                      vec4 indicator_color;
                                      if (tex_layer >= 0.0) {
                                        // Sample from billboard texture array
                                        indicator_color = texture(billboard_textures, vec3(depth_uv, tex_layer));
                                      } //else {
                                      //  // Fallback: classification-based color with radial falloff
                                      //  float normalized_dist = length(best_offset) / best_pixel_radius;
                                      //  float intensity = 1.0 - smoothstep(0.0, 1.0, normalized_dist);
                                      //  indicator_color = vec4(col_flags.rgb * intensity, intensity * 0.8);
                                      //}

                                      // Skip fully transparent pixels
                                      if (indicator_color.a < 0.01)
                                        return;

                                      // Alpha-blend over existing framebuffer color
                                      vec4 existing_color = imageLoad(color_image, pixel);
                                      vec4 blended = vec4(
                                          mix(existing_color.rgb, indicator_color.rgb, indicator_color.a),
                                          1.0
                                        );
                                      // FIX: Write blended result, not existing_color!
                                      imageStore(color_image, pixel, indicator_color);*/

  for (int i = 0; i < count; i++) {
    vec4 objData = bodies.data[i * 2];
    vec3 worldPos = objData.xyz;
    float radius = objData.w;

    // 1. Transform object to Clip Space
    vec4 clipPos = camera.view_proj_matrix * vec4(worldPos, 1.0);

    // 2. Frustum Culling (is the object center in front of the camera?)
    if (clipPos.w <= 0.0) continue;

    // 3. Perspective Divide to NDC
    vec3 ndcPos = clipPos.xyz / clipPos.w;

    // 4. Convert NDC to Screen Space
    vec2 screenPos = vec2(
        (ndcPos.x + 1.0) * 0.5 * camera.screen_size.x,
        (1.0 - ndcPos.y) * 0.5 * camera.screen_size.y
      );

    // 5. Calculate dynamic screen-space radius
    // The formula: (WorldRadius * ProjectionScale) / Distance
    // A simple approximation for "larger than proportion" and scaling:
    float dist = length(worldPos - camera.camera_position.xyz);
    float projectedRadius = (radius * 1000.0) / dist; // 1000.0 is a tuning constant

    // 6. Check if current pixel is inside the projected circle
    float distToCenter = distance(vec2(pixel), screenPos);
    if (distToCenter < projectedRadius) {

      // 7. Depth Test
      // clipPos.z / clipPos.w gives the depth in NDC [0, 1]
      if (ndcPos.z < sceneDepth) {
        // Calculate UVs for the texture (mapping the circle to [0, 1] square)
        vec2 uv = (vec2(pixel) - screenPos) / (2.0 * projectedRadius);
        uv = uv * 0.5 + 0.5; // Remap from [-0.5, 0.5] to [0, 1]

        // Only sample if UV is within the circle (circular crop)
        if (length(uv - 0.5) <= 0.5) {
          vec4 texColor = texture(billboard_textures, vec3(uv, i));

          // Basic Alpha Blending
          finalColor = mix(finalColor, texColor, texColor.a);
        }
      }
    }
  }

  imageStore(color_image, pixel, finalColor);
}

#[compute]
#version 450

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, std140) uniform CameraData {
  mat4 view_matrix;
  mat4 proj_matrix;
  vec2 screen_size;
  float object_count;
  float _pad;
} camera;

layout(set = 0, binding = 1, std430) restrict readonly buffer PositionBuffer {
  vec4 objects[];
} positions;

layout(set = 0, binding = 2, rgba16f) uniform restrict writeonly image2D color_image;

layout(set = 0, binding = 3, r32f) uniform restrict image2D depth_image;

void main() {
  uint object_idx = gl_WorkGroupID.x;
  if (object_idx >= uint(camera.object_count)) return;

  vec4 objec = positions.objects[object_idx];
  vec3 world_pos = obj.xyz;
  float sprite_radius_px = obj.w;

  vec4 view_pos = camera.view_matrix * vec4(world_pos, 1.0);
  vec4 clip_pos = camera.proj_matrix * view_pos;

  if (clip_pos.w <= 0.0) return;

  vec3 ndc = clip_pos.xyz / clip_pos.w;

  if (abs(ndc.x) > 1.2 || abs(ndc.y) > 1.2) return;

  vec2 screen_center = (ndc.xy * 0.5 + 0.5) * camera.screen_size;

  ivec2 local = ivec2(gl_LocalInvocationID.xy);
  ivec2 sprite_size = ivec2(int(sprite_radius_px * 2.0));

  ivec2 pixel_offset = local - ivec2(int(sprite_radius_px));
  ivec2 pixel_coord = ivec2(screen_center) + pixel_offset;

  if (pixel_coord.x < 0 || pixel_coord.x >= int(camera.screen_size.x)) return;
  if (pixel_coord.y < 0 || pixel_coord.y >= int(camera.screen_size.y)) return;
  float dist = length(vec2(pixel_offset));
  if (dist > sprite_radius_px) return;
  float billboard_depth = 0.001;
  float existing_depth = imageLoad(depth_image, pixel_coord).r;
  if (existing_depth > billboard_depth) return;

  float intensity = 1.0 - smoothstep(0.0, sprite_radius_px, dist);
  vec4 color = vec4(intensity, intensity * 0.9, intensity * 0.7, 1.0);
  imageStore(color_image, pixel_coord, color);
  imageStore(depth_image, pixel_coord, vec4(billboard_depth));
}

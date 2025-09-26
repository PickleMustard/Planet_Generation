shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque, depth_test_disabled;

// Star parameters
uniform float temperature : hint_range(3000.0, 40000.0, 100.0) = 5778.0; // Sun's temperature in Kelvin
uniform float emission_strength : hint_range(1.0, 50.0, 0.5) = 10.0;
uniform float corona_size : hint_range(0.0, 2.0, 0.1) = 0.3;
uniform float turbulence_strength : hint_range(0.0, 1.0, 0.01) = 0.15;
uniform float pulse_speed : hint_range(0.0, 5.0, 0.1) = 1.0;
uniform float fresnel_power : hint_range(0.5, 5.0, 0.1) = 2.0;

// Noise texture for surface detail
uniform sampler2D noise_texture : hint_default_white;
uniform float noise_scale : hint_range(0.1, 10.0, 0.1) = 2.0;

// Convert temperature to RGB using blackbody radiation approximation
vec3 temperature_to_rgb(float temp) {
    vec3 color;
    temp = clamp(temp, 1000.0, 40000.0) / 100.0;

    // Red
    if (temp <= 66.0) {
        color.r = 1.0;
    } else {
        float r = temp - 60.0;
        color.r = 329.698727446 * pow(r, -0.1332047592);
        color.r = clamp(color.r / 255.0, 0.0, 1.0);
    }

    // Green
    if (temp <= 66.0) {
        float g = temp;
        color.g = 99.4708025861 * log(g) - 161.1195681661;
        color.g = clamp(color.g / 255.0, 0.0, 1.0);
    } else {
        float g = temp - 60.0;
        color.g = 288.1221695283 * pow(g, -0.0755148492);
        color.g = clamp(color.g / 255.0, 0.0, 1.0);
    }

    // Blue
    if (temp >= 66.0) {
        color.b = 1.0;
    } else if (temp >= 19.0) {
        float b = temp - 10.0;
        color.b = 138.5177312231 * log(b) - 305.0447927307;
        color.b = clamp(color.b / 255.0, 0.0, 1.0);
    } else {
        color.b = 0.0;
    }

    return color;
}

// Simple 3D noise function
float noise3D(vec3 p) {
    return texture(noise_texture, p.xy * noise_scale + p.z).r;
}

void vertex() {
    // Add some subtle vertex displacement for a more dynamic look
    vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float noise = noise3D(world_pos * 0.5 + vec3(TIME * pulse_speed * 0.2));
    VERTEX += NORMAL * noise * turbulence_strength * 0.02;
}

void fragment() {
    // Get base color from temperature
    vec3 star_color = temperature_to_rgb(temperature);

    // Calculate view-dependent effects
    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - NODE_POSITION_WORLD);
    float fresnel = pow(1.0 - dot(NORMAL, view_dir), fresnel_power);

    // Sample noise for surface variations
    vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float noise1 = noise3D(world_pos * noise_scale + vec3(TIME * pulse_speed * 0.3));
    float noise2 = noise3D(world_pos * noise_scale * 2.0 - vec3(TIME * pulse_speed * 0.15));
    float combined_noise = mix(noise1, noise2, 0.5);

    // Create pulsating effect
    float pulse = sin(TIME * pulse_speed) * 0.1 + 0.9;

    // Combine surface details
    float surface_variation = mix(0.7, 1.0, combined_noise) * pulse;

    // Apply color with variations
    vec3 final_color = star_color * surface_variation;

    // Add corona/atmosphere glow at edges
    vec3 corona_color = star_color * 1.5;
    corona_color = mix(corona_color, vec3(1.0), 0.2); // Slightly whiten the corona
    final_color = mix(final_color, corona_color, fresnel * corona_size);

    // Add bright spots (simulating solar flares/prominences)
    float bright_spots = smoothstep(0.7, 0.9, combined_noise) * pulse;
    final_color += star_color * bright_spots * 0.5;

    // Set albedo and emission
    ALBEDO = final_color;
    EMISSION = final_color * emission_strength;

    // Ensure the star appears bright
    SPECULAR = 0.0;
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
}
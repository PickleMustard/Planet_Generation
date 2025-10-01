shader_type sky;
render_mode use_half_res_pass;

// Star field parameters
uniform float star_density : hint_range(1000.0, 50000.0, 1000.0) = 15000.0;
uniform float star_brightness : hint_range(0.1, 5.0, 0.1) = 1.0;
uniform float star_twinkle_speed : hint_range(0.0, 10.0, 0.5) = 2.0;

// Nebula parameters
uniform float nebula_density : hint_range(0.0, 1.0, 0.01) = 0.3;
uniform float nebula_brightness : hint_range(0.0, 2.0, 0.1) = 0.5;
uniform vec3 nebula_color1 : source_color = vec3(0.8, 0.3, 0.9);
uniform vec3 nebula_color2 : source_color = vec3(0.3, 0.5, 0.9);
uniform float nebula_scale : hint_range(0.1, 5.0, 0.1) = 1.0;

// Supernova parameters
uniform int supernova_count : hint_range(0, 10, 1) = 2;
uniform float supernova_brightness : hint_range(1.0, 20.0, 1.0) = 8.0;
uniform float supernova_expansion_speed : hint_range(0.1, 5.0, 0.1) = 1.0;

// Neutron star parameters
uniform int neutron_star_count : hint_range(0, 5, 1) = 1;
uniform float neutron_star_brightness : hint_range(5.0, 50.0, 1.0) = 15.0;
uniform float neutron_star_pulse_speed : hint_range(5.0, 50.0, 1.0) = 20.0;

// Noise textures for procedural generation
uniform sampler2D noise_texture : hint_default_white;
uniform sampler2D star_noise_texture : hint_default_white;

// Random number generator
float random(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

// 3D noise function
float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    
    vec2 uv = (i.xy + i.z * vec2(37.0, 17.0)) + f.xy;
    uv = uv / 256.0;
    
    float a = texture(noise_texture, uv).r;
    float b = texture(noise_texture, uv + vec2(1.0/256.0, 0.0)).r;
    float c = texture(noise_texture, uv + vec2(0.0, 1.0/256.0)).r;
    float d = texture(noise_texture, uv + vec2(1.0/256.0, 1.0/256.0)).r;
    
    float x1 = mix(a, b, f.x);
    float x2 = mix(c, d, f.x);
    return mix(x1, x2, f.y);
}

// Fractal noise for more detail
float fractal_noise(vec3 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    for (int i = 0; i < octaves; i++) {
        value += amplitude * noise3D(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    
    return value;
}

// Generate star color based on temperature
vec3 star_temperature_color(float temp) {
    temp = clamp(temp, 0.0, 1.0);
    
    if (temp < 0.2) {
        // Cool stars - red
        return mix(vec3(0.5, 0.1, 0.0), vec3(1.0, 0.3, 0.1), temp * 5.0);
    } else if (temp < 0.4) {
        // Orange stars
        return mix(vec3(1.0, 0.3, 0.1), vec3(1.0, 0.8, 0.3), (temp - 0.2) * 5.0);
    } else if (temp < 0.6) {
        // Yellow stars (like our Sun)
        return mix(vec3(1.0, 0.8, 0.3), vec3(1.0, 1.0, 0.8), (temp - 0.4) * 5.0);
    } else if (temp < 0.8) {
        // White stars
        return mix(vec3(1.0, 1.0, 0.8), vec3(0.8, 0.9, 1.0), (temp - 0.6) * 5.0);
    } else {
        // Hot blue stars
        return mix(vec3(0.8, 0.9, 1.0), vec3(0.4, 0.6, 1.0), (temp - 0.8) * 5.0);
    }
}

// Generate stars
vec3 generate_stars(vec3 direction, float time) {
    vec3 star_color = vec3(0.0);
    
    // Use multiple layers of noise for star distribution
    float star_seed = fractal_noise(direction * 10.0 + time * 0.01, 3);
    
    if (star_seed > 1.0 - star_density / 50000.0) {
        // Star exists at this position
        float brightness = random(direction.xy) * 0.8 + 0.2;
        float temperature = random(direction.yz);
        vec3 color = star_temperature_color(temperature);
        
        // Add twinkling effect
        float twinkle = sin(time * star_twinkle_speed + random(direction.xz) * 6.28) * 0.3 + 0.7;
        
        star_color = color * brightness * twinkle * star_brightness;
    }
    
    return star_color;
}

// Generate nebulae
vec3 generate_nebula(vec3 direction, float time) {
    if (nebula_density <= 0.0) return vec3(0.0);
    
    float nebula_noise = fractal_noise(direction * nebula_scale + time * 0.05, 4);
    nebula_noise = smoothstep(0.3, 0.7, nebula_noise);
    
    // Create nebula clouds
    float cloud1 = fractal_noise(direction * nebula_scale * 2.0 + time * 0.02, 3);
    float cloud2 = fractal_noise(direction * nebula_scale * 3.0 - time * 0.03, 3);
    float cloud_mix = mix(cloud1, cloud2, 0.5);
    
    vec3 nebula_color = mix(nebula_color1, nebula_color2, cloud_mix);
    
    return nebula_color * nebula_noise * nebula_density * nebula_brightness;
}

// Generate supernovae
vec3 generate_supernovae(vec3 direction, float time) {
    vec3 supernova_color = vec3(0.0);
    
    for (int i = 0; i < 10; i++) {
        if (i >= supernova_count) break;
        
        // Create pseudo-random positions for supernovae
        float seed = float(i) * 123.456;
        vec3 supernova_pos = normalize(vec3(
            sin(seed) * 0.7 + cos(seed * 2.1) * 0.3,
            cos(seed * 1.3) * 0.7 + sin(seed * 3.7) * 0.3,
            sin(seed * 2.7) * 0.7 + cos(seed * 0.9) * 0.3
        ));
        
        float distance = length(direction - supernova_pos);
        float radius = 0.1 + sin(time * supernova_expansion_speed + seed) * 0.05;
        
        if (distance < radius) {
            float intensity = 1.0 - (distance / radius);
            intensity = pow(intensity, 2.0);
            
            // Supernova color - bright blue-white core with orange edges
            vec3 core_color = vec3(0.8, 0.9, 1.0);
            vec3 edge_color = vec3(1.0, 0.6, 0.2);
            vec3 color = mix(core_color, edge_color, distance / radius);
            
            supernova_color += color * intensity * supernova_brightness;
        }
    }
    
    return supernova_color;
}

// Generate neutron stars
vec3 generate_neutron_stars(vec3 direction, float time) {
    vec3 neutron_color = vec3(0.0);
    
    for (int i = 0; i < 5; i++) {
        if (i >= neutron_star_count) break;
        
        // Create pseudo-random positions for neutron stars
        float seed = float(i) * 234.567 + 100.0;
        vec3 neutron_pos = normalize(vec3(
            cos(seed) * 0.8,
            sin(seed * 1.7) * 0.8,
            cos(seed * 2.3) * 0.8
        ));
        
        float distance = length(direction - neutron_pos);
        
        if (distance < 0.02) {
            // Neutron stars are very small but extremely bright
            float intensity = 1.0 - (distance / 0.02);
            intensity = pow(intensity, 8.0);
            
            // Pulsing effect
            float pulse = sin(time * neutron_star_pulse_speed + seed * 10.0) * 0.5 + 0.5;
            
            // Neutron star color - bluish white
            vec3 color = vec3(0.7, 0.8, 1.0);
            
            neutron_color += color * intensity * pulse * neutron_star_brightness;
            
            // Add small accretion disk effect
            if (distance < 0.01) {
                float disk_intensity = (1.0 - distance / 0.01) * 0.3;
                neutron_color += vec3(1.0, 0.7, 0.3) * disk_intensity;
            }
        }
    }
    
    return neutron_color;
}

void sky() {
    vec3 direction = normalize(EYEDIR);
    float time = TIME;
    
    // Start with black space
    vec3 color = vec3(0.0);
    
    // Add celestial objects
    color += generate_stars(direction, time);
    color += generate_nebula(direction, time);
    color += generate_supernovae(direction, time);
    color += generate_neutron_stars(direction, time);
    
    // Add subtle vignette effect for depth
    float vignette = 1.0 - length(direction) * 0.1;
    color *= vignette;
    
    COLOR = color;
    ALPHA = 1.0;
}
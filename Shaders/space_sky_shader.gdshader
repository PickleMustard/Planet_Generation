shader_type sky;
render_mode use_half_res_pass;

uniform float rand_seed;
// Star field parameters
group_uniforms stars;
uniform vec3 star_base_color: source_color = vec3(0.8, 1.0, 0.3);
uniform float star_hue_offset: hint_range(0., 1.) = 0.6;
uniform float star_intensity: hint_range(0., 0.2) = 0.08;
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

float hash11( uint n ) 
{
    // integer hash copied from Hugo Elias
	n = (n << 13U) ^ n;
    n = n * (n * n * 15731U + 789221U) + 1376312589U;
    return float( n & uint(0x7fffffffU))/float(0x7fffffff);
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

// Generate stars - revised for even spacing and distinct points
vec3 generate_stars(vec3 direction, float time) {
    vec3 star_color = vec3(0.0);
    int num_stars = int(star_density / 100.0);  // Adjust based on density for performance

    for (int i = 0; i < 1000; i++) {  // Limit iterations for performance
        if (i >= num_stars) break;

        // Generate evenly distributed positions using a simple pseudo-random but structured method
        float phi = acos(1.0 - 2.0 * float(i) / float(num_stars));  // Golden angle distribution
        float theta = 3.14159 * (1.0 + sqrt(5.0)) * float(i);  // Fibonacci spiral for even spacing

        vec3 star_pos = normalize(vec3(
            cos(theta) * sin(phi),
            sin(theta) * sin(phi),
            cos(phi)
        ));

        // Calculate angular distance between view direction and star position
        float dot_product = dot(direction, star_pos);
        float angular_distance = acos(clamp(dot_product, -1.0, 1.0));

        // Define star size (in radians) - smaller for more point-like appearance
        float star_radius = 0.005;  // Adjust for star size; smaller = more pinpoint

        if (angular_distance < star_radius) {
            // Star is "hit" - calculate intensity based on distance
            float intensity = 1.0 - (angular_distance / star_radius);
            intensity = pow(intensity, 3.0);  // Sharper falloff for point-like stars

            // Generate star properties
            float brightness = random(star_pos.xy) * 0.8 + 0.2;
            float temperature = random(star_pos.yz);
            vec3 color = star_temperature_color(temperature);

            // Add twinkling effect
            float twinkle = sin(time * star_twinkle_speed + random(star_pos.xz) * 6.28) * 0.3 + 0.7;

            // Combine and add to color
            star_color += color * brightness * intensity * twinkle * star_brightness;
        }
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
vec3 generate_supernovae(vec3 direction, float seed) {
    vec3 supernova_color = vec3(0.0);

        // Create pseudo-random positions for supernovae
        vec3 supernova_pos = normalize(vec3(
            sin(seed) * 0.7 + cos(seed * 2.1) * 0.3,
            cos(seed * 1.3) * 0.7 + sin(seed * 3.7) * 0.3,
            sin(seed * 2.7) * 0.7 + cos(seed * 0.9) * 0.3
        ));

        float distance = length(direction - supernova_pos);
        float radius = 0.1 + sin(supernova_expansion_speed + seed) * 0.05;

        if (distance < radius) {
            float intensity = 1.0 - (distance / radius);
            intensity = pow(intensity, 2.0);

            // Supernova color - bright blue-white core with orange edges
            vec3 core_color = vec3(0.8, 0.9, 1.0);
            vec3 edge_color = vec3(1.0, 0.6, 0.2);
            vec3 color = mix(core_color, edge_color, distance / radius);

            supernova_color += color * intensity * supernova_brightness;
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

vec3 hue(vec3 input, float offset, int range_index) {
	// RGB to HSV
	vec4 k = vec4(0., -1./3., 2./3., -1.);
	vec4 p = mix(vec4(input.bg, k.wz), vec4(input.gb, k.xy), step(input.b, input.g));
	vec4 q = mix(vec4(p.xyw, input.r), vec4(input.r, p.yzx), step(p.x, input.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.e-10;
	vec3 hsv = vec3(abs(q.z + (q.w - q.y) / (6. * d + e)), d / (q.x + e), q.x);

	offset = (range_index == 0) ? offset / 360. : offset;
	float hue = hsv.x + offset;
	if (hue < 0.) {
		hsv.x = hue + 1.;
	} else if (hue > 1.) {
		hsv.x = hue - 1.;
	} else {
		hsv.x = hue;
	}

	// HSV to RGB
	vec4 k2 = vec4(1., 2./3., 1./3., 3.);
	vec3 p2 = abs(fract(hsv.xxx + k2.xyz) * 6. - k2.www);
	vec3 rgb = hsv.z * mix(k2.xxx, clamp(p2 - k2.xxx, 0., 1.), hsv.y);
	return rgb;
}

vec3 hash(vec3 x) {
	x = vec3(dot(x, vec3(127.1,311.7, 74.7)),
			 dot(x, vec3(269.5,183.3,246.1)),
			 dot(x, vec3(113.5,271.9,124.6)));
	return fract(sin(x) * 43758.5453123);
}

vec2 voronoi(in vec3 x) {
	vec3 integer = floor(x);
	vec3 fractional = fract(x);
	
	float res = 100.;
	float id = 0.;
	
	for (float k = -1.; k <= 1.; k += 1.) {
		for (float j = -1.; j <= 1.; j += 1.) {
			for (float i = -1.; i <= 1.; i += 1.) {
				vec3 cell_offset = vec3(i, j, k);
				vec3 dist = vec3(cell_offset) - fractional + hash(integer + cell_offset);
				float sqrEuclDist = dot(dist, dist);
				if (sqrEuclDist < res) {
					res = sqrEuclDist;
					id = dot(integer + cell_offset, vec3(0., 57., 113.));
				}
		    }
		}
	}
	
	return vec2(sqrt(res), id);
}

void sky() {
	float rand_num = random(vec2(rand_seed));
	vec3 dir = normalize(EYEDIR);
	vec3 pos = EYEDIR * (60.);
	vec2 layer = voronoi(pos);
    float time = TIME;
	
	vec3 rand = hash(vec3(layer.y));
	float star = smoothstep(star_intensity, 0., layer.x);
	vec3 star_color = star * hue((COLOR + star_base_color), rand.y * star_hue_offset, 1);

    // Start with black space

    // Add celestial objects
		//color += generate_stars(direction, time);
	    //star_color += generate_nebula(dir, time);
	    star_color += generate_supernovae(dir, rand_num);
	    //star_color += generate_neutron_stars(dir, time);
    // Add subtle vignette effect for depth
    //float vignette = 1.0 - length(direction) * 0.1;
    //color *= vignette;
	float vignette = 1.0 - length(dir) * 0.1;
    star_color *= vignette;
    COLOR += star_color;
    ALPHA = 1.0;
}
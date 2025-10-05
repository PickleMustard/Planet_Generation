shader_type spatial;

render_mode unshaded;

uniform vec4 modelColor : source_color;
uniform vec4 wireframeColor : source_color;

uniform float width : hint_range(0.0, 15.) = 1.;
uniform float modelOpacity : hint_range(0.0, 1.0) = 1.;

uniform bool filtered = true;

const float tau = (1.0 *sqrt(5)) / 2.0;
const vec3 vectors[3] = {
	vec3(tau, 0.0 ,0.0),
	vec3(0.0, tau ,0.0),
	vec3(0.0, 0.0 ,tau)
};

varying vec3 baryCoord;

void vertex()
{
	baryCoord = vectors[VERTEX_ID % 3] * 1.05f;
}

void fragment() {
	vec3 dBaryCoordX = dFdxFine(baryCoord);
	vec3 dBaryCoordY = dFdyFine(baryCoord);
	vec3 dBaryCoord  = sqrt(dBaryCoordX*dBaryCoordX + dBaryCoordY*dBaryCoordY);

	vec3 remap = smoothstep(
		vec3(0.0),
		dBaryCoord,
		baryCoord
	);

	remap = filtered ? remap : step(.1, remap);

	float closestEdge = remap.z;

	ALBEDO = mix(wireframeColor, modelColor, closestEdge).xyz;
	ALPHA = mix(wireframeColor, modelColor, closestEdge).w;
}
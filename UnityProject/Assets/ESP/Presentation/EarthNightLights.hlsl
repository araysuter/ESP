#ifndef ESP_EARTH_NIGHT_LIGHTS_INCLUDED
#define ESP_EARTH_NIGHT_LIGHTS_INCLUDED
// NASA 2016 observed lights, geographic equirectangular; linear display intensity.
// Global bindings are supplied by the same UTC owner as the directional Sun.
TEXTURE2D(_EspNightLights); SAMPLER(sampler_EspNightLights);
float4 _EspSunDirectionEcef;
float _EspNightLightsStrength;

half3 EspNightEmission(float2 geographicUv)
{
    float longitude = (geographicUv.x - .5) * TWO_PI;
    float latitude = (geographicUv.y - .5) * PI;
    // Unity globe axes are ECEF X,Z,Y. UV-derived geodetic normals work for
    // map, rotated flight globe, Mercator tiles and local offline ground alike.
    float3 normalEcef = float3(cos(latitude) * cos(longitude), sin(latitude), cos(latitude) * sin(longitude));
    float solarElevationSine = dot(normalEcef, _EspSunDirectionEcef.xyz);
    // Smooth civil-twilight display fade: fully off above -1 degree; full at -8.
    half night = 1 - smoothstep(-.1391731, -.0174524, solarElevationSine);
    half light = SAMPLE_TEXTURE2D(_EspNightLights, sampler_EspNightLights, geographicUv).r;
    // Warm display tint is illustrative, not measured lamp spectra. No base glow.
    return light * night * _EspNightLightsStrength * half3(1, .68, .38);
}
#endif

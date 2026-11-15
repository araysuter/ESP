#ifndef ESP_EARTH_NIGHT_LIGHTS_INCLUDED
#define ESP_EARTH_NIGHT_LIGHTS_INCLUDED
// NASA 2016 observed lights, geographic equirectangular; linear display intensity.
// Global bindings are supplied by the same UTC owner as the directional Sun.
TEXTURE2D(_EspNightLights); SAMPLER(sampler_EspNightLights);
float4 _EspSunDirectionEcef;
float _EspNightLightsStrength;
TEXTURE2D(_EspObservedClouds); SAMPLER(sampler_EspObservedClouds);
float _EspObservedCloudsEnabled;

half EspSolarElevationSine(float2 geographicUv)
{
    float longitude = (geographicUv.x - .5) * TWO_PI;
    float latitude = (geographicUv.y - .5) * PI;
    // Unity globe axes are ECEF X,Z,Y. UV-derived geodetic normals work for
    // map, rotated flight globe, Mercator tiles and local offline ground alike.
    float3 normalEcef = float3(cos(latitude) * cos(longitude), sin(latitude), cos(latitude) * sin(longitude));
    float solarElevationSine = dot(normalEcef, _EspSunDirectionEcef.xyz);
    return solarElevationSine;
}

half EspNightFactor(float2 geographicUv)
{
    // Display fade: fully off above -1 degree; full at -8.
    return 1 - smoothstep(-.1391731, -.0174524, EspSolarElevationSine(geographicUv));
}

half EspObservedCloudCoverage(float2 geographicUv)
{
    if (_EspObservedCloudsEnabled < .5) return 0;
    // RG stores fraction * support, support. Normalize after filtering so
    // absent observations remain absent rather than becoming clear weather.
    half2 observation = SAMPLE_TEXTURE2D(_EspObservedClouds, sampler_EspObservedClouds, geographicUv).rg;
    half density = saturate(observation.r / max(observation.g, .00392h));
    return density * smoothstep(.03h, .50h, observation.g);
}

half EspNightCloudTransmission(half coverage)
{
    // Display optical approximation, not retrieved optical depth. Suppress
    // unresolved direct lamps rapidly under cloud; full coverage transmits none.
    half clear = 1 - saturate(coverage);
    return clear * clear * clear * clear;
}

half3 EspNightEmission(float2 geographicUv)
{
    half night = EspNightFactor(geographicUv);
    half light = SAMPLE_TEXTURE2D(_EspNightLights, sampler_EspNightLights, geographicUv).r;
    // Warm display tint is illustrative, not measured lamp spectra. No base glow.
    // The observed shell composites over this emission and owns transmission.
    // Applying it here too would square the attenuation in partly cloudy areas.
    return light * night * _EspNightLightsStrength * half3(1, .68, .38);
}
// The real-metre tile camera renders after the scaled cloud backdrop. Composite
// the same observed layer onto tiles above the cloud height so their opaque
// imagery cannot cover it. Geographic column sampling is a display approximation,
// not a volumetric ray march or a measured cloud base/thickness.
half4 EspLocalCloudLayer(float2 geographicUv, float cameraAltitudeMeters)
{
    if (_EspObservedCloudsEnabled < .5 || cameraAltitudeMeters <= 9500) return half4(0,0,0,0);
    half coverage = EspObservedCloudCoverage(geographicUv);
    half solar = EspSolarElevationSine(geographicUv);
    half night = 1 - smoothstep(-.1391731h, -.0174524h, solar);
    half opacity = lerp(coverage * .35h, 1 - EspNightCloudTransmission(coverage), night);
    half above = smoothstep(9500, 10500, cameraAltitudeMeters);
    half illumination = lerp(.30h, .006h, night) + .70h * saturate(solar);
    return half4(half3(.94h,.97h,1) * illumination, opacity * above);
}
#endif

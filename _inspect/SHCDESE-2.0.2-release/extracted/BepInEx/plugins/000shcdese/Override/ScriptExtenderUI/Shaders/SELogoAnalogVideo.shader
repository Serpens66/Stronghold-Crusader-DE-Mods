/*
 * SHCDESE Unity port of "VCR Analog Distortions" by LazarusOverlook,
 * itself a Godot 4 port of Ryk's VCR distortion shader.
 *
 * Sources:
 *   https://godotshaders.com/shader/vcr-analog-distortions/
 *   https://www.shadertoy.com/view/ldjGzV
 *
 * License: Creative Commons Attribution-NonCommercial-ShareAlike 4.0
 *   https://creativecommons.org/licenses/by-nc-sa/4.0/
 *
 * Changes for SHCDESE:
 *   - Ported GLSL/Shadertoy uniforms and sampling to Unity ShaderLab/HLSL.
 *   - Replaced the external noise channel with deterministic procedural noise.
 *   - Added independent static and scanline strength controls.
 *   - Uses Unity's built-in _Time value for editor and runtime animation.
 *   - Added configurable transparent output with optional source-alpha use.
 *   - Added centered aspect-fill sampling for the square diamond surface.
 *   - Uses an editor-visible gray preview texture instead of hidden black input.
 *
 * This shader and adaptations of it are distributed under CC BY-NC-SA 4.0.
 */

Shader "SHCDESE/LogoAnalogVideo"
{
    Properties
    {
        _MainTex ("Video / Preview Texture", 2D) = "gray" {}
        _StaticStrength ("Static Strength", Range(0, 1)) = 1
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 1
        _DistortionStrength ("Image Distortion", Range(0, 1)) = 0.65
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.75
        _Opacity ("Output Opacity", Range(0, 1)) = 0.85
        [Toggle] _UseSourceAlpha ("Multiply By Source Alpha", Float) = 0
        [HideInInspector] _SourceAspect ("Source Aspect Ratio", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _StaticStrength;
            float _ScanlineStrength;
            float _DistortionStrength;
            float _VignetteStrength;
            float _Opacity;
            float _UseSourceAlpha;
            float _SourceAspect;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p, float time)
            {
                float2 cell = floor(p * 256.0 + float2(time * 113.0, -time * 79.0));
                float s = hash21(cell);
                return s * s;
            }

            float onOff(float a, float b, float c, float time)
            {
                return step(c, sin(time + a * cos(time * b)));
            }

            float ramp(float y, float start, float end)
            {
                float inside = step(start, y) - step(end, y);
                float fact = (y - start) / (end - start) * inside;
                return (1.0 - fact) * inside;
            }

            float stripes(float2 uv, float time)
            {
                float noi = noise(uv * float2(0.5, 1.0) + float2(1.0, 3.0), time);
                float band = frac(uv.y * 4.0 + time / 2.0 + sin(time + sin(time * 0.63)));
                return ramp(band, 0.5, 0.6) * noi;
            }

            float2 getVideoUV(float2 uv, float time)
            {
                float2 look = uv;
                float sweep = frac(time / 4.0);
                float window = 1.0 / (1.0 + 20.0 * (look.y - sweep) * (look.y - sweep));
                look.x += sin(look.y * 10.0 + time) / 50.0 *
                          onOff(4.0, 4.0, 0.3, time) * (1.0 + cos(time * 80.0)) * window;
                float vShift = 0.4 * onOff(2.0, 3.0, 0.9, time) *
                               (sin(time) * sin(time * 20.0) +
                               (0.5 + 0.1 * sin(time * 200.0) * cos(time)));
                look.y = frac(look.y + vShift);
                return look;
            }

            float2 screenDistort(float2 uv)
            {
                uv -= float2(0.5, 0.5);
                uv = uv * 1.2 * (1.0 / 1.2 + 2.0 * uv.x * uv.x * uv.y * uv.y);
                uv += float2(0.5, 0.5);
                return uv;
            }

            // Map the square logo surface into a centered, aspect-fill window of the
            // decoded video. Landscape clips crop equally on the left and right; portrait
            // clips crop equally at the top and bottom.
            float2 centerAspectFillUV(float2 uv)
            {
                float aspect = max(_SourceAspect, 0.0001);
                if (aspect > 1.0)
                    uv.x = (uv.x - 0.5) / aspect + 0.5;
                else
                    uv.y = (uv.y - 0.5) * aspect + 0.5;
                return uv;
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;

                float time = _Time.y;
                float distortionAmount = _StaticStrength * _DistortionStrength;
                float2 distorted = screenDistort(uv);
                distorted = getVideoUV(distorted, time);
                float2 videoUV = centerAspectFillUV(
                    saturate(lerp(uv, distorted, distortionAmount)));

                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                    videoUV.y = 1.0 - videoUV.y;
                #endif

                fixed4 source = tex2D(_MainTex, videoUV);
                float3 video = source.rgb;

                float staticNoise = noise(uv * 2.0, time);
                video += staticNoise * 0.28 * _StaticStrength;
                video += stripes(uv, time) * 0.55 * _ScanlineStrength;

                float vigAmt = 3.0 + 0.3 * sin(time + 5.0 * cos(time * 5.0));
                float vignette = (1.0 - vigAmt * (uv.y - 0.5) * (uv.y - 0.5)) *
                                 (1.0 - vigAmt * (uv.x - 0.5) * (uv.x - 0.5));
                video *= lerp(1.0, saturate(vignette), _StaticStrength * _VignetteStrength);

                float fineScanline = (12.0 + frac(uv.y * 30.0 + time)) / 13.0;
                video *= lerp(1.0, fineScanline, _ScanlineStrength);

                // MP4/WebM decoder textures normally contain no meaningful alpha.
                float sourceAlpha = lerp(1.0, source.a, saturate(_UseSourceAlpha));
                return fixed4(saturate(video), saturate(sourceAlpha * _Opacity));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
